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
    /// CommonAvatar.json のエントリを表すモデル
    /// </summary>
    public sealed class CommonAvatarDefinition
    {
        public string Name { get; set; } = "";
        public List<string> Avatars { get; set; } = new List<string>();
    }

    #region Database Model
    /// <summary>
    /// AvatarExplorerのデータベースモデル
    /// アセットアイテムのリストを管理する
    /// </summary>
    public class AvatarExplorerDatabase
    {
        /// <summary>
        /// アセットアイテムのリスト
        /// </summary>
        [JsonProperty("Items")]
        public List<AvatarExplorerItem> Items { get; set; } = new List<AvatarExplorerItem>();

        /// <summary>
        /// 配列からデータベースを作成するための変換コンストラクタ
        /// </summary>
        /// <param name="items">アイテムの配列</param>
        public AvatarExplorerDatabase(AvatarExplorerItem[] items)
        {
            Items = new List<AvatarExplorerItem>(items);
        }
    }
    #endregion

    #region Item Model
    /// <summary>
    /// AvatarExplorerのアイテムタイプ
    /// アセットの種類を定義する
    /// </summary>
    public enum AvatarExplorerItemType
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
    /// AvatarExplorerのアイテムモデル
    /// アセットの詳細情報を管理する
    /// </summary>
    public class AvatarExplorerItem : IDatabaseItem
    {
        /// <summary>
        /// アイテムのタイトル
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// 作者名
        /// </summary>
        public string AuthorName { get; set; } = "";

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

        /// <summary>
        /// マテリアルのパス
        /// </summary>
        public string MaterialPath { get; set; } = "";

        /// <summary>
        /// 対応アバターのリスト
        /// </summary>
        public string[] SupportedAvatar { get; set; } = Array.Empty<string>();

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
            if (ItemPath.StartsWith("Datas\\"))
            {
                return Path.GetFullPath(Path.Combine(DatabaseService.GetAEDatabasePath(), ItemPath.Replace("Datas\\", "")));
            }

            return Path.GetFullPath(ItemPath);
        }
        public string GetImagePath()
            => Path.GetFullPath(Path.Combine(DatabaseService.GetAEDatabasePath(), ImagePath.Replace("Datas\\", "")));
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
            => GetCategoryNameByType((AvatarExplorerItemType)Type);

        /// <summary>
        /// タイプに基づいてカテゴリー名を取得
        /// </summary>
        /// <param name="itemType">アイテムのタイプ</param>
        /// <returns>対応するカテゴリー名</returns>
        private string GetCategoryNameByType(AvatarExplorerItemType itemType)
        {
            return itemType switch
            {
                AvatarExplorerItemType.Avatar => LocalizationService.Instance.GetString("category_avatar"),
                AvatarExplorerItemType.Clothing => LocalizationService.Instance.GetString("category_clothing"),
                AvatarExplorerItemType.Texture => LocalizationService.Instance.GetString("category_texture"),
                AvatarExplorerItemType.Gimmick => LocalizationService.Instance.GetString("category_gimmick"),
                AvatarExplorerItemType.Accessory => LocalizationService.Instance.GetString("category_accessory"),
                AvatarExplorerItemType.HairStyle => LocalizationService.Instance.GetString("category_hairstyle"),
                AvatarExplorerItemType.Animation => LocalizationService.Instance.GetString("category_animation"),
                AvatarExplorerItemType.Tool => LocalizationService.Instance.GetString("category_tool"),
                AvatarExplorerItemType.Shader => LocalizationService.Instance.GetString("category_shader"),
                AvatarExplorerItemType.Custom => CustomCategory,
                _ => LocalizationService.Instance.GetString("category_unknown")
            };
        }
    }
    #endregion
}
