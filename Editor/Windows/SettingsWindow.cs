// Copyright (c) 2025-2026 sakurayuki

using System.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Services;
using UnityEditorAssetBrowser.Helper; // Added
using UnityEditorAssetBrowser.ViewModels;
using UnityEditorAssetBrowser.Views;
using UnityEngine;

namespace UnityEditorAssetBrowser.Windows
{
    public class SettingsWindow : EditorWindow, IHasCustomMenu
    {
        private SettingsView _settingsView;
        private AssetBrowserViewModel _assetBrowserViewModel;
        private SearchViewModel _searchViewModel;
        private PaginationViewModel _paginationViewModel;

        public static void ShowWindow(
            AssetBrowserViewModel assetBrowserViewModel,
            SearchViewModel searchViewModel,
            PaginationViewModel paginationViewModel
        )
        {
            DebugLogger.Log("Opening SettingsWindow.");
            var window = GetWindow<SettingsWindow>(LocalizationService.Instance.GetString("settings_window_title"));
            window.minSize = new Vector2(400, 200);
            window._assetBrowserViewModel = assetBrowserViewModel;
            window._searchViewModel = searchViewModel;
            window._paginationViewModel = paginationViewModel;

            // DatabaseServiceにViewModelの参照を設定
            DatabaseService.SetViewModels(
                assetBrowserViewModel,
                searchViewModel,
                paginationViewModel
            );
        }

        private void OnEnable()
        {
            _settingsView = new SettingsView(
                DatabaseService.OnAEDatabasePathChanged,
                DatabaseService.OnKADatabasePathChanged,
                DatabaseService.OnBOOTHLMDatabasePathChanged
            );

            _settingsView.OnSettingsChanged += () =>
            {
                _assetBrowserViewModel?.InvalidateCategoryAssetTypeCache();
                var window = Resources.FindObjectsOfTypeAll<UnityEditorAssetBrowser>().FirstOrDefault();
                if (window != null)
                {
                    window.Repaint();
                }
            };
            DatabaseService.OnPathChanged += OnDatabasePathChanged;
        }

        private void OnDisable()
        {
            DatabaseService.OnPathChanged -= OnDatabasePathChanged;
        }

        private void OnDatabasePathChanged()
        {
            _settingsView?.RefreshDatabaseSummaries();
            Repaint();
        }

        private void OnGUI()
        {
            _settingsView.Draw();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            bool isDebug = DebugLogger.IsDebugMode;

            menu.AddItem(new GUIContent("Debug Mode"), isDebug, () =>
            {
                bool newState = !isDebug;
                DebugLogger.SetDebugMode(newState);
                Debug.Log($"[UniAsset][Debug] Debug Mode toggled: {(newState ? "ON" : "OFF")}");
            });
        }
    }
}
