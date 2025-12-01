using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditorAssetBrowser.Services;

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
            EditorGUILayout.LabelField("UniAsset -UnityEditorAssetBrowser-", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"バージョン: {_version}");
            EditorGUILayout.LabelField("開発者: sakurayuki");
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("概要:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Avatar ExplorerとKonoAssetによって保存されているアイテムを検索・表示し、簡単にインポートすることが出来るエディタ拡張です。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("ライセンス:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("このソフトはMIT ライセンスの下で配布されています。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GitHubリポジトリ:", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("GitHubを開く", "GitHub リポジトリをブラウザで開きます"), GUILayout.Width(180)))
            {
                Application.OpenURL(RepoUrl);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("サポート Discord サーバー:", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Discordを開く", "サポート用 Discord サーバーに移動します"), GUILayout.Width(180)))
            {
                Application.OpenURL(DiscordUrl);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("開発者X:", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Xを開く", "開発者の X (旧Twitter) ページを開きます"), GUILayout.Width(180)))
            {
                Application.OpenURL(DeveloperXUrl);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("支援のお願い:", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("支援ページを開く", "支援ページ (Fanbox) をブラウザで開きます"), GUILayout.Width(180)))
            {
                Application.OpenURL(FanboxUrl);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("みなさんの支援が開発のモチベーションとなります！", EditorStyles.wordWrappedLabel);

            DrawSupporters();

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSupporters()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Special Thanks (支援者のみなさま):", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            string text = string.Join(", ", _supporters);
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.EndVertical();
        }
    }
}