// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from Avatar-Explorer(https://github.com/puk06/Avatar-Explorer)
// Avatar-Explorer is licensed under the MIT License. https://github.com/puk06/Avatar-Explorer/blob/main/LICENSE

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Services;

namespace UnityEditorAssetBrowser.Models
{
    /// <summary>
    /// commonAvatars.json のエントリを表すV2モデル
    /// </summary>
    public sealed class CommonAvatarV2Definition
    {
        public string Name { get; set; } = "";

        [JsonProperty("GroupName")]
        private string GroupName { set { Name = value; } }

        public List<string> Avatars { get; set; } = new List<string>();
    }

    #region Database Model
    /// <summary>
    /// AvatarExplorer V2のデータベースモデル
    /// </summary>
    public sealed class AvatarExplorerV2Database
    {
        [JsonProperty("Items")]
        public List<AvatarExplorerV2Item> Items { get; set; } = new List<AvatarExplorerV2Item>();

        public AvatarExplorerV2Database(AvatarExplorerV2Item[] items)
        {
            Items = new List<AvatarExplorerV2Item>(items);
        }
    }
    #endregion

    #region Item Model
    /// <summary>
    /// AvatarExplorer V2のアイテムタイプ
    /// アセットの種類を定義する
    /// </summary>
    public enum AvatarExplorerV2ItemType
    {
        /// <summary>
        /// アバター
        /// </summary>
        Avatar,

        /// <summary>
        /// 衣装
        /// </summary>
        Clothing,

        /// <summary>
        /// テクスチャ
        /// </summary>
        Texture,

        /// <summary>
        /// ギミック
        /// </summary>
        Gimmick,

        /// <summary>
        /// アクセサリー
        /// </summary>
        Accessory,

        /// <summary>
        /// 髪型
        /// </summary>
        HairStyle,

        /// <summary>
        /// アニメーション
        /// </summary>
        Animation,

        /// <summary>
        /// ツール
        /// </summary>
        Tool,

        /// <summary>
        /// シェーダー
        /// </summary>
        Shader,

        /// <summary>
        /// カスタムカテゴリー
        /// </summary>
        Custom,

        /// <summary>
        /// 不明
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// AvatarExplorerV2のアイテムモデル
    /// アセットの詳細情報を管理する
    /// </summary>
    public class AvatarExplorerV2Item : IDatabaseItem
    {
        /// <summary>
        /// アイテムのタイトル
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// 作者名
        /// </summary>
        public string AuthorName { get; set; } = "";

        [JsonProperty("Author")]
        private string AuthorV2 { set { AuthorName = value; } }

        /// <summary>
        /// アイテムのメモ
        /// </summary>
        public string ItemMemo { get; set; } = "";

        /// <summary>
        /// アイテムのパス
        /// </summary>
        public string ItemPath { get; set; } = "";

        /// <summary>
        /// 画像のパス
        /// </summary>
        public string ImagePath { get; set; } = "";

        [JsonProperty("ThumbnailFileName")]
        private string ImagePathV2 { set { ImagePath = value; } }

        /// <summary>
        /// マテリアルのパス
        /// </summary>
        public string MaterialPath { get; set; } = "";

        /// <summary>
        /// 対応アバターのリスト
        /// </summary>
        public string[] SupportedAvatar { get; set; } = Array.Empty<string>();

        [JsonProperty("SupportedAvatars")]
        private string[] SupportedAvatarsV2 { set { SupportedAvatar = value; } }

        /// <summary>
        /// BOOTHのID
        /// </summary>
        public int BoothId { get; set; } = -1;

        /// <summary>
        /// アイテムのタイプ
        /// </summary>
        public int Type { get; set; } = 0;

        /// <summary>
        /// カスタムカテゴリー
        /// </summary>
        public string CustomCategory { get; set; } = "";

        /// <summary>
        /// 作者のID
        /// </summary>
        public string AuthorId { get; set; } = "";

        /// <summary>
        /// サムネイル画像のURL
        /// </summary>
        public string ThumbnailUrl { get; set; } = "";

