// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using System.Linq;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Models;

namespace UnityEditorAssetBrowser.Services
{
    /// <summary>
    /// アイテム検索を支援するサービスクラス
    /// 基本検索と詳細検索の機能を提供し、アイテムの検索条件に基づいたフィルタリングを行う
    /// </summary>
    public class ItemSearchService
    {
        private readonly AvatarExplorerDatabase? _aeDatabase;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="aeDatabase">AvatarExplorerデータベース（オプション）</param>
        public ItemSearchService(AvatarExplorerDatabase? aeDatabase = null)
        {
            _aeDatabase = aeDatabase;
        }

        /// <summary>
        /// アイテムが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="criteria">検索条件</param>
        /// <param name="tabIndex">現在のタブインデックス</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        public bool IsItemMatchSearch(IDatabaseItem item, SearchCriteria criteria, int tabIndex = 0)
        {
            if (criteria == null) return true;

            // 基本検索
            if (!string.IsNullOrEmpty(criteria.SearchQuery))
            {
                bool basic = IsBasicSearchMatch(item, criteria.GetKeywords(), tabIndex);
                if (!basic) return false;
            }

            // 詳細検索
            if (criteria.ShowAdvancedSearch)
            {
                bool adv = IsAdvancedSearchMatch(item, criteria);
                if (!adv) return false;
            }

            return true;
        }

