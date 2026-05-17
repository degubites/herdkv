using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using System.Text;

namespace Degubites.HerdKV
{
internal sealed class AppendOnlyHerdKVStore : IHerdKVStore
{
    private readonly string _path;
    private readonly HerdKVOptions _options;
    private readonly Dictionary<string, IndexEntry> _index = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _keys = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private int _minSegmentId = 1;
    private int _activeSegmentId = 1;
    private long _activeSegmentLength;
    private long _totalBytes;
    private long _sequence;
    private FileStream? _activeWriter;
    private int _activeWriterSegmentId;
    private bool _activeWriterDirty;
    private bool _disposed;

    private AppendOnlyHerdKVStore(string path, HerdKVOptions options)
    {
        _path = path;
        _options = options;
    }

    public static async ValueTask<IHerdKVStore> OpenAsync(
        string path,
        HerdKVOptions options,
        CancellationToken cancellationToken = default)
    {
        var store = new AppendOnlyHerdKVStore(path, options);
        await store.OpenCoreAsync(cancellationToken);
        return store;
    }

    public async ValueTask PutAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        byte[] keyBytes = HerdKVKey.Encode(key);
        byte[] valueBytes = value.ToArray();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await PutCoreAsync(key, keyBytes, valueBytes, cancellationToken);
            await FlushAfterWriteIfNeededAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask WriteBatchAsync(
        IEnumerable<HerdKVBatchOperation> operations,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        List<PendingBatchOperation> batch = CoalesceBatchOperations(operations);
        if (batch.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (PendingBatchOperation operation in batch)
            {
                if (operation.IsDelete)
                {
                    await DeleteCoreAsync(operation.Key, operation.KeyBytes, cancellationToken);
                }
                else
                {
                    await PutCoreAsync(operation.Key, operation.KeyBytes, operation.ValueBytes!, cancellationToken);
                }
            }

            await FlushAfterWriteIfNeededAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        IndexEntry entry;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_index.TryGetValue(key, out entry))
            {
                return null;
            }

            if (!_options.VerifyChecksumOnRead && _values.TryGetValue(key, out byte[]? cachedValue))
            {
                return Copy(cachedValue);
            }

            await FlushActiveWriterAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        return await ReadValueAsync(key, entry, _options.VerifyChecksumOnRead, cancellationToken);
    }

