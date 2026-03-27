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
        private const string TempAvatarPrefix = "<sys:temp>";
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

                var dataDir = Path.GetDirectoryName(jsonPath) ?? string.Empty;

                // AEV2 は commonAvatars.json を使う
                var commonAvatarPath = Path.Combine(dataDir, "commonAvatars.json");
                var commonAvatarDefinitions = LoadCommonAvatarDefinitions(commonAvatarPath);

                // tempAvatars.json を読み込む
                var tempAvatarPath = Path.Combine(dataDir, "tempAvatars.json");
                var tempAvatarDefinitions = LoadTempAvatarDefinitions(tempAvatarPath);

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
                        item.SupportedAvatars = MergeSupportedAvatarsWithCommon(v2Items, item.SupportedAvatars, commonAvatarDefinitions, tempAvatarDefinitions);
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
        /// 対応アバターのIDをアバター名に変換する
        /// </summary>
        private static string[] ConvertSupportedAvatarIds(AvatarExplorerV2Item[] items, string[] supportedAvatarIds, IReadOnlyList<TempAvatarV2Definition> tempAvatars)
        {
            var supportedAvatarNames = new List<string>();

            foreach (var avatarId in supportedAvatarIds)
            {
                var title = GetAvatarTitle(items, tempAvatars, avatarId);
                // items にも tempAvatars にも見つからない場合はスキップ（IDのまま出力しない）
                if (title != avatarId) supportedAvatarNames.Add(title);
            }

            return supportedAvatarNames.ToArray();
        }

        /// <summary>
        /// アバターIDからタイトルを取得する。
        /// "&lt;sys:temp&gt;{Id}" 形式の場合は tempAvatars から、それ以外は items から検索する。
        /// 見つからない場合はIDをそのまま返す。
        /// </summary>
        private static string GetAvatarTitle(AvatarExplorerV2Item[] items, IReadOnlyList<TempAvatarV2Definition> tempAvatars, string avatarId)
        {
            if (avatarId.StartsWith(TempAvatarPrefix))
            {
                var tempId = avatarId[TempAvatarPrefix.Length..];
                var tempData = tempAvatars.FirstOrDefault(x => x.Id == tempId);
                if (tempData != null && !string.IsNullOrEmpty(tempData.AvatarName))
                    return tempData.AvatarName;
                return avatarId;
            }

            var avatarData = items.FirstOrDefault(x => x.Id == avatarId);
            if (avatarData != null && !string.IsNullOrEmpty(avatarData.Title))
                return avatarData.Title;

            return avatarId;
        }

        /// <summary>
        /// CommonAvatar 定義を考慮して SupportedAvatar をまとめる
        /// </summary>
        private static string[] MergeSupportedAvatarsWithCommon(
            AvatarExplorerV2Item[] items,
            string[] supportedAvatars,
            IReadOnlyList<CommonAvatarV2Definition> commonDefinitions,
            IReadOnlyList<TempAvatarV2Definition> tempAvatars)
        {
            // CommonAvatar が無ければ既存処理で終了
            if (commonDefinitions == null || commonDefinitions.Count == 0)
            {
                return ConvertSupportedAvatarIds(items, supportedAvatars, tempAvatars);
            }

            // ID→タイトルのマップ（重複IDは先勝ち）
            var titleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in supportedAvatars)
            {
                if (titleMap.ContainsKey(id)) continue;
                var title = GetAvatarTitle(items, tempAvatars, id);
                if (title != id) titleMap[id] = title;
            }

            var remainingIds = new HashSet<string>(supportedAvatars, StringComparer.OrdinalIgnoreCase);
            var merged = new List<string>();

            // CommonAvatar を優先的にまとめる（1つでも含まれれば、定義内の全アバター名でまとめる）
            foreach (var definition in commonDefinitions)
            {
                if (definition.Avatars == null || definition.Avatars.Count == 0) continue;

                // SupportedAvatar に一つでも含まれるかを判定
                bool hasAny = definition.Avatars.Any(p => remainingIds.Contains(p));
                if (!hasAny) continue;

                // 定義内すべてのアバター名を並べる
                var titles = new List<string>();
                foreach (var avatarId in definition.Avatars)
                {
                    titles.Add(GetAvatarTitle(items, tempAvatars, avatarId));
                }

                // まとめる対象のIDを残余から除外（重複表示を防ぐ）
                foreach (var avatarId in definition.Avatars)
                {
                    remainingIds.Remove(avatarId);
                }

                merged.Add($"{definition.Name}({string.Join(",", titles)})");
            }

            // CommonAvatar にまとめられなかったものを個別追加（元の順序を尊重）
            foreach (var id in supportedAvatars)
            {
                if (!remainingIds.Contains(id)) continue;
                if (titleMap.TryGetValue(id, out var title))
                {
                    merged.Add(title);
                }
            }

            return merged.ToArray();
        }

        /// <summary>
        /// tempAvatars.json を読み込む（存在しない場合は空リスト）
        /// </summary>
        private static IReadOnlyList<TempAvatarV2Definition> LoadTempAvatarDefinitions(string tempAvatarPath)
        {
            if (string.IsNullOrEmpty(tempAvatarPath) || !File.Exists(tempAvatarPath))
            {
                DebugLogger.Log("tempAvatars.json not found. Skipping temp avatar resolution.");
                return Array.Empty<TempAvatarV2Definition>();
            }

            try
            {
                var json = File.ReadAllText(tempAvatarPath);
                var data = JsonConvert.DeserializeObject<List<TempAvatarV2Definition>>(json);
                return data ?? new List<TempAvatarV2Definition>();
            }
            catch (Exception ex)
            {
                DebugLogger.LogWarning($"Failed to load temp avatar definitions: {ex.Message}");
                return Array.Empty<TempAvatarV2Definition>();
            }
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
