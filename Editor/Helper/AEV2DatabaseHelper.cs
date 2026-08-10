// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from AETools(https://github.com/puk06/AE-Tools)
// AETools is licensed under the MIT License. https://github.com/puk06/AE-Tools/blob/master/LICENSE.txt

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditorAssetBrowser.Models;
using UnityEditorAssetBrowser.Services;

namespace UnityEditorAssetBrowser.Helper
{
    /// <summary>
    /// AEデータベースのバージョンが対応範囲外であることを表す例外
    /// </summary>
    public sealed class AEV2DatabaseVersionMismatchException : Exception
    {
        public AEV2DatabaseVersionMismatchException()
            : base(AEV2DatabaseHelper.VersionMismatchMessage)
        {
        }
    }

    /// <summary>
    /// AvatarExplorer V2のデータベース読み込みを支援するヘルパークラス
    /// </summary>
    public static class AEV2DatabaseHelper
    {
        public const string VersionMismatchMessage = "ゆにあせとAvatarExplorerを最新版にアップデートしてください";

        private const int SupportedItemsVersion = 3;
        private const int SupportedCommonAvatarVersion = 1;
        private const int SupportedTempAvatarVersion = 0;

        private const string ItemPrefix = "item:";
        private const string TempAvatarPrefix = "tempavatar:";
        private const string CommonAvatarPrefix = "commonavatar:";

        /// <summary>
        /// AvatarExplorer V2のデータベースファイルを読み込む
        /// </summary>
        /// <param name="path">データベースディレクトリ</param>
        /// <returns>読み込んだデータベース。読み込みに失敗した場合はnull</returns>
        public static AvatarExplorerDatabase? LoadAEDatabaseFile(string path)
        {
            DebugLogger.Log($"Starting to load AE database from: {path}");

            try
            {
                if (!Directory.Exists(path))
                {
                    DebugLogger.LogWarning($"AE database path is not a valid directory: {path}");
                    return null;
                }

                var dataDir = path;
                var itemsPath = Path.Combine(dataDir, "items.json");
                if (!File.Exists(itemsPath))
                {
                    DebugLogger.LogWarning($"AEV2 database file (items.json) not found in directory: {path}");
                    return null;
                }

                var settings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter> { new CustomDateTimeConverter() },
                };

                var v2Database = ReadVersionedDatabase<AvatarExplorerV2Database>(
                    itemsPath,
                    SupportedItemsVersion,
                    settings
                );

                var commonAvatarDefinitions = LoadCommonAvatarDefinitions(
                    Path.Combine(dataDir, "commonAvatars.json"),
                    settings
                );
                var tempAvatarDefinitions = LoadTempAvatarDefinitions(
                    Path.Combine(dataDir, "tempAvatars.json"),
                    settings
                );

                var v2Items = (v2Database.Items ?? new List<AvatarExplorerV2Item>()).ToArray();
                DebugLogger.Log($"Loaded {v2Items.Length} items from AEV2 database.");

                foreach (var item in v2Items)
                {
                    item.SupportedAvatars = MergeSupportedAvatarsWithCommon(
                        v2Items,
                        item.SupportedAvatars ?? Array.Empty<string>(),
                        commonAvatarDefinitions,
                        tempAvatarDefinitions
                    );
                }

                return new AvatarExplorerDatabase(v2Items.Select(x => x.ToBaseModel()).ToArray());
            }
            catch (AEV2DatabaseVersionMismatchException)
            {
                throw;
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Failed to load AE database: {ex.Message}");
                return null;
            }
        }

        private static T ReadVersionedDatabase<T>(
            string filePath,
            int supportedVersion,
            JsonSerializerSettings settings
        ) where T : class
        {
            var root = JToken.Parse(File.ReadAllText(filePath)) as JObject;
            if (root == null || root["Items"] is not JArray)
                throw new AEV2DatabaseVersionMismatchException();

            var version = root["Version"];
            if (version == null || version.Type != JTokenType.Integer || version.Value<int>() != supportedVersion)
                throw new AEV2DatabaseVersionMismatchException();

            var database = root.ToObject<T>(JsonSerializer.Create(settings));
            if (database == null)
                throw new AEV2DatabaseVersionMismatchException();

            return database;
        }

