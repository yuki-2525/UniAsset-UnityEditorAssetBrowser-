// Copyright (c) 2025-2026 sakurayuki
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Services;

namespace UnityEditorAssetBrowser.Models
{
    /// <summary>
    /// BOOTH Library Managerのデータベースモデル
    /// </summary>
    public class BOOTHLMDatabase
    {
        public List<BOOTHLMItem> Items { get; set; } = new List<BOOTHLMItem>();

        public BOOTHLMDatabase(IEnumerable<BOOTHLMItem> items)
        {
            Items = new List<BOOTHLMItem>(items);
        }
    }

    /// <summary>
    /// BOOTH Library Managerのアイテムモデル
    /// booth_itemsテーブルをベースに、関連情報を集約します
    /// </summary>
    public class BOOTHLMItem : IDatabaseItem
    {
        public int Id { get; set; }
        public string RegisteredId { get; set; } = "";
        public string Name { get; set; } = "";
        public string ShopName { get; set; } = "";
        public string? ThumbnailUrl { get; set; }
        public string? Description { get; set; }
        public string CategoryName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsAdult { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        // IDatabaseItem の実装
        public string GetTitle() => Name;
        public string GetAuthor() => ShopName;
        public string GetMemo() => Description ?? "";

        public string GetItemPath()
        {
            // 設定から取得したBOOTHLM Data Path配下の商品IDフォルダを返す
            // ※DatabaseServiceにGetBOOTHLMDataPathの実装が必要
            string basePath = DatabaseService.GetBOOTHLMDataPath();
            return Path.GetFullPath(Path.Combine(basePath, RegisteredId));
        }

        public string GetImagePath()
        {
            // BOOTHLMは画像URLを管理しているため、URLを返す
            return ThumbnailUrl ?? "";
        }

        public string[] GetSupportedAvatars() => Array.Empty<string>();
        public int GetBoothId() => Id;
        public string GetCategory() => CategoryName;
        public string[] GetTags() => Tags.ToArray();
        public DateTime GetCreatedDate() => CreatedAt;
        public DateTime GetUpdatedDate() => UpdatedAt;
    }
}