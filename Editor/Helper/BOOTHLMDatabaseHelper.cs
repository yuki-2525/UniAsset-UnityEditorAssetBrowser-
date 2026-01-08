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

        private class ListResult
        {
            public int id { get; set; }
            public string? title { get; set; }
            public string? description { get; set; }
        }

        private class SmartListCriteriaResult
        {
            public string? text { get; set; }
            public int? category_id { get; set; }
            public int? subcategory_id { get; set; }
            public string? age_restriction { get; set; }
        }

        // キャッシュ
        private static readonly Dictionary<(int id, BOOTHLMListType type, int limit), List<string>> _registeredIdsCache = new Dictionary<(int, BOOTHLMListType, int), List<string>>();
        private static readonly Dictionary<(int id, BOOTHLMListType type), int> _countCache = new Dictionary<(int, BOOTHLMListType), int>();

        /// <summary>
        /// キャッシュをクリアする
        /// </summary>
        public static void ClearCache()
        {
            _registeredIdsCache.Clear();
            _countCache.Clear();
        }

        /// <summary>
        /// BOOTHLMのデータベースファイルを読み込む
        /// </summary>
        /// <param name="dbPath">data.dbへのフルパス</param>
        /// <returns>読み込んだデータベース。失敗時はnull</returns>
        public static BOOTHLMDatabase? LoadBOOTHLMDatabase(string dbPath)
        {
            // DB再読み込み時はキャッシュもクリア
            ClearCache();

            DebugLogger.Log($"Starting to load BOOTHLM database from: {dbPath}");

            if (!File.Exists(dbPath))
            {
                DebugLogger.LogWarning($"BOOTHLM database file not found at: {dbPath}");
                return null;
            }

            var items = new List<BOOTHLMItem>();
            var lists = new List<BOOTHLMList>();

            try
            {
                // SQLiteConnectionを作成（SQLite4Unity3d）
                using (var connection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly))
                {
                    DebugLogger.Log("SQLite DB connected.");
                    
                    // リスト情報の取得
                    var normalLists = connection.Query<ListResult>("SELECT id, title, description FROM lists");
                    lists.AddRange(normalLists.Select(l => new BOOTHLMList { Id = l.id, Title = l.title ?? "", Description = l.description ?? "", Type = BOOTHLMListType.Normal }));

                    var smartLists = connection.Query<ListResult>("SELECT id, title, description FROM smart_lists");
                    lists.AddRange(smartLists.Select(l => new BOOTHLMList { Id = l.id, Title = l.title ?? "", Description = l.description ?? "", Type = BOOTHLMListType.Smart }));

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
                        items.Add(MapResultToItem(result, connection));
                    }
                }
                
                DebugLogger.Log($"Loaded {items.Count} items and {lists.Count} lists from BOOTHLM database.");
                return new BOOTHLMDatabase(items, lists);
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

        private class RegisteredIdResult
        {
            public string? registered_id { get; set; }
        }
        
        private class ListItemResult
        {
            public string? item_id { get; set; }
        }

        /// <summary>
        /// 指定されたリストのアイテムIDリストを取得する
        /// </summary>
        public static List<string> GetListItemRegisteredIds(BOOTHLMList list, int limit = -1)
        {
            // キャッシュチェック
            var cacheKey = (list.Id, list.Type, limit);
            if (_registeredIdsCache.TryGetValue(cacheKey, out var cachedIds))
            {
                return cachedIds;
            }

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dbPath = Path.Combine(appDataPath, "pm.booth.library-manager", "data.db");

            if (!File.Exists(dbPath)) 
            {
                DebugLogger.LogError($"DB file not found: {dbPath}");
                return new List<string>();
            }

            var ids = new List<string>();

            try
            {
                using (var connection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly))
                {
                    if (list.Type == BOOTHLMListType.Normal)
                    {
                        string query = "SELECT item_id FROM list_items WHERE list_id = ?";
                        if (limit > 0) query += $" LIMIT {limit}";

                        var results = connection.Query<ListItemResult>(query, list.Id);
                        ids.AddRange(results.Select(r => r.item_id ?? "").Where(id => !string.IsNullOrEmpty(id)));
                    }
                    else // Smart List
                    {
                         string baseQuery = @"
                        SELECT 
                            r.id as registered_id
                        FROM booth_items b
                        INNER JOIN registered_items r ON b.id = r.booth_item_id
                        LEFT JOIN sub_categories c ON b.sub_category = c.id";

                        // Criteriaの取得
                        var criteria = connection.Query<SmartListCriteriaResult>("SELECT * FROM smart_list_criteria WHERE smart_list_id = ?", list.Id).FirstOrDefault();
                        // Tagsの取得
                        var tags = connection.Query<TagResult>("SELECT tag FROM smart_list_tags WHERE smart_list_id = ?", list.Id).Select(t => t.tag).ToList();

                        string whereClause = " WHERE 1=1";
                        var args = new List<object>();

                        if (criteria != null)
                        {
                            if (!string.IsNullOrEmpty(criteria.text))
                            {
                                // テキスト検索: 商品名 OR 説明 OR URL(サブドメイン) OR ショップ名
                                whereClause += @" AND (
                                    b.name LIKE ? OR 
                                    b.description LIKE ? OR 
                                    b.shop_subdomain LIKE ? OR 
                                    EXISTS (SELECT 1 FROM shops s WHERE s.subdomain = b.shop_subdomain AND s.name LIKE ?)
                                )";
                                string likeText = $"%{criteria.text}%";
                                args.Add(likeText);
                                args.Add(likeText);
                                args.Add(likeText);
                                args.Add(likeText);
                            }
                            if (criteria.category_id.HasValue)
                            {
                                whereClause += " AND b.sub_category IN (SELECT id FROM sub_categories WHERE parent_category_id = ?)";
                                args.Add(criteria.category_id.Value);
                            }
                            if (criteria.subcategory_id.HasValue)
                            {
                                whereClause += " AND b.sub_category = ?";
                                args.Add(criteria.subcategory_id.Value);
                            }
                            if (!string.IsNullOrEmpty(criteria.age_restriction))
                            {
                                if (criteria.age_restriction == "adult_only") whereClause += " AND b.adult = 1";
                                else if (criteria.age_restriction == "safe") whereClause += " AND b.adult = 0";
                            }
                        }

                        if (tags != null && tags.Count > 0)
                        {
                            // タグ条件: 指定されたすべてのタグを含む (AND条件)
                            // 各タグごとにEXISTS句を追加してANDでつなぐ
                            foreach (var tag in tags)
                            {
                                if (tag != null)
                                {
                                    whereClause += " AND EXISTS (SELECT 1 FROM booth_item_tag_relations tr WHERE tr.booth_item_id = b.id AND tr.tag = ?)";
                                    args.Add(tag);
                                }
                            }
                        }
                        
                        string finalQuery = baseQuery + whereClause;
                        if (limit > 0) finalQuery += $" LIMIT {limit}";
                        
                        var results = connection.Query<RegisteredIdResult>(finalQuery, args.ToArray());
                        ids.AddRange(results.Select(r => r.registered_id ?? "").Where(id => !string.IsNullOrEmpty(id)));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Failed to load list item ids: {ex.Message}");
            }

            // キャッシュに保存
            _registeredIdsCache[cacheKey] = ids;

            DebugLogger.Log($"Loaded {ids.Count} item ids for list {list.Title}");
            return ids;
        }

        /// <summary>
        /// リスト内のアイテム数を取得します
        /// </summary>
        public static int GetListItemCount(BOOTHLMList list)
        {
            // キャッシュチェック
            var cacheKey = (list.Id, list.Type);
            if (_countCache.TryGetValue(cacheKey, out var cachedCount))
            {
                return cachedCount;
            }

            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dbPath = Path.Combine(appDataPath, "pm.booth.library-manager", "data.db");

                if (!File.Exists(dbPath)) return 0;

                using (var connection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly))
                {
                    int count = 0;
                    if (list.Type == BOOTHLMListType.Normal)
                    {
                        count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM list_items WHERE list_id = ?", list.Id);
                    }
                    else // Smart List
                    {
                         string baseQuery = @"
                        SELECT COUNT(*)
                        FROM booth_items b
                        INNER JOIN registered_items r ON b.id = r.booth_item_id
                        LEFT JOIN sub_categories c ON b.sub_category = c.id";

                        // Criteriaの取得
                        var criteria = connection.Query<SmartListCriteriaResult>("SELECT * FROM smart_list_criteria WHERE smart_list_id = ?", list.Id).FirstOrDefault();
                        // Tagsの取得
                        var tags = connection.Query<TagResult>("SELECT tag FROM smart_list_tags WHERE smart_list_id = ?", list.Id).Select(t => t.tag).ToList();

                        string whereClause = " WHERE 1=1";
                        var args = new List<object>();

                        if (criteria != null)
                        {
                            if (!string.IsNullOrEmpty(criteria.text))
                            {
                                whereClause += @" AND (
                                    b.name LIKE ? OR 
                                    b.description LIKE ? OR 
                                    b.shop_subdomain LIKE ? OR 
                                    EXISTS (SELECT 1 FROM shops s WHERE s.subdomain = b.shop_subdomain AND s.name LIKE ?)
                                )";
                                string likeText = $"%{criteria.text}%";
                                args.Add(likeText);
                                args.Add(likeText);
                                args.Add(likeText);
                                args.Add(likeText);
                            }
                            if (criteria.category_id.HasValue)
                            {
                                whereClause += " AND b.sub_category IN (SELECT id FROM sub_categories WHERE parent_category_id = ?)";
                                args.Add(criteria.category_id.Value);
                            }
                            if (criteria.subcategory_id.HasValue)
                            {
                                whereClause += " AND b.sub_category = ?";
                                args.Add(criteria.subcategory_id.Value);
                            }
                            if (!string.IsNullOrEmpty(criteria.age_restriction))
                            {
                                if (criteria.age_restriction == "adult_only") whereClause += " AND b.adult = 1";
                                else if (criteria.age_restriction == "safe") whereClause += " AND b.adult = 0";
                            }
                        }

                        if (tags != null && tags.Count > 0)
                        {
                            foreach (var tag in tags)
                            {
                                if (tag != null)
                                {
                                    whereClause += " AND EXISTS (SELECT 1 FROM booth_item_tag_relations tr WHERE tr.booth_item_id = b.id AND tr.tag = ?)";
                                    args.Add(tag);
                                }
                            }
                        }
                        
                        string finalQuery = baseQuery + whereClause;
                        count = connection.ExecuteScalar<int>(finalQuery, args.ToArray());
                    }

                    // キャッシュに保存
                    _countCache[cacheKey] = count;
                    return count;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogError($"Failed to count list items: {ex.Message}");
                return 0;
            }
        }

        private static BOOTHLMItem MapResultToItem(BoothItemResult result, SQLiteConnection connection)
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
            item.Tags = GetTagsForItem(connection, item.Id);
            return item;
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
