// Copyright (c) 2025-2026 sakurayuki

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// アイテム検索対象のファイル拡張子を管理します。
    /// </summary>
    public static class AssetFileExtensionService
    {
        private const string CustomExtensionsPrefsKey = "UnityEditorAssetBrowser_CustomFileExtensions";
        private const string DefaultExtensionPrefsKeyPrefix = "UnityEditorAssetBrowser_FileExtension_";
        private const string ExtensionOrderPrefsKey = "UnityEditorAssetBrowser_FileExtensionOrder";

        private static readonly string[] DefaultExtensions =
        {
            ".txt",
            ".unitypackage",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".tif", ".tiff", ".webp", ".psd",
            ".fbx",
            ".blend"
        };

        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".tif", ".tiff", ".webp", ".psd"
        };

        private static bool _cacheValid;
        private static string[] _cachedCustomExtensions = Array.Empty<string>();
        private static string[] _cachedExtensionsInOrder = Array.Empty<string>();
        private static string[] _cachedSearchExtensions = Array.Empty<string>();
        private static Dictionary<string, int> _cachedExtensionOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, bool> _cachedExtensionEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static string _cachedConfigurationSignature = string.Empty;

        public static IReadOnlyList<string> GetDefaultExtensions() => DefaultExtensions;

        public static bool IsDefaultExtensionEnabled(string extension)
        {
            return IsExtensionEnabled(extension);
        }

        public static bool IsDefaultExtension(string extension)
        {
            string normalized = NormalizeExtension(extension);
            return DefaultExtensions.Contains(normalized, StringComparer.OrdinalIgnoreCase);
        }

        public static void SetDefaultExtensionEnabled(string extension, bool enabled)
        {
            SetExtensionEnabled(extension, enabled);
        }

        public static bool IsExtensionEnabled(string extension)
        {
            string normalized = NormalizeExtension(extension);
            if (string.IsNullOrEmpty(normalized)) return false;

            EnsureCache();
            return _cachedExtensionEnabled.TryGetValue(normalized, out bool enabled)
                ? enabled
                : EditorPrefs.GetBool(DefaultExtensionPrefsKeyPrefix + normalized, true);
        }

        public static void SetExtensionEnabled(string extension, bool enabled)
        {
            string normalized = NormalizeExtension(extension);
            if (string.IsNullOrEmpty(normalized)) return;
            EditorPrefs.SetBool(DefaultExtensionPrefsKeyPrefix + normalized, enabled);
            InvalidateCache();
        }

        public static IReadOnlyList<string> GetCustomExtensions()
        {
            EnsureCache();
            return _cachedCustomExtensions;
        }

        public static bool AddCustomExtension(string extension)
        {
            string normalized = NormalizeExtension(extension);
            if (string.IsNullOrEmpty(normalized) || string.Equals(normalized, ".unitypackage", StringComparison.OrdinalIgnoreCase))
                return false;

            var extensions = GetCustomExtensions().ToList();
            if (extensions.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
                return false;

            extensions.Add(normalized);
            SaveCustomExtensions(extensions);
            InvalidateCache();
            return true;
        }

        public static void RemoveCustomExtension(string extension)
        {
            string normalized = NormalizeExtension(extension);
            SaveCustomExtensions(GetCustomExtensions()
                .Where(x => !string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)));
            InvalidateCache();
        }

        public static IReadOnlyList<string> GetSearchExtensions()
        {
            EnsureCache();
            return _cachedSearchExtensions;
        }

        public static IReadOnlyList<string> GetExtensionsInOrder()
        {
            EnsureCache();
            return _cachedExtensionsInOrder;
        }

        public static int GetExtensionOrderIndex(string extension)
        {
            string normalized = NormalizeExtension(extension);
            EnsureCache();
            return _cachedExtensionOrder.TryGetValue(normalized, out int index)
                ? index
                : int.MaxValue;
        }

        public static bool MoveExtension(string extension, int direction)
        {
            var ordered = GetExtensionsInOrder().ToList();
            int index = ordered.FindIndex(x => string.Equals(x, NormalizeExtension(extension), StringComparison.OrdinalIgnoreCase));
            int newIndex = index + direction;
            if (index < 0 || newIndex < 0 || newIndex >= ordered.Count) return false;

            string moved = ordered[index];
            ordered[index] = ordered[newIndex];
            ordered[newIndex] = moved;
            SaveOrder(ordered);
            InvalidateCache();
            return true;
        }

        /// <summary>
        /// 設定変更後にファイル検索キャッシュを区別するための値を返します。
        /// </summary>
        public static string GetConfigurationSignature()
        {
            EnsureCache();
            return _cachedConfigurationSignature;
        }

        public static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return string.Empty;

            string normalized = extension.Trim();
            if (!normalized.StartsWith(".")) normalized = "." + normalized;

            if (normalized.Length <= 1 || normalized.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_'))
                return string.Empty;

            return normalized.ToLowerInvariant();
        }

        public static string GetFileTypeKey(string extension)
        {
            string normalized = NormalizeExtension(extension);
            if (string.Equals(normalized, ".unitypackage", StringComparison.OrdinalIgnoreCase))
                return "unitypackage";
            if (string.Equals(normalized, ".txt", StringComparison.OrdinalIgnoreCase))
                return "txt";
            if (ImageExtensions.Contains(normalized))
                return "image";
            if (string.Equals(normalized, ".fbx", StringComparison.OrdinalIgnoreCase))
                return "fbx";
            if (string.Equals(normalized, ".blend", StringComparison.OrdinalIgnoreCase))
                return "blend";

            return normalized.TrimStart('.');
        }

        private static void SaveCustomExtensions(IEnumerable<string> extensions)
        {
            EditorPrefs.SetString(
                CustomExtensionsPrefsKey,
                string.Join(";", extensions
                    .Select(NormalizeExtension)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)));
        }

        private static void EnsureCache()
        {
            if (_cacheValid) return;

            _cachedCustomExtensions = LoadCustomExtensions();

            var available = DefaultExtensions
                .Concat(_cachedCustomExtensions)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var availableSet = new HashSet<string>(available, StringComparer.OrdinalIgnoreCase);
            var ordered = new List<string>();
            var orderedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string extension in LoadSavedOrder())
            {
                if (availableSet.Contains(extension) && orderedSet.Add(extension))
                    ordered.Add(extension);
            }

            foreach (string extension in available)
            {
                if (orderedSet.Add(extension))
                    ordered.Add(extension);
            }

            _cachedExtensionsInOrder = ordered.ToArray();
            _cachedExtensionOrder = _cachedExtensionsInOrder
                .Select((extension, index) => new { extension, index })
                .ToDictionary(x => x.extension, x => x.index, StringComparer.OrdinalIgnoreCase);
            _cachedExtensionEnabled = _cachedExtensionsInOrder.ToDictionary(
                extension => extension,
                extension => EditorPrefs.GetBool(DefaultExtensionPrefsKeyPrefix + extension, true),
                StringComparer.OrdinalIgnoreCase);
            _cachedSearchExtensions = _cachedExtensionsInOrder
                .Where(extension => _cachedExtensionEnabled[extension])
                .ToArray();
            _cachedConfigurationSignature = string.Join(";", _cachedExtensionsInOrder
                .Select(extension => $"{extension}={_cachedExtensionEnabled[extension]}"));
            _cacheValid = true;
        }

        private static void InvalidateCache()
        {
            _cacheValid = false;
        }

        private static string[] LoadCustomExtensions()
        {
            return (EditorPrefs.GetString(CustomExtensionsPrefsKey, string.Empty) ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeExtension)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] LoadSavedOrder()
        {
            return (EditorPrefs.GetString(ExtensionOrderPrefsKey, string.Empty) ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeExtension)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void SaveOrder(IEnumerable<string> extensions)
        {
            EditorPrefs.SetString(
                ExtensionOrderPrefsKey,
                string.Join(";", extensions
                    .Select(NormalizeExtension)
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)));
        }
    }
}
