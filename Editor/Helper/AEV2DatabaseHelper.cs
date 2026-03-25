// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from AETools(https://github.com/puk06/AE-Tools)
// AETools is licensed under the MIT License. https://github.com/puk06/AE-Tools/blob/master/LICENSE.txt

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditorAssetBrowser.Models;
using UnityEditorAssetBrowser.Services;
using UnityEngine;

namespace UnityEditorAssetBrowser.Helper
{
    /// <summary>
    /// AEデータベース操作を支援するヘルパークラス
    /// AvatarExplorerのデータベースファイルの読み込み、保存、変換を行う
    /// </summary>
    public static class AEV2DatabaseHelper
    {
        /// <summary>
        /// AvatarExplorerのデータベースファイルを読み込む
        /// </summary>
        /// <param name="path">データベースのパス（ディレクトリまたはファイルパス）</param>
        /// <returns>読み込んだデータベース。読み込みに失敗した場合はnull</returns>
        public static AvatarExplorerDatabase? LoadAEDatabaseFile(string path)
        {
            DebugLogger.Log($"Starting to load AE database from: {path}");
            try
            {
                string jsonPath;

                // AEV2 は items.json のみを対象にする
                if (Directory.Exists(path))
                {
                    var v2Path = Path.Combine(path, "items.json");
                    if (File.Exists(v2Path))
                    {
                        jsonPath = v2Path;
                    }
                    else
                    {
                        DebugLogger.LogWarning($"AEV2 database file (items.json) not found in directory: {path}");
                        return null;
                    }
                }
                else
                {
                    DebugLogger.LogWarning($"AE database path is not a valid directory: {path}");
                    return null;
                }

                DebugLogger.Log($"Reading json file: {jsonPath}");
                var json = File.ReadAllText(jsonPath);

                // AEV2 は commonAvatars.json を使う
                var commonAvatarPath = Path.Combine(Path.GetDirectoryName(jsonPath) ?? string.Empty, "commonAvatars.json");

                var commonAvatarDefinitions = LoadCommonAvatarDefinitions(commonAvatarPath);

                // JSONシリアライザーの設定
                var settings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter> { new CustomDateTimeConverter() },
                };

                var v2Items = JsonConvert.DeserializeObject<AvatarExplorerV2Item[]>(json, settings);
                if (v2Items != null)
                {
                    DebugLogger.Log($"Loaded {v2Items.Length} items from AEV2 database.");
                    foreach (var item in v2Items)
                    {
                        item.SupportedAvatar = MergeSupportedAvatarsWithCommon(v2Items, item.SupportedAvatar, commonAvatarDefinitions);
                    }

                    var items = v2Items.Select(x => x.ToBaseModel()).ToArray();
                    return new AvatarExplorerDatabase(items);
                }

                DebugLogger.LogWarning("Deserialized items structure is null.");
                return null;
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Failed to load AE database: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 対応アバターのパスをアバター名に変換する
        /// </summary>
        /// <param name="items">全アイテムリスト</param>
        /// <param name="supportedAvatars">変換対象の対応アバターパス配列</param>
        /// <returns>変換後のアバター名配列</returns>
        private static string[] ConvertSupportedAvatarPaths(AvatarExplorerV2Item[] items, string[] supportedAvatars)
        {
            var supportedAvatarNames = new List<string>();

            foreach (var avatar in supportedAvatars)
            {
                var avatarData = items.FirstOrDefault(x => x.ItemPath == avatar);
                if (avatarData != null) supportedAvatarNames.Add(avatarData.Title);
            }

            return supportedAvatarNames.ToArray();
        }

        /// <summary>
        /// アバターパスからタイトルを取得する（既存のパス→タイトル変換を再利用し、見つからない場合はパスでフォールバック）
        /// </summary>
        private static string GetAvatarTitle(AvatarExplorerV2Item[] items, string avatarPath)
        {
            // 単一要素で既存変換を利用
            var converted = ConvertSupportedAvatarPaths(items, new[] { avatarPath });
            if (converted.Length > 0 && !string.IsNullOrEmpty(converted[0]))
            {
                return converted[0];
            }

            // 念のため直接検索も行う
            var avatarData = items.FirstOrDefault(x => string.Equals(x.ItemPath, avatarPath, StringComparison.OrdinalIgnoreCase));
            if (avatarData != null && !string.IsNullOrEmpty(avatarData.Title))
            {
                return avatarData.Title;
            }

            // 最後のフォールバックはパス
            return avatarPath;
        }

        /// <summary>
        /// CommonAvatar 定義を考慮して SupportedAvatar をまとめる
        /// </summary>
        private static string[] MergeSupportedAvatarsWithCommon(
            AvatarExplorerV2Item[] items,
            string[] supportedAvatars,
            IReadOnlyList<CommonAvatarV2Definition> commonDefinitions)
        {
            // CommonAvatar が無ければ既存処理で終了
            if (commonDefinitions == null || commonDefinitions.Count == 0)
            {
                return ConvertSupportedAvatarPaths(items, supportedAvatars);
            }

            // パス→タイトルのマップ（重複パスは先勝ち）。
            var titleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in supportedAvatars)
            {
                if (titleMap.ContainsKey(path)) continue;
                var avatarData = items.FirstOrDefault(x => x.ItemPath == path);
                if (avatarData != null) titleMap[path] = avatarData.Title;
            }

            var remainingPaths = new HashSet<string>(supportedAvatars, StringComparer.OrdinalIgnoreCase);
            var merged = new List<string>();

            // CommonAvatar を優先的にまとめる（1つでも含まれれば、定義内の全アバター名でまとめる）
            foreach (var definition in commonDefinitions)
            {
                if (definition.Avatars == null || definition.Avatars.Count == 0) continue;

                // SupportedAvatar に一つでも含まれるかを判定
                bool hasAny = definition.Avatars.Any(p => remainingPaths.Contains(p));
                if (!hasAny) continue;

                // 定義内すべてのアバター名を並べる（ConvertSupportedAvatarPaths を使ってパス→名前変換）
                var titles = new List<string>();
                foreach (var avatarPath in definition.Avatars)
                {
                    var title = GetAvatarTitle(items, avatarPath);
                    titles.Add(title);
                }

                // まとめる対象のパスを残余から除外（重複表示を防ぐ）
                foreach (var avatarPath in definition.Avatars)
                {
                    if (remainingPaths.Contains(avatarPath))
                    {
                        remainingPaths.Remove(avatarPath);
                    }
                }

                merged.Add($"{definition.Name}({string.Join(",", titles)})");
            }

            // CommonAvatar にまとめられなかったものを個別追加（元の順序を尊重）
            foreach (var path in supportedAvatars)
            {
                if (!remainingPaths.Contains(path)) continue;
                if (titleMap.TryGetValue(path, out var title))
                {
                    merged.Add(title);
                }
            }

            return merged.ToArray();
        }

        /// <summary>
        /// CommonAvatar.json を読み込む（存在しない場合は空リスト）
        /// </summary>
        private static IReadOnlyList<CommonAvatarV2Definition> LoadCommonAvatarDefinitions(string commonAvatarPath)
        {
            if (string.IsNullOrEmpty(commonAvatarPath) || !File.Exists(commonAvatarPath))
            {
                DebugLogger.Log("CommonAvatar.json not found. Skipping CommonAvatar aggregation.");
                return Array.Empty<CommonAvatarV2Definition>();
            }

            try
            {
                var json = File.ReadAllText(commonAvatarPath);
                var data = JsonConvert.DeserializeObject<List<CommonAvatarV2Definition>>(json);
                return data ?? new List<CommonAvatarV2Definition>();
            }
            catch (Exception ex)
            {
                DebugLogger.LogWarning($"Failed to load CommonAvatar definitions: {ex.Message}");
                return Array.Empty<CommonAvatarV2Definition>();
            }
        }
    }
}
