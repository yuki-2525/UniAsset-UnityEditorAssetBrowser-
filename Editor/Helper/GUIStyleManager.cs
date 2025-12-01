// Copyright (c) 2025 sakurayuki

#nullable enable

using UnityEditor;
using UnityEngine;

namespace UnityEditorAssetBrowser.Helper
{
    /// <summary>
    /// Unity EditorのGUIスタイルを管理するクラス
    /// 共通で使用するスタイルをキャッシュし、効率的に提供する
    /// </summary>
    public static class GUIStyleManager
    {
        private const string PREFS_KEY_ICON_SIZE = "UnityEditorAssetBrowser_IconSize";
        private const string PREFS_KEY_FONT_SIZE = "UnityEditorAssetBrowser_FontSize";

        private static int _currentFontSize = -1;
        
        private static GUIStyle? _titleStyle;
        private static GUIStyle? _boxStyle;
        private static GUIStyle? _labelStyle;
        private static GUIStyle? _boldLabelStyle;
        private static GUIStyle? _wordWrappedLabelStyle;
        private static GUIStyle? _buttonStyle;
        private static GUIStyle? _textFieldStyle;
        private static GUIStyle? _popupStyle;
        private static GUIStyle? _foldoutStyle;
        private static GUIStyle? _tabButtonStyle;

        /// <summary>
        /// 現在設定されているアイコンサイズ
        /// </summary>
        public static int IconSize => EditorPrefs.GetInt(PREFS_KEY_ICON_SIZE, 210);

        /// <summary>
        /// 現在設定されているフォントサイズ
        /// </summary>
        public static int FontSize => EditorPrefs.GetInt(PREFS_KEY_FONT_SIZE, 13);

        /// <summary>
        /// スタイルが現在のフォントサイズと一致しているか確認し、必要ならリセットする
        /// </summary>
        private static void EnsureStyles()
        {
            int size = FontSize;
            if (_currentFontSize != size)
            {
                _currentFontSize = size;
                _titleStyle = null;
                _boxStyle = null;
                _labelStyle = null;
                _boldLabelStyle = null;
                _wordWrappedLabelStyle = null;
                _buttonStyle = null;
                _textFieldStyle = null;
                _popupStyle = null;
                _foldoutStyle = null;
                _tabButtonStyle = null;
            }
        }

        /// <summary>
        /// 通常のラベルスタイル
        /// </summary>
        public static GUIStyle Label
        {
            get
            {
                EnsureStyles();
                _labelStyle ??= new GUIStyle(EditorStyles.label)
                {
                    fontSize = _currentFontSize
                };
                return _labelStyle;
            }
        }

        /// <summary>
        /// 太字ラベルスタイル
        /// </summary>
        public static GUIStyle BoldLabel
        {
            get
            {
                EnsureStyles();
                _boldLabelStyle ??= new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = _currentFontSize
                };
                return _boldLabelStyle;
            }
        }

        /// <summary>
        /// 折り返しラベルスタイル
        /// </summary>
        public static GUIStyle WordWrappedLabel
        {
            get
            {
                EnsureStyles();
                _wordWrappedLabelStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = _currentFontSize
                };
                return _wordWrappedLabelStyle;
            }
        }

        /// <summary>
        /// ボタンスタイル
        /// </summary>
        public static GUIStyle Button
        {
            get
            {
                EnsureStyles();
                _buttonStyle ??= new GUIStyle(GUI.skin.button)
                {
                    fontSize = _currentFontSize
                };
                return _buttonStyle;
            }
        }

        /// <summary>
        /// テキストフィールドスタイル
        /// </summary>
        public static GUIStyle TextField
        {
            get
            {
                EnsureStyles();
                _textFieldStyle ??= new GUIStyle(EditorStyles.textField)
                {
                    fontSize = _currentFontSize
                };
                return _textFieldStyle;
            }
        }

        /// <summary>
        /// ポップアップスタイル
        /// </summary>
        public static GUIStyle Popup
        {
            get
            {
                EnsureStyles();
                _popupStyle ??= new GUIStyle(EditorStyles.popup)
                {
                    fontSize = _currentFontSize
                };
                return _popupStyle;
            }
        }

        /// <summary>
        /// フォールドアウトスタイル
        /// </summary>
        public static GUIStyle Foldout
        {
            get
            {
                EnsureStyles();
                _foldoutStyle ??= new GUIStyle(EditorStyles.foldout)
                {
                    fontSize = _currentFontSize
                };
                return _foldoutStyle;
            }
        }

        /// <summary>
        /// タブバー用のボタンスタイル
        /// </summary>
        public static GUIStyle TabButton
        {
            get
            {
                EnsureStyles();
                _tabButtonStyle ??= new GUIStyle(GUI.skin.button)
                {
                    fontSize = _currentFontSize,
                    fixedHeight = _currentFontSize + 12,
                    alignment = TextAnchor.MiddleCenter
                };
                return _tabButtonStyle;
            }
        }

        /// <summary>
        /// タイトル用のスタイル
        /// 太字で設定フォントサイズ+2ptのフォントサイズを使用し、適切なマージンを設定
        /// </summary>
        public static GUIStyle TitleStyle
        {
            get
            {
                EnsureStyles();
                _titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = _currentFontSize + 2,
                    margin = new RectOffset(4, 4, 4, 4),
                };
                
                return _titleStyle;
            }
        }

        /// <summary>
        /// ボックス用のスタイル
        /// ヘルプボックスをベースに、適切なパディングとマージンを設定
        /// </summary>
        public static GUIStyle BoxStyle
        {
            get
            {
                _boxStyle ??= new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    margin = new RectOffset(0, 0, 5, 5),
                };
                
                return _boxStyle;
            }
        }
    }
}
