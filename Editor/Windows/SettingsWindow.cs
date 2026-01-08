// Copyright (c) 2025 sakurayuki

using System.Linq;
using UnityEditor;
using UnityEditorAssetBrowser.Services;
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
                var window = Resources.FindObjectsOfTypeAll<UnityEditorAssetBrowser>().FirstOrDefault();
                if (window != null)
                {
                    window.Repaint();
                }
            };
        }

        private void OnGUI()
        {
            _settingsView.Draw();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            const string debugKey = "UniAsset_DebugMode";
            bool isDebug = EditorPrefs.GetBool(debugKey, false);

            menu.AddItem(new GUIContent("Debug Mode"), isDebug, () =>
            {
                bool newState = !isDebug;
                EditorPrefs.SetBool(debugKey, newState);
                Debug.Log($"[UniAsset] Debug Mode: {(newState ? "ON" : "OFF")}");
            });
        }
    }
}
