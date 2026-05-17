using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Degubites.HerdKV.Editor
{
public sealed class HerdKVViewerWindow : EditorWindow
{
    private const string ManifestFileName = "MANIFEST";
    private const string SegmentExtension = ".hseg";

    private string databasePath = string.Empty;
    private string search = string.Empty;
    private Vector2 scroll;
    private Vector2 databaseScroll;
    private HerdKVInspectionReport? report;
    private HerdKVInspectionEntry? selected;
    private string preview = string.Empty;
    private IReadOnlyList<string> discoveredDatabases = Array.Empty<string>();

    [MenuItem("Window/HerdKV/Viewer")]
    public static void Open()
    {
        GetWindow<HerdKVViewerWindow>("HerdKV Viewer");
    }

    private void OnEnable()
    {
        databasePath = Path.Combine(Application.persistentDataPath, "HerdKV");
        Refresh();
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            databasePath = EditorGUILayout.TextField(databasePath, GUILayout.MinWidth(240));
            if (GUILayout.Button("Open", EditorStyles.toolbarButton, GUILayout.Width(64)))
            {
                string? selectedPath = EditorUtility.OpenFolderPanel("Open HerdKV Database", databasePath, string.Empty);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    databasePath = selectedPath;
                    Refresh();
                }
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(72)))
            {
                Refresh();
            }

            if (GUILayout.Button("Compact", EditorStyles.toolbarButton, GUILayout.Width(72)))
            {
                Compact();
            }
        }

        search = EditorGUILayout.TextField("Search", search);

        if (report is null)
        {
            if (discoveredDatabases.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "This folder contains HerdKV databases. Select a database folder below to inspect keys.",
                    MessageType.Info);
                DrawDiscoveredDatabases();
            }
            else
            {
                EditorGUILayout.HelpBox("Open a HerdKV database folder to inspect keys.", MessageType.Info);
            }

            return;
        }

        EditorGUILayout.LabelField($"Keys {report.KeyCount}  Total {report.TotalBytes} B  Dead {report.DeadBytes} B");
        if (report.HasWarnings)
        {
            EditorGUILayout.HelpBox(string.Join("\n", report.Warnings), MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (var view = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Width(position.width * 0.5f)))
            {
                scroll = view.scrollPosition;
                bool drewEntry = false;
                foreach (HerdKVInspectionEntry entry in report.Entries)
                {
                    if (!string.IsNullOrWhiteSpace(search) &&
                        entry.Key.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (GUILayout.Button($"{entry.Key}  ({entry.ValueSizeBytes} B)", EditorStyles.miniButtonLeft))
                    {
                        selected = entry;
                        LoadPreview();
                    }

                    drewEntry = true;
                }

                if (!drewEntry)
                {
                    EditorGUILayout.HelpBox("No live keys match the current search.", MessageType.Info);
                }
            }

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(selected?.Key ?? "No key selected", EditorStyles.boldLabel);
                if (selected is not null)
                {
                    EditorGUILayout.LabelField("Segment", selected.SegmentFileName);
                    EditorGUILayout.LabelField("Offset", selected.Offset.ToString());
                    EditorGUILayout.LabelField("Value bytes", selected.ValueSizeBytes.ToString());
                    EditorGUILayout.LabelField("Record bytes", selected.RecordSizeBytes.ToString());
                }

                EditorGUILayout.TextArea(preview, GUILayout.ExpandHeight(true));

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = selected is not null;
                    if (GUILayout.Button("Export"))
                    {
                        ExportSelected();
                    }

                    if (GUILayout.Button("Delete"))
                    {
                        DeleteSelected();
                    }

                    GUI.enabled = true;
                }
            }
        }
    }

    private async void Refresh()
    {
        if (!Directory.Exists(databasePath))
        {
            report = null;
            selected = null;
            preview = string.Empty;
            discoveredDatabases = Array.Empty<string>();
            Repaint();
            return;
        }

        if (!IsDatabaseFolder(databasePath))
        {
            report = null;
            selected = null;
            preview = string.Empty;
            discoveredDatabases = FindDatabaseFolders(databasePath);
            Repaint();
            return;
        }

        discoveredDatabases = Array.Empty<string>();
        report = await HerdKVInspector.InspectAsync(databasePath);
        selected = null;
        preview = string.Empty;
        Repaint();
    }

    private async void Compact()
    {
        if (!Directory.Exists(databasePath))
        {
            return;
        }

        await using IHerdKVStore store = await HerdKVStore.OpenAsync(databasePath);
        await store.CompactAsync();
        Refresh();
    }

    private async void LoadPreview()
    {
        if (selected is null)
        {
            preview = string.Empty;
            return;
        }

        byte[]? value = await HerdKVInspector.ReadValueAsync(databasePath, selected);
        if (value is null)
        {
            preview = string.Empty;
            return;
        }

        preview = IsUtf8(value) ? Encoding.UTF8.GetString(value) : BitConverter.ToString(value).Replace("-", " ");
        Repaint();
    }

    private async void DeleteSelected()
    {
        if (selected is null || !EditorUtility.DisplayDialog("Delete Key", selected.Key, "Delete", "Cancel"))
        {
            return;
        }

        await using IHerdKVStore store = await HerdKVStore.OpenAsync(databasePath);
        await store.DeleteAsync(selected.Key);
        Refresh();
    }

    private async void ExportSelected()
    {
        if (selected is null)
        {
            return;
        }

        string path = EditorUtility.SaveFilePanel("Export HerdKV Value", string.Empty, selected.Key.Replace('/', '_'), "bin");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        byte[]? value = await HerdKVInspector.ReadValueAsync(databasePath, selected);
        if (value is not null)
        {
            File.WriteAllBytes(path, value);
        }
    }

    private static bool IsUtf8(byte[] value)
    {
        try
        {
            Encoding.UTF8.GetString(value);
            return value.All(b => b == 9 || b == 10 || b == 13 || b >= 32);
        }
        catch
        {
            return false;
        }
    }

    private void DrawDiscoveredDatabases()
    {
        using (var view = new EditorGUILayout.ScrollViewScope(databaseScroll))
        {
            databaseScroll = view.scrollPosition;
            foreach (string path in discoveredDatabases)
            {
                string label = MakeDisplayPath(path);
                if (GUILayout.Button(label, EditorStyles.miniButtonLeft))
                {
                    databasePath = path;
                    Refresh();
                }
            }
        }
    }

    private string MakeDisplayPath(string path)
    {
        string root = Path.Combine(Application.persistentDataPath, "HerdKV");
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return path;
    }

    private static bool IsDatabaseFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        if (File.Exists(Path.Combine(path, ManifestFileName)))
        {
            return true;
        }

        try
        {
            return Directory.EnumerateFiles(path, "*" + SegmentExtension).Any();
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> FindDatabaseFolders(string root)
    {
        var results = new List<string>();
        CollectDatabaseFolders(root, results, depthRemaining: 3);
        return results.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void CollectDatabaseFolders(string root, List<string> results, int depthRemaining)
    {
        if (depthRemaining <= 0)
        {
            return;
        }

        string[] directories;
        try
        {
            directories = Directory.GetDirectories(root);
        }
        catch
        {
            return;
        }

        foreach (string directory in directories)
        {
            if (IsDatabaseFolder(directory))
            {
                results.Add(directory);
            }
            else
            {
                CollectDatabaseFolders(directory, results, depthRemaining - 1);
            }
        }
    }
}
}
