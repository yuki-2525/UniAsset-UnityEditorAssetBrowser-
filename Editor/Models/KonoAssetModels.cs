// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from AETools(https://github.com/puk06/AE-Tools)
// AETools is licensed under the MIT License. https://github.com/puk06/AE-Tools/blob/master/LICENSE.txt

#nullable enable

using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Services;

namespace UnityEditorAssetBrowser.Models
{
    internal sealed class KonoAssetItemPathCache
    {
        private string _key = string.Empty;
        private string _itemPath = string.Empty;
        private string[] _itemPaths = Array.Empty<string>();
        private string _imagePath = string.Empty;

        private void Ensure(string id, string? imageFilename)
        {
            string root = DatabaseService.GetKADatabasePath();
            string key = root + "\n" + id + "\n" + imageFilename;
            if (string.Equals(_key, key, StringComparison.Ordinal)) return;

            _key = key;
            _itemPath = Path.GetFullPath(Path.Combine(root, "data", id));
            _itemPaths = new[] { _itemPath };
            _imagePath = string.IsNullOrEmpty(imageFilename)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(root, "images", imageFilename));
        }

        public string GetItemPath(string id, string? imageFilename)
        {
            Ensure(id, imageFilename);
            return _itemPath;
        }

        public string[] GetItemPaths(string id, string? imageFilename)
        {
            Ensure(id, imageFilename);
            return _itemPaths;
        }