    public async ValueTask<IReadOnlyList<string>> ListKeysAsync(
        string prefix = "",
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (prefix is null)
        {
            throw new ArgumentNullException(nameof(prefix));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return ListKeysCore(prefix);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        byte[] keyBytes = HerdKVKey.Encode(key);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            bool deleted = await DeleteCoreAsync(key, keyBytes, cancellationToken);
            await FlushAfterWriteIfNeededAsync(cancellationToken);
            return deleted;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await FlushActiveWriterAsync(cancellationToken);
            await WriteManifestAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask CompactAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await CloseActiveWriterAsync(cancellationToken);

            var live = _index.ToArray();
            int newMinSegmentId = _activeSegmentId + 1;
            int newActiveSegmentId = newMinSegmentId;
            long newActiveLength = 0;
            long newTotalBytes = 0;
            var newIndex = new Dictionary<string, IndexEntry>(StringComparer.Ordinal);
            Dictionary<string, byte[]>? newValues = CacheValuesInMemory
                ? new Dictionary<string, byte[]>(StringComparer.Ordinal)
                : null;

            foreach (KeyValuePair<string, IndexEntry> pair in live)
            {
                byte[]? value = await ReadValueAsync(pair.Key, pair.Value, verifyChecksum: true, cancellationToken);
                if (value is null)
                {
                    continue;
                }

                byte[] keyBytes = HerdKVKey.Encode(pair.Key);
                int recordLength = HerdKVRecord.GetRecordLength(keyBytes.Length, value.Length);
                if (newActiveLength > 0 && newActiveLength + recordLength > _options.SegmentSizeBytes)
                {
                    newActiveSegmentId++;
                    newActiveLength = 0;
                }

                long offset = newActiveLength;
                byte[] record = HerdKVRecord.Create(keyBytes, value, HerdKVRecordFlags.None, ++_sequence);
                await AppendRecordAsync(newActiveSegmentId, record, cancellationToken);

                newActiveLength += recordLength;
                newTotalBytes += recordLength;
                newIndex[pair.Key] = new IndexEntry(newActiveSegmentId, offset, keyBytes.Length, value.Length, recordLength);
                newValues?.Add(pair.Key, value);
            }

            _index.Clear();
            _keys.Clear();
            foreach (KeyValuePair<string, IndexEntry> pair in newIndex)
            {
                _index[pair.Key] = pair.Value;
                _keys.Add(pair.Key);
            }

            _values.Clear();
            if (newValues is not null)
            {
                foreach (KeyValuePair<string, byte[]> pair in newValues)
                {
                    _values[pair.Key] = pair.Value;
                }
            }

            _minSegmentId = newMinSegmentId;
            _activeSegmentId = newActiveSegmentId;
            _activeSegmentLength = newActiveLength;
            _totalBytes = newTotalBytes;
            await CloseActiveWriterAsync(cancellationToken);
            await WriteManifestAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public HerdKVStats GetStats()
    {
        ThrowIfDisposed();
        long liveBytes = _index.Values.Sum(entry => (long)entry.RecordSizeBytes);
        return new HerdKVStats(
            _index.Count,
            Math.Max(0, _activeSegmentId - _minSegmentId + 1),
            _totalBytes,
            liveBytes,
            Math.Max(0, _totalBytes - liveBytes));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            await CloseActiveWriterAsync(CancellationToken.None);
            _disposed = true;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async ValueTask OpenCoreAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_path);
        ReadManifest();

        _index.Clear();
        _values.Clear();
        _keys.Clear();
        _totalBytes = 0;
        _activeSegmentLength = 0;

        for (int segmentId = _minSegmentId; segmentId <= _activeSegmentId; segmentId++)
        {
            string segmentPath = GetSegmentPath(segmentId);
            if (!File.Exists(segmentPath))
            {
                continue;
            }

            long validLength = await RecoverSegmentAsync(segmentId, segmentPath, cancellationToken);
            _totalBytes += validLength;
            if (segmentId == _activeSegmentId)
            {
                _activeSegmentLength = validLength;
            }
        }

        if (_activeSegmentId < _minSegmentId)
        {
            _activeSegmentId = _minSegmentId;
        }

        await WriteManifestAsync(cancellationToken);
    }

    private async ValueTask<long> RecoverSegmentAsync(int segmentId, string segmentPath, CancellationToken cancellationToken)
    {
        long offset = 0;

        await using var file = new FileStream(segmentPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        while (offset < file.Length)
        {
            long recordOffset = offset;
            byte[] headerBytes = new byte[HerdKVConstants.HeaderSize];
            int headerRead = await ReadAtMostAsync(file, headerBytes, cancellationToken);
            if (headerRead != HerdKVConstants.HeaderSize)
            {
                file.SetLength(recordOffset);
                return recordOffset;
            }

            if (!HerdKVRecord.TryReadHeader(headerBytes, out HerdKVRecordHeader header))
            {
                file.SetLength(recordOffset);
                return recordOffset;
            }

            if (recordOffset + header.RecordLength > file.Length)
            {
                file.SetLength(recordOffset);
                return recordOffset;
            }

            byte[] record = new byte[header.RecordLength];
            Buffer.BlockCopy(headerBytes, 0, record, 0, headerBytes.Length);
            int payloadLength = header.RecordLength - HerdKVConstants.HeaderSize;
            int payloadRead = await ReadAtMostAsync(file, record.AsMemory(HerdKVConstants.HeaderSize, payloadLength), cancellationToken);
            if (payloadRead != payloadLength || !HerdKVRecord.Validate(record, out header))
            {
                file.SetLength(recordOffset);
                return recordOffset;
            }

            string key = Encoding.UTF8.GetString(record, HerdKVConstants.HeaderSize, header.KeyLength);
            if ((header.Flags & HerdKVRecordFlags.Tombstone) == HerdKVRecordFlags.Tombstone)
            {
                _index.Remove(key);
                _values.Remove(key);
                _keys.Remove(key);
            }
            else
            {
                _index[key] = new IndexEntry(segmentId, recordOffset, header.KeyLength, header.ValueLength, header.RecordLength);
                _keys.Add(key);
                if (CacheValuesInMemory)
                {
                    _values[key] = CopyValue(record, header);
                }
            }

            _sequence = Math.Max(_sequence, header.Sequence);
            offset += header.RecordLength;
        }

        return offset;
    }

    private async ValueTask<byte[]?> ReadValueAsync(
        string expectedKey,
        IndexEntry entry,
        bool verifyChecksum,
        CancellationToken cancellationToken)
    {
        string segmentPath = GetSegmentPath(entry.SegmentId);
        if (!File.Exists(segmentPath))
        {
            return null;
        }

        await using var file = new FileStream(segmentPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        file.Position = entry.Offset;

        byte[] record = new byte[entry.RecordSizeBytes];
        int read = await ReadAtMostAsync(file, record, cancellationToken);
        if (read != record.Length)
        {
            throw new HerdKVCorruptRecordException($"Record for key '{expectedKey}' could not be read completely.");
        }

        HerdKVRecordHeader validatedHeader;
        if (verifyChecksum)
        {
            if (!HerdKVRecord.Validate(record, out validatedHeader))
            {
                throw new HerdKVCorruptRecordException($"Record for key '{expectedKey}' failed checksum validation.");
            }
        }
        else if (!HerdKVRecord.TryReadHeader(record.AsSpan(0, HerdKVConstants.HeaderSize), out validatedHeader)
            || validatedHeader.RecordLength != record.Length)
        {
            throw new HerdKVCorruptRecordException($"Record for key '{expectedKey}' is corrupt.");
        }

        string actualKey = Encoding.UTF8.GetString(record, HerdKVConstants.HeaderSize, validatedHeader.KeyLength);
        if (!string.Equals(expectedKey, actualKey, StringComparison.Ordinal))
        {
            throw new HerdKVCorruptRecordException($"Record key mismatch. Expected '{expectedKey}', got '{actualKey}'.");
        }

        if ((validatedHeader.Flags & HerdKVRecordFlags.Tombstone) == HerdKVRecordFlags.Tombstone)
        {
            return null;
        }

        byte[] value = new byte[validatedHeader.ValueLength];
        Buffer.BlockCopy(
            record,
            HerdKVConstants.HeaderSize + validatedHeader.KeyLength,
            value,
            0,
            value.Length);
        return value;
    }

    private async ValueTask EnsureWritableSegmentAsync(int recordLength, CancellationToken cancellationToken)
    {
        if (_activeSegmentLength > 0 && _activeSegmentLength + recordLength > _options.SegmentSizeBytes)
        {
            await CloseActiveWriterAsync(cancellationToken);
            _activeSegmentId++;
            _activeSegmentLength = 0;
            await WriteManifestAsync(cancellationToken);
        }
    }

    private async ValueTask PutCoreAsync(
        string key,
        byte[] keyBytes,
        byte[] valueBytes,
        CancellationToken cancellationToken)
    {
        int recordLength = HerdKVRecord.GetRecordLength(keyBytes.Length, valueBytes.Length);
        await EnsureWritableSegmentAsync(recordLength, cancellationToken);

        long offset = _activeSegmentLength;
        byte[] record = HerdKVRecord.Create(keyBytes, valueBytes, HerdKVRecordFlags.None, ++_sequence);
        await AppendRecordAsync(_activeSegmentId, record, cancellationToken);

        _activeSegmentLength += recordLength;
        _totalBytes += recordLength;
        _index[key] = new IndexEntry(_activeSegmentId, offset, keyBytes.Length, valueBytes.Length, recordLength);
        _keys.Add(key);

        if (CacheValuesInMemory)
        {
            _values[key] = valueBytes;
        }
    }

    private async ValueTask<bool> DeleteCoreAsync(
        string key,
        byte[] keyBytes,
        CancellationToken cancellationToken)
    {
        if (!_index.ContainsKey(key))
        {
            _values.Remove(key);
            _keys.Remove(key);
            return false;
        }

        int recordLength = HerdKVRecord.GetRecordLength(keyBytes.Length, 0);
        await EnsureWritableSegmentAsync(recordLength, cancellationToken);

        byte[] record = HerdKVRecord.Create(keyBytes, ReadOnlySpan<byte>.Empty, HerdKVRecordFlags.Tombstone, ++_sequence);
        await AppendRecordAsync(_activeSegmentId, record, cancellationToken);

        _activeSegmentLength += recordLength;
        _totalBytes += recordLength;
        _index.Remove(key);
        _values.Remove(key);
        _keys.Remove(key);
        return true;
    }

    private async ValueTask AppendRecordAsync(int segmentId, byte[] record, CancellationToken cancellationToken)
    {
        FileStream file = await GetActiveWriterAsync(segmentId, cancellationToken);
        await file.WriteAsync(record, 0, record.Length, cancellationToken);
        _activeWriterDirty = true;
    }

    private async ValueTask FlushAfterWriteIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_options.FlushMode != HerdKVFlushMode.Manual)
        {
            await FlushActiveWriterAsync(cancellationToken);
        }
    }

    private async ValueTask<FileStream> GetActiveWriterAsync(int segmentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_activeWriter is not null && _activeWriterSegmentId == segmentId)
        {
            return _activeWriter;
        }

        await CloseActiveWriterAsync(cancellationToken);

        var file = new FileStream(
            GetSegmentPath(segmentId),
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.ReadWrite,
            bufferSize: 4096,
            ActiveWriterOptions);
        file.Position = file.Length;
        _activeWriter = file;
        _activeWriterSegmentId = segmentId;
        _activeWriterDirty = false;
        return file;
    }

