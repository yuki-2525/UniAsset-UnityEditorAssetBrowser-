// Copyright (c) 2025-2026 sakurayuki

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// 除外フォルダ判定の共通サービス
    /// </summary>
    public static class ExcludeFolderService
    {
        private const string PREFS_KEY_EXCLUDE_FOLDERS = "UnityEditorAssetBrowser_ExcludeFolders";
        private const string PREFS_KEY_EXCLUDE_FOLDERS_COMBINED = "UnityEditorAssetBrowser_ExcludeFolders_Combined";
        private static readonly char[] RegexCharacters = ".*+?^${}()|[]\\".ToCharArray();
        private static readonly HashSet<string> ExactPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<Regex> RegexPatterns = new List<Regex>();
        private static List<string> _combinedPatterns = new List<string>();
        private static bool _cacheInitialized;

        // デフォルト除外フォルダ（s?付き正規表現）
        private static readonly List<string> DefaultExcludePatterns = new List<string>
        {
            "Assets?",
            "Packages?",
            "Animes?",
            "Animations?",
            "AnimationControllers?",
            "Animators?",
            "Commons?",
            "Data",
            "Expressions?",
            "Fbxs?",
            "Mats?",
            "Matcaps?",
            "Materials?",
            "Motions?",
            "Partcles?",
            "Prefabs?",
            "Shaders?",
            "Texs?",
            "Textures?",
        };

        /// <summary>
        /// 除外フォルダかどうか判定（正規表現でなければ完全一致、大文字小文字区別なし）
        /// </summary>
        public static bool IsExcludedFolder(string folderName)
        {
            EnsurePatternCache();
            string normalizedFolderName = Normalize(folderName);

            if (ExactPatterns.Contains(normalizedFolderName)) return true;
            foreach (var regex in RegexPatterns)
                if (regex.IsMatch(normalizedFolderName)) return true;

            return false;
        }

        /// <summary>
        /// 除外判定用の合成リスト（ユーザー追加分 + ONの初期設定分）
        /// </summary>
        public static List<string> GetExcludeFolderPatterns()
        {
            var result = new List<string>();

            var prefs = LoadPrefs();
            if (prefs != null)
            {
                result.AddRange(prefs.userFolders);
                result.AddRange(prefs.enabledDefaults);
            }

            return result;
        }

        /// <summary>
        /// 正規表現かどうかを判定する（特殊文字が含まれていれば正規表現とみなす）
        /// </summary>
        public static bool IsRegexPattern(string pattern)
            => pattern.IndexOfAny(RegexCharacters) >= 0;

        /// <summary>
        /// 除外判定用に空白・全角空白・アンダースコアを除去
        /// </summary>
        private static string Normalize(string s)
        {
            if (s == null) return "";
            return s.Replace(" ", "").Replace("　", "").Replace("_", "");
        }

        /// <summary>
        /// 除外フォルダの初期設定リスト（s?付き正規表現、abc順でUIに表示）を取得
        /// </summary>
        public static List<string> GetAllDefaultExcludeFolders()
            => DefaultExcludePatterns;

        /// <summary>
        /// 除外フォルダ設定を初期化（EditorPrefsに値がなければデフォルト値を登録）
        /// </summary>
        public static void InitializeDefaultExcludeFolders()
        {
            // DebugLogger.Log("Initializing Default Exclude Folders"); // 頻繁に呼び出される可能性があるためコメントアウト
            string excludeFoldersJson = EditorPrefs.GetString(PREFS_KEY_EXCLUDE_FOLDERS, "");
            if (string.IsNullOrEmpty(excludeFoldersJson))
            {
                DebugLogger.Log("First time setup for exclude folders.");
                string json = JsonUtility.ToJson(new ExcludeFoldersPrefsData
                {
                    userFolders = new List<string>(),
                    enabledDefaults = new List<string>(DefaultExcludePatterns)
                });

                EditorPrefs.SetString(PREFS_KEY_EXCLUDE_FOLDERS, json);
            }
            else
            {
                // マイグレーション: 新しいデフォルト項目があればONで追加、不要な項目は削除
                var data = JsonUtility.FromJson<ExcludeFoldersPrefsData>(excludeFoldersJson);

                bool updated = false;
                foreach (var def in DefaultExcludePatterns)
                {
                    if (data.enabledDefaults.Contains(def)) continue;

                    data.enabledDefaults.Add(def);
                    updated = true;
                }

                var toRemove = data.enabledDefaults
                    .Where(x => !DefaultExcludePatterns.Contains(x))
                    .ToList();

                foreach (var rem in toRemove)
                {
                    data.enabledDefaults.Remove(rem);
                    updated = true;
                }

                if (updated)
                {
                    string json = JsonUtility.ToJson(data);
                    EditorPrefs.SetString(PREFS_KEY_EXCLUDE_FOLDERS, json);
                }
            }

            InvalidateCache();
        }

        /// <summary>
        /// 除外フォルダ設定データ（ユーザー追加分・ONの初期設定分）をEditorPrefsから読み込む
        /// </summary>
        public static ExcludeFoldersPrefsData LoadPrefs()
        {
            string json = EditorPrefs.GetString(PREFS_KEY_EXCLUDE_FOLDERS, "");
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                var data = JsonUtility.FromJson<ExcludeFoldersPrefsData>(json);
                if (data != null && (data.enabledDefaults == null || data.enabledDefaults.Count == 0))
                {
                    data.enabledDefaults = new List<string>(DefaultExcludePatterns);
                }

                return data;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 除外フォルダ設定データ（ユーザー追加分・ONの初期設定分）をEditorPrefsに保存する
        /// </summary>
        public static void SaveExcludeFolders(
            List<string> userFolders,
            List<string> enabledDefaults
        )
        {
            DebugLogger.Log($"Saving exclude folders. User: {userFolders.Count}, Default: {enabledDefaults.Count}");
            string jsonString = JsonUtility.ToJson(new ExcludeFoldersPrefsData
            {
                userFolders = userFolders,
                enabledDefaults = enabledDefaults
            });

            EditorPrefs.SetString(PREFS_KEY_EXCLUDE_FOLDERS, jsonString);
            RebuildPatternCache(userFolders.Concat(enabledDefaults));
        }

        /// <summary>
        /// 判定用の合成済みリスト（ユーザー追加分 + ONの初期設定分）を保存
        /// </summary>
        public static void SaveCombinedExcludePatterns(List<string> combined)
        {DebugLogger.Log($"Saving combined exclude patterns. Count: {combined.Count}");
            
            string json = JsonUtility.ToJson(new StringListWrapper { list = combined });
            EditorPrefs.SetString(PREFS_KEY_EXCLUDE_FOLDERS_COMBINED, json);
            RebuildPatternCache(combined);
        }

        /// <summary>
        /// 判定用の合成済みリストを取得
        /// </summary>
        public static List<string> GetCombinedExcludePatterns()
        {
            EnsurePatternCache();
            return new List<string>(_combinedPatterns);
        }

        public static void InvalidateCache()
        {
            _cacheInitialized = false;
            ExactPatterns.Clear();
            RegexPatterns.Clear();
            _combinedPatterns.Clear();
        }

        private static void EnsurePatternCache()
        {
            if (_cacheInitialized) return;

            string json = EditorPrefs.GetString(PREFS_KEY_EXCLUDE_FOLDERS_COMBINED, "");
            if (string.IsNullOrEmpty(json))
            {
                RebuildPatternCache(Array.Empty<string>());
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<StringListWrapper>(json);
                RebuildPatternCache(data?.list ?? new List<string>());
            }
            catch
            {
                RebuildPatternCache(Array.Empty<string>());
            }
        }

        private static void RebuildPatternCache(IEnumerable<string> patterns)
        {
            ExactPatterns.Clear();
            RegexPatterns.Clear();
            _combinedPatterns = patterns.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var pattern in _combinedPatterns)
            {
                string normalized = Normalize(pattern);
                if (string.IsNullOrEmpty(normalized)) continue;

                if (!IsRegexPattern(normalized))
                {
                    ExactPatterns.Add(normalized);
                    continue;
                }

                try
                {
                    RegexPatterns.Add(new Regex(
                        normalized,
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled));
                }
                catch (ArgumentException)
                {
                    // 無効な正規表現は従来どおり無視する。
                }
            }

            _cacheInitialized = true;
        }

        [Serializable]
        public class ExcludeFoldersPrefsData
        {
            public List<string> userFolders = new List<string>();
            public List<string> enabledDefaults = new List<string>();
        }

        [Serializable]
        private class StringListWrapper
        {
            public List<string> list = new List<string>();
        }
    }
}
