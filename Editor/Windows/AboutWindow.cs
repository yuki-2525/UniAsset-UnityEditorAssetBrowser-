using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditorAssetBrowser.Services;
using UnityEditorAssetBrowser.Helper;

namespace UnityEditorAssetBrowser.Windows
{
    public class AboutWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string _version = "Unknown";
        private const string RepoUrl = "https://github.com/yuki-2525/UniAsset-UnityEditorAssetBrowser-";
        private const string DiscordUrl = "https://discord.gg/6gvucjC4FE";
        private const string DeveloperXUrl = "https://x.com/sakurayuki_dev";
        private const string FanboxUrl = "https://sakurayuki-dev.fanbox.cc/";

        // 支援者様リスト
        private readonly string[] _supporters = new string[]
        {
            "シャル様"
        };

        public static void ShowWindow()
        {
            var w = GetWindow<AboutWindow>(true, LocalizationService.Instance.GetString("about_window_title"), true);
            w.minSize = new Vector2(400, 220);
            w.Show();
        }

        private void OnEnable()
        {
            LoadPackageInfo();
        }

        private void LoadPackageInfo()
        {
            try
            {
                // VersionUpdateService の外部 API を利用してバージョンとリポジトリ URL を取得
                var version = VersionUpdateService.External.GetVersion();
                if (!string.IsNullOrEmpty(version)) _version = version;
            }
            catch (System.Exception)
            {
                
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Space(8);
            EditorGUILayout.LabelField("UniAsset -UnityEditorAssetBrowser-", GUIStyleManager.BoldLabel);
            EditorGUILayout.LabelField($"{LocalizationService.Instance.GetString("version")}: {_version}", GUIStyleManager.Label);
            EditorGUILayout.LabelField($"{LocalizationService.Instance.GetString("developer")}: sakurayuki", GUIStyleManager.Label);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("description_label"), GUIStyleManager.BoldLabel);
            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("description_text"), GUIStyleManager.WordWrappedLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("license_label"), GUIStyleManager.BoldLabel);
            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("license_text"), GUIStyleManager.WordWrappedLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("github_repo_label"), GUIStyleManager.BoldLabel);
            if (GUILayout.Button(new GUIContent(LocalizationService.Instance.GetString("open_github"), "GitHub リポジトリをブラウザで開きます"), GUIStyleManager.Button, GUILayout.Width(180)))
            {
                Application.OpenURL(RepoUrl);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("discord_server_label"), GUIStyleManager.BoldLabel);
            if (GUILayout.Button(new GUIContent(LocalizationService.Instance.GetString("open_discord"), "サポート用 Discord サーバーに移動します"), GUIStyleManager.Button, GUILayout.Width(180)))
            {
                Application.OpenURL(DiscordUrl);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("developer_x_label"), GUIStyleManager.BoldLabel);
            if (GUILayout.Button(new GUIContent(LocalizationService.Instance.GetString("open_x"), "開発者の X (旧Twitter) ページを開きます"), GUIStyleManager.Button, GUILayout.Width(180)))
            {
                Application.OpenURL(DeveloperXUrl);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("support_request_label"), GUIStyleManager.BoldLabel);
            if (GUILayout.Button(new GUIContent(LocalizationService.Instance.GetString("open_support_page"), "支援ページ (Fanbox) をブラウザで開きます"), GUIStyleManager.Button, GUILayout.Width(180)))
            {
                Application.OpenURL(FanboxUrl);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("support_message"), GUIStyleManager.WordWrappedLabel);

            DrawSupporters();

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSupporters()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(LocalizationService.Instance.GetString("special_thanks"), GUIStyleManager.BoldLabel);
            
            EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);
            
            string text = string.Join(", ", _supporters);
            EditorGUILayout.LabelField(text, GUIStyleManager.WordWrappedLabel);
            
            EditorGUILayout.EndVertical();
        }
    }
}