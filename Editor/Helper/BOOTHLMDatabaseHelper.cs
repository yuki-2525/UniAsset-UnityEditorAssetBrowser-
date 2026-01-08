// Copyright (c) 2025-2026 sakurayuki
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite4Unity3d;
using UnityEditorAssetBrowser.Models;
using UnityEngine;

namespace UnityEditorAssetBrowser.Helper
{
    /// <summary>
    /// BOOTHLMのSQLiteデータベース（data.db）の読み込みを支援するヘルパークラス
    /// SQLite4Unity3dを使用してデータベースにアクセスします
    /// </summary>
    public static class BOOTHLMDatabaseHelper
    {
        // クエリ結果を受け取るための内部クラス
        private class BoothItemResult
        {
            public int id { get; set; }
            public string? registered_id { get; set; }
            public string? name { get; set; }
            public string? shop_name { get; set; }
            public string? thumbnail_url { get; set; }
            public string? description { get; set; }
            public string? category_name { get; set; }
            public string? created_at { get; set; }
            public string? updated_at { get; set; }
            public bool adult { get; set; }
        }

        private class TagResult
        {
            public string? tag { get; set; }
        }

        /// <summary>
        /// BOOTHLMのデータベースファイルを読み込む
        /// </summary>
        /// <param name="dbPath">data.dbへのフルパス</param>
        /// <returns>読み込んだデータベース。失敗時はnull</returns>
        public static BOOTHLMDatabase? LoadBOOTHLMDatabase(string dbPath)
        {
            DebugLogger.Log($"Starting to load BOOTHLM database from: {dbPath}");

            if (!File.Exists(dbPath))
            {
                DebugLogger.LogWarning($"BOOTHLM database file not found at: {dbPath}");
                return null;
            }

            var items = new List<BOOTHLMItem>();

            try
            {
                // SQLiteConnectionを作成（SQLite4Unity3d）
                using (var connection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly))
                {
                    DebugLogger.Log("SQLite DB connected.");
                    // アイテム情報の取得クエリ
                    string query = @"
                        SELECT 
                            b.id, 
                            r.id as registered_id,
                            b.name, 
                            s.name as shop_name, 
                            b.thumbnail_url, 
                            b.description, 
                            c.name as category_name,
                            r.created_at,
                            r.updated_at,
                            b.adult
                        FROM booth_items b
                        INNER JOIN registered_items r ON b.id = r.booth_item_id
                        LEFT JOIN shops s ON b.shop_subdomain = s.subdomain
                        LEFT JOIN sub_categories c ON b.sub_category = c.id";

                    var results = connection.Query<BoothItemResult>(query);

                    foreach (var result in results)
                    {
                        var item = new BOOTHLMItem
                        {
                            Id = result.id,
                            RegisteredId = result.registered_id ?? "",
                            Name = result.name ?? "",
                            ShopName = result.shop_name ?? "",
                            ThumbnailUrl = result.thumbnail_url,
                            Description = result.description,
                            CategoryName = result.category_name ?? "不明",
                            CreatedAt = ParseDateTime(result.created_at),
                            UpdatedAt = ParseDateTime(result.updated_at),
                            IsAdult = result.adult
                        };

                        // 各アイテムに紐づくタグを取得
                        item.Tags = GetTagsForItem(connection, item.Id);
                        items.Add(item);
                    }
                }
                
                DebugLogger.Log($"Loaded {items.Count} items from BOOTHLM database.");
                return new BOOTHLMDatabase(items);
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Failed to load BOOTHLM database: {ex.Message}\n{ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    DebugLogger.LogError($"Inner Exception: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
                }
                return null;
            }
        }

        /// <summary>
        /// 指定した商品IDに紐づくタグ名リストを取得する
        /// </summary>
        private static List<string> GetTagsForItem(SQLiteConnection connection, int boothItemId)
        {
            string tagQuery = "SELECT tag FROM booth_item_tag_relations WHERE booth_item_id = ?";
            var results = connection.Query<TagResult>(tagQuery, boothItemId);
            return results.Select(r => r.tag ?? "").Where(t => !string.IsNullOrEmpty(t)).ToList();
        }

        /// <summary>
        /// SQLiteのISO8601形式文字列をDateTimeに変換
        /// </summary>
        private static DateTime ParseDateTime(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr)) return DateTime.MinValue;
            if (DateTime.TryParse(dateStr, out DateTime result))
                return result;
            return DateTime.MinValue;
        }
    }
}
