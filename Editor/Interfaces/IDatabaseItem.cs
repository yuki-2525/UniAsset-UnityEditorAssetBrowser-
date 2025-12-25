// Copyright (c) 2025 sakurayuki
// Code Written by puk06

using System;

namespace UnityEditorAssetBrowser.Interfaces
{
    /// <summary>
    /// 全てのデータベースに共通するアイテム用のInterfaceです
    /// </summary>
    public interface IDatabaseItem
    {
        /// <summary>
        /// アイテムのタイトルを取得します
        /// </summary>
        /// <returns></returns>
        public string GetTitle();

        /// <summary>
        /// 作者名を取得します
        /// </summary>
        /// <returns></returns>
        public string GetAuthor();

        /// <summary>
        /// アイテムメモを取得します
        /// </summary>
        /// <returns></returns>
        public string GetMemo();

        /// <summary>
        /// アイテムのフルパスを取得します
        /// </summary>
        /// <returns></returns>
        public string GetItemPath();

        /// <summary>
        /// アイテムのサムネイルのフルパスを取得します
        /// </summary>
        /// <returns></returns>
        public string GetImagePath();

        /// <summary>
        /// 対応アバターの配列を取得します
        /// </summary>
        /// <returns></returns>
        public string[] GetSupportedAvatars();

        /// <summary>
        /// Boothの商品IDを取得します
        /// </summary>
        /// <returns></returns>
        public int GetBoothId();

        /// <summary>
        /// カテゴリ名を取得します
        /// </summary>
        /// <returns></returns>
        public string GetCategory();

        /// <summary>
        /// タグの配列を取得します
        /// </summary>
        /// <returns></returns>
        public string[] GetTags();

        /// <summary>
        /// アイテムがデータベースに登録された日時を取得します
        /// </summary>
        /// <returns></returns>
        public DateTime GetCreatedDate();

        /// <summary>
        /// アイテムの更新日時を取得します
        /// </summary>
        /// <returns></returns>
        public DateTime GetUpdatedDate();
    }
}
