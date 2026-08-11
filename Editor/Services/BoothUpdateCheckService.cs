// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Helper;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// BOOTH商品のバリエーション更新を確認します。
    /// </summary>
    public static class BoothUpdateCheckService
    {
        private const string ApiUrlFormat = "https://api.booth.pm/vroid/items/{0}";
        private const string SnapshotPrefsKeyFormat = "UnityEditorAssetBrowser_BoothVariationSnapshot_{0}";

        private static readonly Dictionary<int, Task<bool>> InflightChecks = new Dictionary<int, Task<bool>>();
        private static readonly HashSet<int> ApprovedBoothIds = new HashSet<int>();

        private sealed class VariationSnapshot
        {
            public Dictionary<string, string> Variations { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, string> VariationNames { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, DateTimeOffset?> VariationUpdatedAt { get; set; } =
                new Dictionary<string, DateTimeOffset?>();
            public DateTimeOffset? UpdatedAt { get; set; }
        }

        private sealed class UpdateCheckResult
        {
            public VariationSnapshot Snapshot { get; set; } = new VariationSnapshot();
            public List<string> UpdatedVariationIds { get; set; } = new List<string>();
        }

        /// <summary>
        /// 更新確認後にインポート処理を実行します。
        /// 通信に失敗した場合は、通常どおりインポートを継続します。
        /// </summary>
        public static void CheckBeforeImport(IDatabaseItem? item, Action? importAction, Action? skipAction = null)
            => _ = CheckBeforeImportAsync(item, importAction, skipAction);

        public static async Task CheckBeforeImportAsync(IDatabaseItem? item, Action? importAction, Action? skipAction = null)
        {
            if (item == null || importAction == null || item.GetBoothId() <= 0)
            {
                DebugLogger.Log(
                    $"BOOTH update check skipped: item={(item == null ? "null" : item.GetTitle())}, " +
                    $"boothId={(item?.GetBoothId() ?? -1)}, hasImportAction={importAction != null}");
                importAction?.Invoke();
                return;
            }

            int boothId = item.GetBoothId();
            DebugLogger.Log($"BOOTH update check requested: boothId={boothId}, item={item.GetTitle()}");
            if (ApprovedBoothIds.Contains(boothId))
            {
                DebugLogger.Log($"BOOTH update check reused session approval: boothId={boothId}");
                importAction();
                return;
            }

            bool ownsCheck = !InflightChecks.TryGetValue(boothId, out var checkTask);
            if (ownsCheck)
            {
                DebugLogger.Log($"BOOTH update check started: boothId={boothId}");
                checkTask = PerformUpdateCheckAsync(boothId, item);
                InflightChecks[boothId] = checkTask;
            }
            else
            {
                DebugLogger.Log($"BOOTH update check joined in-flight request: boothId={boothId}");
            }

            try
            {
                bool shouldImport = await checkTask;
                DebugLogger.Log($"BOOTH update check completed: boothId={boothId}, import={shouldImport}");
                if (shouldImport) importAction();
                else skipAction?.Invoke();
            }
            finally
            {
                if (ownsCheck && InflightChecks.TryGetValue(boothId, out var current) &&
                    ReferenceEquals(current, checkTask))
                    InflightChecks.Remove(boothId);
            }
        }

        private static async Task<bool> PerformUpdateCheckAsync(int boothId, IDatabaseItem item)
        {
            try
            {
                var remoteResult = await FetchUpdateCheckResult(boothId, item);
                if (remoteResult == null)
                {
                    DebugLogger.Log($"BOOTH update check unavailable; continuing import: boothId={boothId}");
                    return true;
                }

                if (remoteResult.UpdatedVariationIds.Count == 0)
                {
                    DebugLogger.Log($"BOOTH update check found no changes: boothId={boothId}");
                    SaveSnapshot(boothId, remoteResult.Snapshot);
                    ApprovedBoothIds.Add(boothId);
                    return true;
                }

                DebugLogger.Log(
                    $"BOOTH update check found possible changes: boothId={boothId}, " +
                    $"variationIds={string.Join(",", remoteResult.UpdatedVariationIds)}");

                string variationText = string.Join("\n", remoteResult.UpdatedVariationIds.Select(id =>
                    $"・{GetVariationDisplayName(remoteResult.Snapshot, id)}"));
                int result = EditorUtility.DisplayDialogComplex(
                    LocalizationService.Instance.GetString("booth_update_available_title"),
                    string.Format(
                        LocalizationService.Instance.GetString("booth_update_available_message"),
                        variationText),
                    LocalizationService.Instance.GetString("yes"),
                    LocalizationService.Instance.GetString("cancel"),
                    LocalizationService.Instance.GetString("open_product_page"));

                if (result == 0)
                {
                    DebugLogger.Log($"BOOTH update dialog selected import: boothId={boothId}");
                    SaveSnapshot(boothId, remoteResult.Snapshot);
                    ApprovedBoothIds.Add(boothId);
                    return true;
                }

                if (result == 2)
                {
                    DebugLogger.Log($"BOOTH update dialog selected product page: boothId={boothId}");
                    Application.OpenURL($"https://booth.pm/ja/items/{boothId}");
                }
                else
                {
                    DebugLogger.Log($"BOOTH update dialog cancelled import: boothId={boothId}");
                }
                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.LogWarning($"Failed to check BOOTH item {boothId}: {ex.Message}");
                return true;
            }
        }

        private static async Task<UpdateCheckResult?> FetchUpdateCheckResult(int boothId, IDatabaseItem item)
        {
            DebugLogger.Log($"BOOTH API request sending: boothId={boothId}");
            using (var request = UnityWebRequest.Get(string.Format(ApiUrlFormat, boothId)))
            {
                request.timeout = 10;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    DebugLogger.LogWarning($"BOOTH API request failed for {boothId}: {request.error}");
                    return null;
                }

                DebugLogger.Log($"BOOTH API request succeeded: boothId={boothId}, status={request.responseCode}");

                var root = JToken.Parse(request.downloadHandler.text);
                var remoteSnapshot = ParseSnapshot(root);
                if (remoteSnapshot.Variations.Count == 0)
                {
                    DebugLogger.LogWarning($"BOOTH API response contained no comparable variations: boothId={boothId}");
                    return null;
                }

                var savedSnapshot = LoadSnapshot(boothId);
                DebugLogger.Log(
                    $"BOOTH variation comparison: boothId={boothId}, remoteCount={remoteSnapshot.Variations.Count}, " +
                    $"mode={(savedSnapshot == null ? "initial-date" : "saved-hash")}, " +
                    $"localDate={FormatDate(GetInitialComparisonDate(item))}, remoteLatest={FormatDate(remoteSnapshot.UpdatedAt)}");
                var updatedVariationIds = savedSnapshot != null
                    ? CompareVariationHashes(savedSnapshot, remoteSnapshot)
                    : CompareInitialDate(item, remoteSnapshot);

                return new UpdateCheckResult
                {
                    Snapshot = remoteSnapshot,
                    UpdatedVariationIds = updatedVariationIds
                };
            }
        }

        private static VariationSnapshot ParseSnapshot(JToken root)
        {
            var snapshot = new VariationSnapshot
            {
                UpdatedAt = ParseDate(GetProperty(root, "updated_at", "updatedAt", "UpdatedDate"))
            };

            var variations = GetProperty(root, "Variations", "variations") ??
                GetProperty(GetProperty(root, "item", "Item") ?? JValue.CreateNull(), "Variations", "variations") ??
                GetProperty(GetProperty(root, "data", "Data") ?? JValue.CreateNull(), "Variations", "variations");

            if (variations is not JArray variationArray) return snapshot;

            foreach (var variation in variationArray.OfType<JObject>())
            {
                var idToken = GetProperty(variation, "VariationId", "variationId", "id", "Id");
                var hashToken = GetProperty(variation, "hash", "Hash");
                if (idToken == null) continue;

                string variationId = GetTokenText(idToken);
                string hash = hashToken == null
                    ? ComputeVariationHash(variation)
                    : GetTokenText(hashToken);
                if (string.IsNullOrEmpty(variationId) || string.IsNullOrEmpty(hash)) continue;

                snapshot.Variations[variationId] = hash;
                string variationName = GetTokenText(GetProperty(variation, "VariationName", "variationName", "name", "Name") ?? idToken);
                snapshot.VariationNames[variationId] = string.IsNullOrEmpty(variationName) ? variationId : variationName;
                var variationDate = ParseDate(GetProperty(
                    variation,
                    "updated_at", "updatedAt", "UpdatedDate",
                    "created_at", "createdAt", "CreatedDate"));

                var downloadables = GetProperty(variation, "downloadables", "Downloadables") as JArray;
                if (downloadables != null)
                {
                    foreach (var downloadable in downloadables.OfType<JObject>())
                    {
                        var downloadableDate = ParseDate(GetProperty(
                            downloadable,
                            "updated_at", "updatedAt", "UpdatedDate",
                            "created_at", "createdAt", "CreatedDate"));
                        if (downloadableDate.HasValue &&
                            (!variationDate.HasValue || downloadableDate.Value > variationDate.Value))
                            variationDate = downloadableDate;
                    }
                }

                snapshot.VariationUpdatedAt[variationId] = variationDate;
                if (variationDate.HasValue &&
                    (!snapshot.UpdatedAt.HasValue || variationDate.Value > snapshot.UpdatedAt.Value))
                {
                    snapshot.UpdatedAt = variationDate;
                }
            }

            return snapshot;
        }

        private static List<string> CompareVariationHashes(
            VariationSnapshot savedSnapshot,
            VariationSnapshot remoteSnapshot)
        {
            return remoteSnapshot.Variations
                .Where(pair => !savedSnapshot.Variations.TryGetValue(pair.Key, out var savedHash) ||
                    !string.Equals(savedHash, pair.Value, StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .Concat(savedSnapshot.Variations.Keys.Where(id => !remoteSnapshot.Variations.ContainsKey(id)))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> CompareInitialDate(IDatabaseItem item, VariationSnapshot remoteSnapshot)
        {
            DateTimeOffset? localDate = GetInitialComparisonDate(item);
            if (!localDate.HasValue)
                return new List<string>();

            var updated = new List<string>();
            foreach (string variationId in remoteSnapshot.Variations.Keys)
            {
                remoteSnapshot.VariationUpdatedAt.TryGetValue(variationId, out var remoteDate);
                DebugLogger.Log(
                    $"BOOTH initial date comparison: variationId={variationId}, " +
                    $"local={FormatDate(localDate)}, remote={FormatDate(remoteDate)}, " +
                    $"updated={remoteDate.HasValue && remoteDate.Value > localDate.Value}");
                if (remoteDate.HasValue && remoteDate.Value > localDate.Value)
                    updated.Add(variationId);
            }

            return updated;
        }

        private static DateTimeOffset? GetInitialComparisonDate(IDatabaseItem item)
        {
            DateTime date = item is KonoAssetWearableItem ||
                item is KonoAssetAvatarItem ||
                item is KonoAssetWorldObjectItem ||
                item is KonoAssetOtherAssetItem
                ? item.GetCreatedDate()
                : item.GetUpdatedDate();

            if (date == DateTime.MinValue) return null;
            if (date.Kind == DateTimeKind.Unspecified)
                date = DateTime.SpecifyKind(date, DateTimeKind.Utc);

            return new DateTimeOffset(date).ToUniversalTime();
        }

        private static VariationSnapshot? LoadSnapshot(int boothId)
        {
            string json = EditorPrefs.GetString(string.Format(SnapshotPrefsKeyFormat, boothId), string.Empty);
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return JsonConvert.DeserializeObject<VariationSnapshot>(json);
            }
            catch (Exception ex)
            {
                DebugLogger.LogWarning($"Failed to load BOOTH variation snapshot for {boothId}: {ex.Message}");
                return null;
            }
        }

        private static void SaveSnapshot(int boothId, VariationSnapshot snapshot)
        {
            EditorPrefs.SetString(
                string.Format(SnapshotPrefsKeyFormat, boothId),
                JsonConvert.SerializeObject(snapshot));
            DebugLogger.Log($"BOOTH variation snapshot saved: boothId={boothId}, count={snapshot.Variations.Count}");
        }

        private static JToken? GetProperty(JToken? token, params string[] names)
        {
            if (token is not JObject obj) return null;

            return obj.Properties()
                .FirstOrDefault(property => names.Any(name =>
                    string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))?.Value;
        }

        private static DateTimeOffset? ParseDate(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                long value = token.Value<long>();
                return value > 100000000000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                    : DateTimeOffset.FromUnixTimeSeconds(value);
            }

            string text = token.ToString();
            if (long.TryParse(text, out long numericValue))
            {
                return numericValue > 100000000000
                    ? DateTimeOffset.FromUnixTimeMilliseconds(numericValue)
                    : DateTimeOffset.FromUnixTimeSeconds(numericValue);
            }

            return DateTimeOffset.TryParse(text, out var result) ? result : null;
        }

        private static string GetTokenText(JToken token)
        {
            return token.Type == JTokenType.String
                ? token.Value<string>() ?? string.Empty
                : token.ToString(Formatting.None);
        }

        private static string ComputeVariationHash(JObject variation)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(variation.ToString(Formatting.None));
            using (var sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        private static string GetVariationDisplayName(VariationSnapshot snapshot, string variationId)
            => snapshot.VariationNames.TryGetValue(variationId, out string name) && !string.IsNullOrEmpty(name)
                ? name
                : variationId;

        private static string FormatDate(DateTimeOffset? value)
            => value.HasValue ? value.Value.ToUniversalTime().ToString("O") : "none";
    }
}
