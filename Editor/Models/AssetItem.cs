// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;

namespace UnityEditorAssetBrowser.Models
{
    /// <summary>
    /// アセットアイテムの情報を管理するクラス
    /// 様々な形式のアセットアイテムから共通の情報を取得する機能を提供する
    /// </summary>
    public class AssetItem
    {
        /// <summary>
        /// ワールドカテゴリーの日本語名
        /// </summary>
        private const string WORLD_CATEGORY_JP = "ワールド";

        /// <summary>
        /// ワールドカテゴリーの英語名
        /// </summary>
        private const string WORLD_CATEGORY_EN = "world";

        /// <summary>
        /// カテゴリーがワールド関連かどうかを判定
        /// </summary>
        /// <param name="category">判定するカテゴリー名</param>
        /// <returns>ワールド関連のカテゴリーの場合はtrue、それ以外はfalse</returns>
        public static bool IsWorldCategory(string category)
            => category.Contains(WORLD_CATEGORY_JP, StringComparison.OrdinalIgnoreCase) || category.Contains(WORLD_CATEGORY_EN, StringComparison.OrdinalIgnoreCase);
    }
}