    private async ValueTask FlushActiveWriterAsync(CancellationToken cancellationToken)
    {
        if (_activeWriter is not null && _activeWriterDirty)
        {
            await _activeWriter.FlushAsync(cancellationToken);
            _activeWriterDirty = false;
        }
    }

    private async ValueTask CloseActiveWriterAsync(CancellationToken cancellationToken)
    {
        if (_activeWriter is null)
        {
            return;
        }

        await FlushActiveWriterAsync(cancellationToken);
        await _activeWriter.DisposeAsync();
        _activeWriter = null;
        _activeWriterSegmentId = 0;
        _activeWriterDirty = false;
    }

    private void ReadManifest()
    {
        string manifestPath = Path.Combine(_path, HerdKVConstants.ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            int highestSegment = GetHighestSegmentId();
            _minSegmentId = 1;
            _activeSegmentId = Math.Max(1, highestSegment);
            return;
        }

        foreach (string line in File.ReadAllLines(manifestPath))
        {
            string[] parts = line.Split('=');
            if (parts.Length != 2)
            {
                continue;
            }

            if (parts[0] == "minSegment" && int.TryParse(parts[1], out int minSegment))
            {
                _minSegmentId = Math.Max(1, minSegment);
            }
            else if (parts[0] == "activeSegment" && int.TryParse(parts[1], out int activeSegment))
            {
                _activeSegmentId = Math.Max(1, activeSegment);
            }
        }
    }

