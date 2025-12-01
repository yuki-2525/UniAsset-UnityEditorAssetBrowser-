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
            var w = GetWindow<AboutWindow>(true, "このソフトについて", true);
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
            EditorGUILayout.LabelField($"バージョン: {_version}", GUIStyleManager.Label);
            EditorGUILayout.LabelField("開発者: sakurayuki", GUIStyleManager.Label);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("概要:", GUIStyleManager.BoldLabel);
            EditorGUILayout.LabelField("Avatar ExplorerとKonoAssetによって保存されているアイテムを検索・表示し、簡単にインポートすることが出来るエディタ拡張です。", GUIStyleManager.WordWrappedLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("ライセンス:", GUIStyleManager.BoldLabel);
            EditorGUILayout.LabelField("このソフトはMIT ライセンスの下で配布されています。", GUIStyleManager.WordWrappedLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GitHubリポジトリ:", GUIStyleManager.BoldLabel);
            if (GUILayout.Button(new GUIContent("GitHubを開く", "GitHub リポジトリをブラウザで開きます"), GUIStyleManager.Button, GUILayout.Width(180)))
            {
                Application.OpenURL(RepoUrl);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("サポート Discord サーバー:", GUIStyleManager.BoldLabel);
            if (GUILayout.Button(new GUIContent("Discordを開く", "サポート用 Discord サーバーに移動します"), GUIStyleManager.Button, GUILayout.Width(180)))
            {
                Application.OpenURL(DiscordUrl);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("開発者X:", GUIStyleManager.BoldLabel);
            if (GUILayout.Button(new GUIContent("Xを開く", "開発者の X (旧Twitter) ページを開きます"), GUIStyleManager.Button, GUILayout.Width(180)))
            {
                Application.OpenURL(DeveloperXUrl);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("支援のお願い:", GUIStyleManager.BoldLabel);
            if (GUILayout.Button(new GUIContent("支援ページを開く", "支援ページ (Fanbox) をブラウザで開きます"), GUIStyleManager.Button, GUILayout.Width(180)))
            {
                Application.OpenURL(FanboxUrl);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("みなさんの支援が開発のモチベーションとなります！", GUIStyleManager.WordWrappedLabel);

            DrawSupporters();

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSupporters()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Special Thanks (支援者のみなさま):", GUIStyleManager.BoldLabel);
            
            EditorGUILayout.BeginVertical(GUIStyleManager.BoxStyle);
            
            string text = string.Join(", ", _supporters);
            EditorGUILayout.LabelField(text, GUIStyleManager.WordWrappedLabel);
            
            EditorGUILayout.EndVertical();
        }
    }
}