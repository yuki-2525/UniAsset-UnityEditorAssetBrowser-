// Copyright (c) 2025-2026 sakurayuki

#nullable enable

using System;
using UnityEditorAssetBrowser.Interfaces;
using UnityEditorAssetBrowser.Models;

namespace UnityEditorAssetBrowser.Services
{
    internal readonly struct SearchTerm
    {
        public readonly string Value;
        public readonly bool IsExclusion;

        public SearchTerm(string value)
        {
            IsExclusion = value.Length > 1 && value[0] == '-';
            Value = IsExclusion ? value.Substring(1) : value;
        }
    }

    internal sealed class CompiledSearchCriteria
    {
        public readonly bool ShowAdvancedSearch;
        public readonly SearchTerm[] Basic;
        public readonly SearchTerm[] Title;
        public readonly SearchTerm[] Author;
        public readonly SearchTerm[] Category;
        public readonly SearchTerm[] SupportedAvatars;
        public readonly SearchTerm[] Tags;
        public readonly SearchTerm[] Memo;

        public CompiledSearchCriteria(SearchCriteria criteria)
        {
            ShowAdvancedSearch = criteria.ShowAdvancedSearch;
            Basic = Compile(criteria.GetKeywords());
            Title = Compile(criteria.GetTitleKeywords());
            Author = Compile(criteria.GetAuthorKeywords());
            Category = Compile(criteria.GetCategoryKeywords());
            SupportedAvatars = Compile(criteria.GetSupportedAvatarsKeywords());
            Tags = Compile(criteria.GetTagsKeywords());
            Memo = Compile(criteria.GetMemoKeywords());
        }

        private static SearchTerm[] Compile(string[] values)
        {
            var result = new SearchTerm[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = new SearchTerm(values[i]);
            return result;
        }
    }

    public class ItemSearchService
    {
        private readonly AvatarExplorerDatabase? _aeDatabase;
        private SearchCriteria? _lastCriteria;
        private int _lastVersion = -1;
        private CompiledSearchCriteria? _compiled;

        public ItemSearchService(AvatarExplorerDatabase? aeDatabase = null)
        {
            _aeDatabase = aeDatabase;
        }

        public bool IsItemMatchSearch(IDatabaseItem item, SearchCriteria criteria, int tabIndex = 0)
        {
            var compiled = GetCompiled(criteria);
            if (!MatchesBasic(item, compiled.Basic, tabIndex)) return false;
            if (!compiled.ShowAdvancedSearch) return true;

            return MatchesText(item.GetTitle(), compiled.Title) &&
                   MatchesText(item.GetAuthor(), compiled.Author) &&
                   MatchesText(item.GetCategory(), compiled.Category) &&
                   MatchesValues(item.GetSupportedAvatars(), compiled.SupportedAvatars) &&
                   MatchesValues(item.GetTags(), compiled.Tags) &&
                   MatchesText(item.GetMemo(), compiled.Memo);
        }

        private CompiledSearchCriteria GetCompiled(SearchCriteria criteria)
        {
            if (!ReferenceEquals(_lastCriteria, criteria) || _lastVersion != criteria.Version || _compiled == null)
            {
                _lastCriteria = criteria;
                _lastVersion = criteria.Version;
                _compiled = new CompiledSearchCriteria(criteria);
            }
            return _compiled;
        }

        private static bool MatchesBasic(IDatabaseItem item, SearchTerm[] terms, int tabIndex)
        {
            foreach (var term in terms)
            {
                bool match = Contains(item.GetTitle(), term.Value) ||
                             Contains(item.GetAuthor(), term.Value) ||
                             (tabIndex != 0 && Contains(item.GetCategory(), term.Value)) ||
                             (tabIndex == 1 && ContainsAny(item.GetSupportedAvatars(), term.Value)) ||
                             ContainsAny(item.GetTags(), term.Value) ||
                             Contains(item.GetMemo(), term.Value);
                if (term.IsExclusion ? match : !match) return false;
            }
            return true;
        }

        private static bool MatchesText(string value, SearchTerm[] terms)
        {
            foreach (var term in terms)
            {
                bool match = Contains(value, term.Value);
                if (term.IsExclusion ? match : !match) return false;
            }
            return true;
        }

        private static bool MatchesValues(string[] values, SearchTerm[] terms)
        {
            foreach (var term in terms)
            {
                bool match = ContainsAny(values, term.Value);
                if (term.IsExclusion ? match : !match) return false;
            }
            return true;
        }

        private static bool ContainsAny(string[] values, string term)
        {
            for (int i = 0; i < values.Length; i++)
                if (Contains(values[i], term)) return true;
            return false;
        }

        private static bool Contains(string? value, string term)
            => !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.InvariantCultureIgnoreCase);

        public bool IsDatabaseNull() => _aeDatabase == null;
    }
}
