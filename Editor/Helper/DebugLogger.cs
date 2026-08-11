// Copyright (c) 2025-2026 sakurayuki

using UnityEditor;
using UnityEngine;

namespace UnityEditorAssetBrowser.Helper
{
    public static class DebugLogger
    {
        private const string DEBUG_KEY = "UniAsset_DebugMode";
        private static bool? _isDebugMode;

        public static bool IsDebugMode => _isDebugMode ??= EditorPrefs.GetBool(DEBUG_KEY, false);

        public static void SetDebugMode(bool enabled)
        {
            _isDebugMode = enabled;
            EditorPrefs.SetBool(DEBUG_KEY, enabled);
        }

        public static void Log(string message)
        {
            if (IsDebugMode)
            {
                Debug.Log($"[UniAsset][Debug] {message}");
            }
        }
        
        public static void LogWarning(string message)
        {
             if (IsDebugMode)
            {
                Debug.LogWarning($"[UniAsset][Debug] {message}");
            }
        }

        public static void LogError(string message)
        {
             if (IsDebugMode)
            {
                Debug.LogError($"[UniAsset][Debug] {message}");
            }
        }
    }
}
