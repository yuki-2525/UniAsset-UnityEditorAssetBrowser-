// Copyright (c) 2025-2026 sakurayuki
// This code is borrowed from Avatar-Explorer(https://github.com/puk06/Avatar-Explorer)
// Avatar-Explorer is licensed under the MIT License. https://github.com/puk06/Avatar-Explorer/blob/main/LICENSE

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Services;

namespace UnityEditorAssetBrowser.Models
{
    /// <summary>
    /// tempAvatars.json のルートモデル
    /// </summary>
    public sealed class TempAvatarV2Database
    {
        [JsonProperty("Items")]
        public List<TempAvatarV2Definition> Items { get; set; } = new List<TempAvatarV2Definition>();

        [JsonProperty("Version")]
        public int Version { get; set; }
    }

    /// <summary>
    /// tempAvatars.json のエントリを表すV2モデル
    /// </summary>
    public sealed class TempAvatarV2Definition
    {
        public string AvatarName { get; set; } = "";
        public string Id { get; set; } = "";
    }

    /// <summary>
    /// commonAvatars.json のエントリを表すV2モデル
    /// </summary>
    public sealed class CommonAvatarV2Definition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        [JsonProperty("GroupName")]
        private string GroupName { set { Name = value; } }

        public List<string> Avatars { get; set; } = new List<string>();
    }

    /// <summary>
    /// commonAvatars.json のルートモデル
    /// </summary>
    public sealed class CommonAvatarV2Database
    {
        [JsonProperty("Items")]
        public List<CommonAvatarV2Definition> Items { get; set; } = new List<CommonAvatarV2Definition>();

        [JsonProperty("Version")]
        public int Version { get; set; }
    }

    #region Database Model
    /// <summary>
    /// AvatarExplorer V2のデータベースモデル
    /// </summary>
    public sealed class AvatarExplorerV2Database
    {
        [JsonProperty("Items")]
        public List<AvatarExplorerV2Item> Items { get; set; } = new List<AvatarExplorerV2Item>();

        [JsonProperty("Version")]
        public int Version { get; set; }

        public AvatarExplorerV2Database()
        {
        }

        public AvatarExplorerV2Database(AvatarExplorerV2Item[] items)
        {
            Items = new List<AvatarExplorerV2Item>(items);
        }
    }
    #endregion

    #region Item Model
    /// <summary>
    /// AvatarExplorer V2のアイテムタイプ
    /// </summary>
    public enum AvatarExplorerV2ItemType
    {
        Avatar,
        Clothing,
        Texture,
        Gimmick,
        Accessory,
        HairStyle,
        Animation,
        Tool,
        Shader,
        Custom,
        Unknown,
    }

    /// <summary>
    /// AvatarExplorer V2のカテゴリー
    /// </summary>
    public sealed class AvatarExplorerV2Category
    {
        public int Type { get; set; }
        public string CustomCategory { get; set; } = "";
    }

    /// <summary>
    /// AvatarExplorerV2のアイテムモデル
    /// </summary>
    public class AvatarExplorerV2Item : IDatabaseItem
    {
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string ItemMemo { get; set; } = "";
        public string ItemPath { get; set; } = "";
        public string[] ItemPaths { get; set; } = Array.Empty<string>();
        public string ThumbnailFileName { get; set; } = "";
        public AvatarExplorerV2Category Category { get; set; } = new AvatarExplorerV2Category();
        public string[] SupportedAvatars { get; set; } = Array.Empty<string>();
        public string[] ImplementedAvatars { get; set; } = Array.Empty<string>();
        public int BoothId { get; set; } = -1;
        public string AuthorId { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.MinValue;
        public DateTime UpdatedDate { get; set; } = DateTime.MinValue;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string Id { get; set; } = "";

        // 基底モデルが持つ旧形式の値へ変換するための既定値。
        public string MaterialPath { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";

        public string GetTitle() => Title;
        public string GetAuthor() => Author;
        public string GetMemo() => ItemMemo;
        public string GetItemPath() => Path.GetFullPath(ItemPath);
        public string[] GetItemPaths()
        {
            return new[] { ItemPath }
                .Concat(ItemPaths ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string GetImagePath()
        {
            var thumbnailDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Avatar Explorer V2",
                "images",
                "item_thumbnails"
            );
            return Path.GetFullPath(Path.Combine(thumbnailDir, ThumbnailFileName));
        }

        public string[] GetSupportedAvatars() => SupportedAvatars ?? Array.Empty<string>();
        public int GetBoothId() => BoothId;
        public string GetCategory() => GetAECategoryName();
        public string[] GetTags() => Tags ?? Array.Empty<string>();
        public DateTime GetCreatedDate() => TimeZoneInfo.ConvertTimeToUtc(CreatedDate, TimeZoneInfo.Local);
        public DateTime GetUpdatedDate() => TimeZoneInfo.ConvertTimeToUtc(UpdatedDate, TimeZoneInfo.Local);

        public string GetAECategoryName()
            => GetCategoryNameByType((AvatarExplorerV2ItemType)(Category?.Type ?? 0));

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
                AvatarExplorerV2ItemType.Custom => Category?.CustomCategory ?? "",
                _ => LocalizationService.Instance.GetString("category_unknown")
            };
        }

        public AvatarExplorerItem ToBaseModel()
        {
            var resolvedItemPath = TryResolvePath(GetItemPath, string.Empty);
            var resolvedImagePath = TryResolvePath(GetImagePath, string.Empty);

            return new AvatarExplorerItem
            {
                Title = Title,
                AuthorName = Author,
                ItemMemo = ItemMemo,
                ItemPath = resolvedItemPath,
                ItemPaths = (ItemPaths ?? Array.Empty<string>())
                    .Select(path => TryResolvePath(() => Path.GetFullPath(path), string.Empty))
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                ImagePath = resolvedImagePath,
                MaterialPath = MaterialPath,
                SupportedAvatar = SupportedAvatars ?? Array.Empty<string>(),
                BoothId = BoothId,
                Type = Category?.Type ?? 0,
                CustomCategory = Category?.CustomCategory ?? "",
                AuthorId = AuthorId,
                ThumbnailUrl = ThumbnailUrl,
                CreatedDate = CreatedDate,
                UpdatedDate = UpdatedDate,
                Tags = Tags ?? Array.Empty<string>(),
            };
        }

        private static string TryResolvePath(Func<string> resolver, string fallback)
        {
            try
            {
                return resolver();
            }
            catch
            {
                return fallback;
            }
        }
    }
    #endregion
}
