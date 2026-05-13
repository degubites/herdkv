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
    private string databasePath = string.Empty;
    private string search = string.Empty;
    private Vector2 scroll;
    private HerdKVInspectionReport? report;
    private HerdKVInspectionEntry? selected;
    private string preview = string.Empty;

    [MenuItem("Window/HerdKV/Viewer")]
    public static void Open()
    {
        GetWindow<HerdKVViewerWindow>("HerdKV Viewer");
    }

    private void OnEnable()
    {
        databasePath = Path.Combine(Application.persistentDataPath, "HerdKV");
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
            EditorGUILayout.HelpBox("Open a HerdKV database folder to inspect keys.", MessageType.Info);
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
                }
            }

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(selected?.Key ?? "No key selected", EditorStyles.boldLabel);
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
            Repaint();
            return;
        }

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
}
}