        public string GetImagePath(string id, string? imageFilename)
        {
            Ensure(id, imageFilename);
            return _imagePath;
        }
    }

    /// <summary>
    /// 統合されたKonoAssetデータベース
    /// 全てのKonoAssetデータベースのアイテムをまとめて管理する
    /// </summary>
    public class UnifiedKonoAssetDatabase
    {
        public List<IDatabaseItem> Items { get; set; } = new List<IDatabaseItem>();
    }

    // #region Base Database Models
    // /// <summary>
    // /// KonoAssetの基本データベースモデル
    // /// データベースのバージョンとアイテムリストを管理する
    // /// </summary>
    // public class KonoAssetDatabase
    // {
    //     /// <summary>
    //     /// データベースのバージョン
    //     /// </summary>
    //     [JsonProperty("version")]
    //     public int Version { get; set; }

    //     /// <summary>
    //     /// アイテムのリスト
    //     /// </summary>
    //     [JsonProperty("data")]
    //     public object[] Data { get; set; } = Array.Empty<object>();
    // }
    // #endregion

    #region Specific Database Models
    /// <summary>
    /// アバター用データベース
    /// アバターアイテムのリストを管理する
    /// </summary>
    public class KonoAssetAvatarsDatabase
    {
        /// <summary>
        /// アバターアイテムのリスト
        /// </summary>
        [JsonProperty("data")]
        public KonoAssetAvatarItem[] Data { get; set; } = Array.Empty<KonoAssetAvatarItem>();
    }

    /// <summary>
    /// ウェアラブル用データベース
    /// ウェアラブルアイテムのリストを管理する
    /// </summary>
    public class KonoAssetWearablesDatabase
    {
        /// <summary>
        /// ウェアラブルアイテムのリスト
        /// </summary>
        [JsonProperty("data")]
        public KonoAssetWearableItem[] Data { get; set; } = Array.Empty<KonoAssetWearableItem>();
    }

    /// <summary>
    /// ワールドオブジェクト用データベース
    /// ワールドオブジェクトアイテムのリストを管理する
    /// </summary>
    public class KonoAssetWorldObjectsDatabase
    {
        /// <summary>
        /// ワールドオブジェクトアイテムのリスト
        /// </summary>
        [JsonProperty("data")]
        public KonoAssetWorldObjectItem[] Data { get; set; } = Array.Empty<KonoAssetWorldObjectItem>();
    }

    /// <summary>
    /// その他アセット用データベース
    /// その他アセットアイテムのリストを管理する
    /// </summary>
    public class KonoAssetOtherAssetsDatabase
    {
        /// <summary>
        /// その他アセットアイテムのリスト
        /// </summary>
        [JsonProperty("data")]
        public KonoAssetOtherAssetItem[] Data { get; set; } = Array.Empty<KonoAssetOtherAssetItem>();
    }
    #endregion

    #region Item Models
    /// <summary>
    /// ウェアラブルアイテムモデル
    /// 衣装やアクセサリーなどのアイテム情報を管理する
    /// </summary>
    public class KonoAssetWearableItem : IDatabaseItem
    {
        private readonly KonoAssetItemPathCache _pathCache = new KonoAssetItemPathCache();
        /// <summary>
        /// アイテムのID
        /// </summary>

        [JsonProperty("id")]
        public string Id { get; set; } = "";

        /// <summary>
        /// アイテムの詳細情報
        /// </summary>
        [JsonProperty("description")]
        public KonoAssetDescription Description { get; set; } = new KonoAssetDescription();

        /// <summary>
        /// アイテムのカテゴリー
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; } = "";

        /// <summary>
        /// 対応アバターのリスト
        /// </summary>
        [JsonProperty("supportedAvatars")]
        public string[] SupportedAvatars { get; set; } = Array.Empty<string>();

        public string GetTitle()
            => Description.Name;
        public string GetAuthor()
            => Description.Creator;
        public string GetMemo()
            => Description.Memo ?? "";
        public string GetItemPath()
            => _pathCache.GetItemPath(Id, Description.ImageFilename);
        public string[] GetItemPaths() => _pathCache.GetItemPaths(Id, Description.ImageFilename);
        public string GetImagePath()
            => _pathCache.GetImagePath(Id, Description.ImageFilename);
        public string[] GetSupportedAvatars()
            => SupportedAvatars;
        public int GetBoothId()
            => Description.BoothItemId ?? -1;
        public string GetCategory()
            => Category;
        public string[] GetTags()
            => Description.Tags;
        public DateTime GetCreatedDate()
            => DateTimeOffset.FromUnixTimeMilliseconds(Description.CreatedAt).DateTime;
        public DateTime GetUpdatedDate()
            => GetCreatedDate();
    }

    /// <summary>
    /// アバターアイテムモデル
    /// アバターの情報を管理する
    /// </summary>
    public class KonoAssetAvatarItem : IDatabaseItem
    {
        private readonly KonoAssetItemPathCache _pathCache = new KonoAssetItemPathCache();
        /// <summary>
        /// アバターのID
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        /// <summary>
        /// アバターの詳細情報
        /// </summary>
        [JsonProperty("description")]
        public KonoAssetDescription Description { get; set; } = new KonoAssetDescription();

        public string GetTitle()
            => Description.Name;
        public string GetAuthor()
            => Description.Creator;
        public string GetMemo()
            => Description.Memo ?? "";
        public string GetItemPath()
            => _pathCache.GetItemPath(Id, Description.ImageFilename);
        public string[] GetItemPaths() => _pathCache.GetItemPaths(Id, Description.ImageFilename);
        public string GetImagePath()
            => _pathCache.GetImagePath(Id, Description.ImageFilename);
        public string[] GetSupportedAvatars()
            => Array.Empty<string>();
        public int GetBoothId()
            => Description.BoothItemId ?? -1;
        public string GetCategory()
            => LocalizationService.Instance.GetString("category_avatar");
        public string[] GetTags()
            => Description.Tags;
        public DateTime GetCreatedDate()
            => DateTimeOffset.FromUnixTimeMilliseconds(Description.CreatedAt).DateTime;
        public DateTime GetUpdatedDate()
            => GetCreatedDate();
    }

    /// <summary>
    /// ワールドオブジェクトアイテムモデル
    /// ワールドオブジェクトの情報を管理する
    /// </summary>
    public class KonoAssetWorldObjectItem : IDatabaseItem
    {
        private readonly KonoAssetItemPathCache _pathCache = new KonoAssetItemPathCache();
        /// <summary>
        /// オブジェクトのID
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        /// <summary>
        /// オブジェクトの詳細情報
        /// </summary>
        [JsonProperty("description")]
        public KonoAssetDescription Description { get; set; } = new KonoAssetDescription();

        /// <summary>
        /// オブジェクトのカテゴリー
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; } = "";

        public string GetTitle()
            => Description.Name;
        public string GetAuthor()
            => Description.Creator;
        public string GetMemo()
            => Description.Memo ?? "";
        public string GetItemPath()
            => _pathCache.GetItemPath(Id, Description.ImageFilename);
        public string[] GetItemPaths() => _pathCache.GetItemPaths(Id, Description.ImageFilename);
        public string GetImagePath()
            => _pathCache.GetImagePath(Id, Description.ImageFilename);
        public string[] GetSupportedAvatars()
            => Array.Empty<string>();
        public int GetBoothId()
            => Description.BoothItemId ?? -1;
        public string GetCategory()
            => Category;
        public string[] GetTags()
            => Description.Tags;
        public DateTime GetCreatedDate()
            => DateTimeOffset.FromUnixTimeMilliseconds(Description.CreatedAt).DateTime;
        public DateTime GetUpdatedDate()
            => GetCreatedDate();
    }

    /// <summary>
    /// その他アセットアイテムモデル
    /// その他アセットの情報を管理する
    /// </summary>
    public class KonoAssetOtherAssetItem : IDatabaseItem
    {
        private readonly KonoAssetItemPathCache _pathCache = new KonoAssetItemPathCache();
        /// <summary>
        /// アセットのID
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        /// <summary>
        /// アセットの詳細情報
        /// </summary>
        [JsonProperty("description")]
        public KonoAssetDescription Description { get; set; } = new KonoAssetDescription();

        /// <summary>
        /// アセットのカテゴリー
        /// </summary>
        [JsonProperty("category")]
        public string Category { get; set; } = "";

        public string GetTitle()
            => Description.Name;
        public string GetAuthor()
            => Description.Creator;
        public string GetMemo()
            => Description.Memo ?? "";
        public string GetItemPath()
            => _pathCache.GetItemPath(Id, Description.ImageFilename);
        public string[] GetItemPaths() => _pathCache.GetItemPaths(Id, Description.ImageFilename);
        public string GetImagePath()
            => _pathCache.GetImagePath(Id, Description.ImageFilename);
        public string[] GetSupportedAvatars()
            => Array.Empty<string>();
        public int GetBoothId()
            => Description.BoothItemId ?? -1;
        public string GetCategory()
            => Category;
        public string[] GetTags()
            => Description.Tags;
        public DateTime GetCreatedDate()
            => DateTimeOffset.FromUnixTimeMilliseconds(Description.CreatedAt).DateTime;
        public DateTime GetUpdatedDate()
            => GetCreatedDate();
    }
    #endregion

    #region Description Model
    /// <summary>
    /// KonoAssetアイテムの詳細情報モデル
    /// アイテムの基本情報を管理する
    /// </summary>
    public class KonoAssetDescription
    {
        /// <summary>
        /// アイテムの名前
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// 作者名
        /// </summary>
        [JsonProperty("creator")]
        public string Creator { get; set; } = "";

        /// <summary>
        /// 画像ファイル名
        /// </summary>
        [JsonProperty("imageFileName")]
        public string? ImageFilename { get; set; } = "";

        /// <summary>
        /// タグのリスト
        /// </summary>
        [JsonProperty("tags")]
        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// メモ
        /// </summary>
        [JsonProperty("memo")]
        public string? Memo { get; set; }

        /// <summary>
        /// BOOTHのアイテムID
        /// </summary>
        [JsonProperty("boothItemId")]
        public int? BoothItemId { get; set; }

        /// <summary>
        /// 依存アイテムのリスト
        /// </summary>
        [JsonProperty("dependencies")]
        public string[] Dependencies { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 作成日時（UnixTimeMilliseconds）
        /// </summary>
        [JsonProperty("createdAt")]
        public long CreatedAt { get; set; }

        /// <summary>
        /// 公開日時（UnixTimeMilliseconds）
        /// </summary>
        [JsonProperty("publishedAt")]
        public long? PublishedAt { get; set; }
    }
    #endregion
}