    private async ValueTask WriteManifestAsync(CancellationToken cancellationToken)
    {
        string text = $"version=1{Environment.NewLine}minSegment={_minSegmentId}{Environment.NewLine}activeSegment={_activeSegmentId}{Environment.NewLine}";
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        await WriteAllBytesAtomicallyAsync(Path.Combine(_path, HerdKVConstants.ManifestFileName), bytes, cancellationToken);
    }

    private async ValueTask WriteAllBytesAtomicallyAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        string tempPath = path + ".tmp";
        string backupPath = path + ".bak";

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        await using (var file = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            ActiveWriterOptions))
        {
            await file.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
            await file.FlushAsync(cancellationToken);
        }

        if (File.Exists(path))
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            try
            {
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (PlatformNotSupportedException)
            {
                File.Delete(path);
                File.Move(tempPath, path);
            }
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private int GetHighestSegmentId()
    {
        int highest = 0;
        foreach (string file in Directory.EnumerateFiles(_path, "*" + HerdKVConstants.SegmentExtension))
        {
            if (int.TryParse(Path.GetFileNameWithoutExtension(file), out int segmentId))
            {
                highest = Math.Max(highest, segmentId);
            }
        }

        return highest;
    }

    private string GetSegmentPath(int segmentId)
    {
        return Path.Combine(_path, $"{segmentId:D6}{HerdKVConstants.SegmentExtension}");
    }

    private bool CacheValuesInMemory => !_options.VerifyChecksumOnRead;

    private FileOptions ActiveWriterOptions =>
        _options.FlushMode == HerdKVFlushMode.WriteThrough ? FileOptions.WriteThrough : FileOptions.None;

    private IReadOnlyList<string> ListKeysCore(string prefix)
    {
        if (prefix.Length == 0)
        {
            return _keys.ToArray();
        }

        string? upperBound = GetExclusivePrefixUpperBound(prefix);
        IEnumerable<string> candidates = upperBound is null
            ? _keys
            : _keys.GetViewBetween(prefix, upperBound);

        var keys = new List<string>();
        foreach (string key in candidates)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                keys.Add(key);
            }
            else if (keys.Count > 0)
            {
                break;
            }
        }