        private static string[] ConvertSupportedAvatarIds(
            AvatarExplorerV2Item[] items,
            string[] supportedAvatarReferences,
            IReadOnlyList<TempAvatarV2Definition> tempAvatars
        )
        {
            var supportedAvatarNames = new List<string>();

            foreach (var avatarReference in supportedAvatarReferences)
            {
                if (TryGetAvatarTitle(items, tempAvatars, avatarReference, out var title))
                    supportedAvatarNames.Add(title);
            }

            return supportedAvatarNames.ToArray();
        }

        private static bool TryGetAvatarTitle(
            AvatarExplorerV2Item[] items,
            IReadOnlyList<TempAvatarV2Definition> tempAvatars,
            string avatarReference,
            out string title
        )
        {
            title = "";

            if (avatarReference.StartsWith(TempAvatarPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var tempId = avatarReference[TempAvatarPrefix.Length..];
                var tempData = tempAvatars.FirstOrDefault(x =>
                    string.Equals(x.Id, tempId, StringComparison.OrdinalIgnoreCase)
                );
                if (tempData != null && !string.IsNullOrEmpty(tempData.AvatarName))
                {
                    title = tempData.AvatarName;
                    return true;
                }

                return false;
            }

            if (!avatarReference.StartsWith(ItemPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            var itemId = avatarReference[ItemPrefix.Length..];
            var avatarData = items.FirstOrDefault(x =>
                string.Equals(x.Id, itemId, StringComparison.OrdinalIgnoreCase)
            );
            if (avatarData != null && !string.IsNullOrEmpty(avatarData.Title))
            {
                title = avatarData.Title;
                return true;
            }

            return false;
        }

        private static string[] MergeSupportedAvatarsWithCommon(
            AvatarExplorerV2Item[] items,
            string[] supportedAvatars,
            IReadOnlyList<CommonAvatarV2Definition> commonDefinitions,
            IReadOnlyList<TempAvatarV2Definition> tempAvatars
        )
        {
            if (commonDefinitions.Count == 0)
                return ConvertSupportedAvatarIds(items, supportedAvatars, tempAvatars);

            var merged = new List<string>();
            var emittedCommonAvatarIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var avatarReference in supportedAvatars)
            {
                if (avatarReference.StartsWith(CommonAvatarPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var commonAvatarId = avatarReference[CommonAvatarPrefix.Length..];
                    if (!emittedCommonAvatarIds.Add(commonAvatarId))
                        continue;

                    var definition = commonDefinitions.FirstOrDefault(x =>
                        string.Equals(x.Id, commonAvatarId, StringComparison.OrdinalIgnoreCase)
                    );
                    if (definition == null || definition.Avatars == null || definition.Avatars.Count == 0)
                        continue;

                    var titles = new List<string>();
                    foreach (var avatarId in definition.Avatars)
                    {
                        if (TryGetAvatarTitle(items, tempAvatars, avatarId, out var title))
                            titles.Add(title);
                    }

                    if (titles.Count > 0)
                        merged.Add($"{definition.Name}({string.Join(",", titles)})");

                    continue;
                }

                if (TryGetAvatarTitle(items, tempAvatars, avatarReference, out var avatarTitle))
                    merged.Add(avatarTitle);
            }

            return merged.ToArray();
        }

        private static IReadOnlyList<TempAvatarV2Definition> LoadTempAvatarDefinitions(
            string tempAvatarPath,
            JsonSerializerSettings settings
        )
        {
            if (!File.Exists(tempAvatarPath))
            {
                DebugLogger.Log("tempAvatars.json not found. Skipping temp avatar resolution.");
                return Array.Empty<TempAvatarV2Definition>();
            }

            var database = ReadVersionedDatabase<TempAvatarV2Database>(
                tempAvatarPath,
                SupportedTempAvatarVersion,
                settings
            );
            return database.Items ?? new List<TempAvatarV2Definition>();
        }

        private static IReadOnlyList<CommonAvatarV2Definition> LoadCommonAvatarDefinitions(
            string commonAvatarPath,
            JsonSerializerSettings settings
        )
        {
            if (!File.Exists(commonAvatarPath))
            {
                DebugLogger.Log("commonAvatars.json not found. Skipping Common Avatar aggregation.");
                return Array.Empty<CommonAvatarV2Definition>();
            }

            var database = ReadVersionedDatabase<CommonAvatarV2Database>(
                commonAvatarPath,
                SupportedCommonAvatarVersion,
                settings
            );
            return database.Items ?? new List<CommonAvatarV2Definition>();
        }
    }
}
