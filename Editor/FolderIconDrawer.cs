// Copyright (c) 2025-2026 sakurayuki

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorAssetBrowser.Services;
using UnityEngine;

namespace UnityEditorAssetBrowser
{
    [InitializeOnLoad]
    public static class FolderIconDrawer
    {
        private const string ShowThumbnailKey = "UnityEditorAssetBrowser_ShowFolderThumbnail";
        private const int MaxDepth = 4;
        private static readonly Dictionary<string, List<string>> IconIndex =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D> TextureCache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D[]> FolderTextureCache =
            new Dictionary<string, Texture2D[]>(StringComparer.OrdinalIgnoreCase);
        private static bool _registered;
        private static bool _rebuildScheduled;

        static FolderIconDrawer()
        {
            SetEnabled(EditorPrefs.GetBool(ShowThumbnailKey, true));
        }

        public static void SetEnabled(bool enabled)
        {
            if (enabled && !_registered)
            {
                EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
                _registered = true;
                ScheduleIndexRebuild();
            }
            else if (!enabled && _registered)
            {
                EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
                _registered = false;
            }
        }

        internal static void ScheduleIndexRebuild()
        {
            if (_rebuildScheduled) return;
            _rebuildScheduled = true;
            EditorApplication.delayCall += RebuildIconIndex;
        }

        private static void RebuildIconIndex()
        {
            _rebuildScheduled = false;
            IconIndex.Clear();
            TextureCache.Clear();
            FolderTextureCache.Clear();
            if (!_registered) return;

            foreach (string guid in AssetDatabase.FindAssets("FolderIcon t:Texture2D", new[] { "Assets" }))
            {
                string iconPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!string.Equals(Path.GetFileName(iconPath), "FolderIcon.jpg", StringComparison.OrdinalIgnoreCase))
                    continue;

                string current = (Path.GetDirectoryName(iconPath) ?? string.Empty).Replace('\\', '/');
                for (int depth = 0; depth <= MaxDepth && !string.IsNullOrEmpty(current); depth++)
                {
                    if (ExcludeFolderService.IsExcludedFolder(Path.GetFileName(current))) break;
                    if (!IconIndex.TryGetValue(current, out var icons))
                        IconIndex[current] = icons = new List<string>(4);
                    if (icons.Count < 4 && !icons.Contains(iconPath)) icons.Add(iconPath);
                    if (string.Equals(current, "Assets", StringComparison.OrdinalIgnoreCase)) break;
                    current = (Path.GetDirectoryName(current) ?? string.Empty).Replace('\\', '/');
                }
            }
            EditorApplication.RepaintProjectWindow();
        }

        private static void OnProjectWindowItemGUI(string guid, Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IconIndex.TryGetValue(path, out var paths)) return;

            var textures = GetTextures(path, paths);
            if (textures.Length == 0) return;
            Rect imageRect = rect.height > 20
                ? new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.width + 2)
                : new Rect(rect.x + (rect.x > 20 ? -1 : 2), rect.y - 1, rect.height + 2, rect.height + 2);

            if (textures.Length == 1)
            {
                GUI.DrawTexture(imageRect, textures[0]);
                return;
            }

            float halfWidth = imageRect.width * 0.5f;
            float halfHeight = imageRect.height * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                var part = new Rect(imageRect.x + i % 2 * halfWidth, imageRect.y + i / 2 * halfHeight, halfWidth, halfHeight);
                if (i < textures.Length) GUI.DrawTexture(part, textures[i]);
                else EditorGUI.DrawRect(part, Color.white);
            }
        }

        private static Texture2D[] GetTextures(string folderPath, List<string> paths)
        {
            if (FolderTextureCache.TryGetValue(folderPath, out var cached)) return cached;
            var result = new List<Texture2D>(paths.Count);
            foreach (string path in paths)
            {
                if (!TextureCache.TryGetValue(path, out var texture) || texture == null)
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null) TextureCache[path] = texture;
                }
                if (texture != null) result.Add(texture);
            }
            cached = result.ToArray();
            FolderTextureCache[folderPath] = cached;
            return cached;
        }
    }

    internal sealed class FolderIconIndexPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (ContainsIcon(imported) || ContainsIcon(deleted) || ContainsIcon(moved) || ContainsIcon(movedFrom))
                FolderIconDrawer.ScheduleIndexRebuild();
        }

        private static bool ContainsIcon(IEnumerable<string> paths)
        {
            foreach (string path in paths)
                if (string.Equals(Path.GetFileName(path), "FolderIcon.jpg", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
