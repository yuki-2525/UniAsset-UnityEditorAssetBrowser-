// Copyright (c) 2025-2026 sakurayuki
// Copyright (c) 2025-2026 sakurayuki

using System;
using System.Collections.Generic;

namespace UnityEditorAssetBrowser.Models
{
    [Serializable]
    public class LocalizationData
    {
        public List<LanguageData> languages = new List<LanguageData>();
    }

    [Serializable]
    public class LanguageData
    {
        public string languageCode;
        public List<KeyValue> items = new List<KeyValue>();
    }

    [Serializable]
    public class KeyValue
    {
        public string key;
        public string value;
    }
}