        return keys;
    }

    private static string? GetExclusivePrefixUpperBound(string prefix)
    {
        char[] chars = prefix.ToCharArray();
        for (int i = chars.Length - 1; i >= 0; i--)
        {
            if (chars[i] < char.MaxValue)
            {
                chars[i]++;
                return new string(chars, 0, i + 1);
            }
        }

        return null;
    }

    private static List<PendingBatchOperation> CoalesceBatchOperations(IEnumerable<HerdKVBatchOperation> operations)
    {
        if (operations is null)
        {
            throw new ArgumentNullException(nameof(operations));
        }

        var batch = new List<PendingBatchOperation>();
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (HerdKVBatchOperation? operation in operations)
        {
            if (operation is null)
            {
                throw new ArgumentException("Batch operations must not contain null entries.", nameof(operations));
            }

            byte[] keyBytes = HerdKVKey.Encode(operation.Key);
            byte[]? valueBytes = operation.IsDelete ? null : operation.Value.ToArray();
            var pending = new PendingBatchOperation(operation.Key, keyBytes, valueBytes, operation.IsDelete);

            if (indexes.TryGetValue(operation.Key, out int index))
            {
                batch[index] = pending;
            }
            else
            {
                indexes.Add(operation.Key, batch.Count);
                batch.Add(pending);
            }
        }

        return batch;
    }

    private static async ValueTask<int> ReadAtMostAsync(FileStream file, byte[] buffer, CancellationToken cancellationToken)
    {
        return await ReadAtMostAsync(file, buffer.AsMemory(), cancellationToken);
    }

    private static async ValueTask<int> ReadAtMostAsync(FileStream file, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await file.ReadAsync(buffer.Slice(totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static byte[] Copy(byte[] value)
    {
        var copy = new byte[value.Length];
        Buffer.BlockCopy(value, 0, copy, 0, value.Length);
        return copy;
    }

    private static byte[] CopyValue(byte[] record, HerdKVRecordHeader header)
    {
        var value = new byte[header.ValueLength];
        Buffer.BlockCopy(
            record,
            HerdKVConstants.HeaderSize + header.KeyLength,
            value,
            0,
            value.Length);
        return value;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AppendOnlyHerdKVStore));
        }
    }

    private readonly struct PendingBatchOperation
    {
        public PendingBatchOperation(string key, byte[] keyBytes, byte[]? valueBytes, bool isDelete)
        {
            Key = key;
            KeyBytes = keyBytes;
            ValueBytes = valueBytes;
            IsDelete = isDelete;
        }

        public string Key { get; }

        public byte[] KeyBytes { get; }

        public byte[]? ValueBytes { get; }

        public bool IsDelete { get; }
    }
}
}