        /// <summary>
        /// 作成日時
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 更新日時
        /// </summary>
        public DateTime UpdatedDate { get; set; } = DateTime.MinValue;

        /// <summary>
        /// アイテムのタグ
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();

        public string GetTitle()
            => Title;
        public string GetAuthor()
            => AuthorName;
        public string GetMemo()
            => ItemMemo;
        public string GetItemPath()
        {
            if (ItemPath.StartsWith("<sys>"))
            {
                var root = DatabaseService.GetAEDataRootPath();
                if (!string.IsNullOrEmpty(root))
                {
                    // <sys>で始まっていないものはフルパスと認識する
                    return Path.GetFullPath(Path.Combine(root, ItemPath.Replace("<sys>", "")));
                }
            }

            return Path.GetFullPath(ItemPath);
        }
        public string GetImagePath()
        {
            var thumbnailDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Avatar Explorer V2", "images", "item_thumbnails");
            return Path.GetFullPath(Path.Combine(thumbnailDir, ImagePath));
        }
        public string[] GetSupportedAvatars()
            => SupportedAvatar;
        public int GetBoothId()
            => BoothId;
        public string GetCategory()
            => GetAECategoryName();
        public string[] GetTags()
            => Tags;
        public DateTime GetCreatedDate()
            => TimeZoneInfo.ConvertTimeToUtc(CreatedDate, TimeZoneInfo.Local);
        public DateTime GetUpdatedDate()
            => TimeZoneInfo.ConvertTimeToUtc(UpdatedDate, TimeZoneInfo.Local);

        /// <summary>
        /// AEアイテムのカテゴリー名を取得
        /// Typeの値に基づいてカテゴリー名を決定する
        /// </summary>
        /// <returns>アイテムのカテゴリー名</returns>
        public string GetAECategoryName()
            => GetCategoryNameByType((AvatarExplorerV2ItemType)Type);

        /// <summary>
        /// タイプに基づいてカテゴリー名を取得
        /// </summary>
        /// <param name="itemType">アイテムのタイプ</param>
        /// <returns>対応するカテゴリー名</returns>
        private string GetCategoryNameByType(AvatarExplorerV2ItemType itemType)
        {
            return itemType switch
            {
                AvatarExplorerV2ItemType.Avatar => LocalizationService.Instance.GetString("category_avatar"),
                AvatarExplorerV2ItemType.Clothing => LocalizationService.Instance.GetString("category_clothing"),
                AvatarExplorerV2ItemType.Texture => LocalizationService.Instance.GetString("category_texture"),
                AvatarExplorerV2ItemType.Gimmick => LocalizationService.Instance.GetString("category_gimmick"),
                AvatarExplorerV2ItemType.Accessory => LocalizationService.Instance.GetString("category_accessory"),
                AvatarExplorerV2ItemType.HairStyle => LocalizationService.Instance.GetString("category_hairstyle"),
                AvatarExplorerV2ItemType.Animation => LocalizationService.Instance.GetString("category_animation"),
                AvatarExplorerV2ItemType.Tool => LocalizationService.Instance.GetString("category_tool"),
                AvatarExplorerV2ItemType.Shader => LocalizationService.Instance.GetString("category_shader"),
                AvatarExplorerV2ItemType.Custom => CustomCategory,
                _ => LocalizationService.Instance.GetString("category_unknown")
            };
        }

        public AvatarExplorerItem ToBaseModel()
        {
            return new AvatarExplorerItem
            {
                Title = Title,
                AuthorName = AuthorName,
                ItemMemo = ItemMemo,
                ItemPath = ItemPath,
                ImagePath = ImagePath,
                MaterialPath = MaterialPath,
                SupportedAvatar = SupportedAvatar,
                BoothId = BoothId,
                Type = Type,
                CustomCategory = CustomCategory,
                AuthorId = AuthorId,
                ThumbnailUrl = ThumbnailUrl,
                CreatedDate = CreatedDate,
                UpdatedDate = UpdatedDate,
                Tags = Tags,
            };
        }
    }
    #endregion
}