        /// <summary>
        /// 基本検索の条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keywords">検索キーワード</param>
        /// <param name="tabIndex">現在のタブインデックス</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsBasicSearchMatch(IDatabaseItem item, string[] keywords, int tabIndex)
        {
            foreach (var keyword in keywords)
            {
                bool isExclusion = keyword.StartsWith("-") && keyword.Length > 1;
                string searchKeyword = isExclusion ? keyword.Substring(1) : keyword;
                
                bool matchesKeyword = false;

                // タイトル
                if (item.GetTitle().Contains(searchKeyword, StringComparison.InvariantCultureIgnoreCase))
                    matchesKeyword = true;

                // 作者名
                if (!matchesKeyword && item.GetAuthor().Contains(searchKeyword, StringComparison.InvariantCultureIgnoreCase))
                    matchesKeyword = true;

                // カテゴリ（アバタータブはスキップ）
                if (!matchesKeyword && tabIndex != 0 && IsCategoryMatch(item, searchKeyword))
                    matchesKeyword = true;

                // 対応アバター（アイテムタブのみ判定）
                if (!matchesKeyword && tabIndex == 1 && IsSupportedAvatarsMatch(item, searchKeyword))
                    matchesKeyword = true;

                // タグ
                if (!matchesKeyword && IsTagsMatch(item, searchKeyword))
                    matchesKeyword = true;

                // メモ
                if (!matchesKeyword && IsMemoMatch(item, searchKeyword))
                    matchesKeyword = true;

                if (isExclusion)
                {
                   // 除外キーワードの場合：一致したら除外（falseを返す）
                   if (matchesKeyword) return false;
                }
                else
                {
                   // 通常キーワードの場合：一致しなかったら除外（falseを返す）
                   if (!matchesKeyword) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 詳細検索の条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="criteria">検索条件</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsAdvancedSearchMatch(IDatabaseItem item, SearchCriteria criteria)
        {
            // タイトル検索
            if (!string.IsNullOrEmpty(criteria.TitleSearch) && !IsTitleMatch(item, criteria.GetTitleKeywords()))
                return false;

            // 作者名検索
            if (!string.IsNullOrEmpty(criteria.AuthorSearch) && !IsAuthorMatch(item, criteria.GetAuthorKeywords()))
                return false;

            // カテゴリ検索
            if (!string.IsNullOrEmpty(criteria.CategorySearch) && !IsCategoryMatch(item, criteria.GetCategoryKeywords()))
                return false;

            // 対応アバター検索
            if (!string.IsNullOrEmpty(criteria.SupportedAvatarsSearch) && !IsSupportedAvatarsMatch(item, criteria.GetSupportedAvatarsKeywords()))
                return false;

            // タグ検索
            if (!string.IsNullOrEmpty(criteria.TagsSearch) && !IsTagsMatch(item, criteria.GetTagsKeywords()))
                return false;

            // メモ検索
            if (!string.IsNullOrEmpty(criteria.MemoSearch) && !IsMemoMatch(item, criteria.GetMemoKeywords()))
                return false;

            return true;
        }

        /// <summary>
        /// タイトルが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keywords">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsTitleMatch(IDatabaseItem item, string[] keywords)
        {
            string itemTitle = item.GetTitle();

            foreach (var keyword in keywords)
            {
                bool isExclusion = keyword.StartsWith("-") && keyword.Length > 1;
                string searchKeyword = isExclusion ? keyword.Substring(1) : keyword;
                
                bool match = itemTitle.Contains(searchKeyword, StringComparison.InvariantCultureIgnoreCase);
                
                if (isExclusion)
                {
                    if (match) return false;
                }
                else
                {
                    if (!match) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 作者名が検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keywords">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsAuthorMatch(IDatabaseItem item, string[] keywords)
        {
            string authorName = item.GetAuthor();

            foreach (var keyword in keywords)
            {
                bool isExclusion = keyword.StartsWith("-") && keyword.Length > 1;
                string searchKeyword = isExclusion ? keyword.Substring(1) : keyword;
                
                bool match = authorName.Contains(searchKeyword, StringComparison.InvariantCultureIgnoreCase);
                
                if (isExclusion)
                {
                    if (match) return false;
                }
                else
                {
                    if (!match) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// カテゴリが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keywords">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsCategoryMatch(IDatabaseItem item, string[] keywords)
        {
            string categoryName = item.GetCategory();

            foreach (var keyword in keywords)
            {
                bool isExclusion = keyword.StartsWith("-") && keyword.Length > 1;
                string searchKeyword = isExclusion ? keyword.Substring(1) : keyword;
                
                bool match = categoryName.Contains(searchKeyword, StringComparison.InvariantCultureIgnoreCase);
                
                if (isExclusion)
                {
                    if (match) return false;
                }
                else
                {
                    if (!match) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// カテゴリが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keyword">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsCategoryMatch(IDatabaseItem item, string keyword)
        {
            string categoryName = item.GetCategory();

            return categoryName.Contains(keyword, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// 対応アバターが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keywords">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsSupportedAvatarsMatch(IDatabaseItem item, string[] keywords)
        {
            var supportedAvatars = item.GetSupportedAvatars();

            foreach (var keyword in keywords)
            {
                bool isExclusion = keyword.StartsWith("-") && keyword.Length > 1;
                string searchKeyword = isExclusion ? keyword.Substring(1) : keyword;
                
                bool match = supportedAvatars.Any(avatar => avatar.Contains(searchKeyword, StringComparison.InvariantCultureIgnoreCase));
                
                if (isExclusion)
                {
                    if (match) return false;
                }
                else
                {
                    if (!match) return false;
                }
            }
            return true;
        }
        
        /// <summary>
        /// 対応アバターが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keyword">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsSupportedAvatarsMatch(IDatabaseItem item, string keyword)
        {
            var supportedAvatars = item.GetSupportedAvatars();

            // キーワードが少なくとも1つの対応アバターに含まれていることを確認
            return supportedAvatars.Any(avatar => avatar.Contains(keyword, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// タグが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keywords">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsTagsMatch(IDatabaseItem item, string[] keywords)
        {
            string[] tags = item.GetTags();

            foreach (var keyword in keywords)
            {
                bool isExclusion = keyword.StartsWith("-") && keyword.Length > 1;
                string searchKeyword = isExclusion ? keyword.Substring(1) : keyword;
                
                bool match = false;
                if (tags.Length > 0)
                {
                    match = tags.Any(tag => tag.Contains(searchKeyword, StringComparison.InvariantCultureIgnoreCase));
                }
                
                if (isExclusion)
                {
                    if (match) return false;
                }
                else
                {
                    if (!match) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// タグが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keyword">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsTagsMatch(IDatabaseItem item, string keyword)
        {
            string[] tags = item.GetTags();
            if (tags.Length == 0) return false;

            // キーワードが少なくとも1つのタグに含まれていることを確認
            return tags.Any(tag => tag.Contains(keyword, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// メモが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keywords">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsMemoMatch(IDatabaseItem item, string[] keywords)
        {
            string memo = item.GetMemo();
            
            foreach (var keyword in keywords)
            {
                bool isExclusion = keyword.StartsWith("-") && keyword.Length > 1;
                string searchKeyword = isExclusion ? keyword.Substring(1) : keyword;
                
                bool match = false;
                if (!string.IsNullOrEmpty(memo))
                {
                    match = memo.Contains(searchKeyword, StringComparison.InvariantCultureIgnoreCase);
                }

                if (isExclusion)
                {
                    if (match) return false;
                }
                else
                {
                    if (!match) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// メモが検索条件に一致するか判定する
        /// </summary>
        /// <param name="item">判定するアイテム</param>
        /// <param name="keyword">検索キーワード</param>
        /// <returns>検索条件に一致する場合はtrue、それ以外はfalse</returns>
        private bool IsMemoMatch(IDatabaseItem item, string keyword)
        {
            string memo = item.GetMemo();
            if (string.IsNullOrEmpty(memo)) return false;

            // キーワードがメモに含まれていることを確認
            return memo.Contains(keyword, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// データベースがNullかどうかチェックします
        /// </summary>
        /// <returns></returns>
        public bool IsDatabaseNull()
            => _aeDatabase == null;
    }
}
