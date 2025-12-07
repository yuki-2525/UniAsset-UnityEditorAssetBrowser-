using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditorAssetBrowser.Models;
using Newtonsoft.Json;

namespace UnityEditorAssetBrowser.Services
{
    public class LocalizationService
    {
        private static LocalizationService _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        private Dictionary<string, Dictionary<string, string>> _localizedText;
        private string _currentLanguage = "ja-JP";
        private const string PREF_KEY = "UniAsset_Language";
        private const string DEFAULT_LANGUAGE = "ja-JP";
        private const string FALLBACK_LANGUAGE = "en-US";

        public event Action OnLanguageChanged;

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    EditorPrefs.SetString(PREF_KEY, value);
                    OnLanguageChanged?.Invoke();
                }
            }
        }

        public string[] AvailableLanguages => _localizedText?.Keys.OrderBy(k => k).ToArray() ?? new string[] { DEFAULT_LANGUAGE };

        private LocalizationService()
        {
            var savedLang = EditorPrefs.GetString(PREF_KEY, DEFAULT_LANGUAGE);
            _currentLanguage = savedLang;
            LoadLocalizationData();
        }

        public void LoadLocalizationData()
        {
            _localizedText = new Dictionary<string, Dictionary<string, string>>();

            var guids = AssetDatabase.FindAssets("LocalizationService t:MonoScript");
            if (guids.Length == 0)
            {
                Debug.LogError("[UniAsset] LocalizationService script not found.");
                return;
            }
            var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var editorDir = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)); // Editor folder
            var localesDir = Path.Combine(editorDir, "_locales");

            // Normalize path for AssetDatabase
            localesDir = localesDir.Replace("\\", "/");

            if (AssetDatabase.IsValidFolder(localesDir))
            {
                var jsonGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { localesDir });
                foreach (var guid in jsonGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".json")) continue;

                    try
                    {
                        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                        if (textAsset != null)
                        {
                            var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(textAsset.text);
                            if (dict != null)
                            {
                                var langCode = Path.GetFileNameWithoutExtension(path);
                                _localizedText[langCode] = dict;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[UniAsset] Failed to load localization file {path}: {e.Message}");
                    }
                }
            }
        }

        public string GetString(string key)
        {
            if (_localizedText != null && 
                _localizedText.TryGetValue(_currentLanguage, out var dict) && 
                dict.TryGetValue(key, out var value))
            {
                return value;
            }
            
            if (_currentLanguage != FALLBACK_LANGUAGE && 
                _localizedText != null && 
                _localizedText.TryGetValue(FALLBACK_LANGUAGE, out var enDict) && 
                enDict.TryGetValue(key, out var enValue))
            {
                return enValue;
            }

            return key;
        }
    }

    public class LocalizationPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool changed = false;
            foreach (var path in importedAssets.Concat(deletedAssets).Concat(movedAssets))
            {
                if (path.Contains("_locales") && path.EndsWith(".json"))
                {
                    changed = true;
                    break;
                }
            }

            if (changed)
            {
                LocalizationService.Instance.LoadLocalizationData();
            }
        }
    }
}
