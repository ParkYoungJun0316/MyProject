#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SettingUI String Table 생성·번역 채우기 + Setting_Panel LocalizeStringEvent 부착.
/// Menu: Tools / Setup Setting UI Localization
/// </summary>
public static class SetupSettingUILocalization
{
    const string TableName = "SettingUI";
    const string TableFolder = "Assets/Localization/StringTables";

    // key → localeCode → value
    static readonly Dictionary<string, Dictionary<string, string>> Entries = BuildEntries();

    // Setting_Panel 내 라벨 경로(부모이름/TMP이름 또는 부모이름) → key
    static readonly Dictionary<string, string> LabelBindings = new Dictionary<string, string>
    {
        { "Tab_General", "Settings.Tab.General" },
        { "Tab_Sound", "Settings.Tab.Sound" },
        { "Tab_TeamVoice", "Settings.Tab.TeamVoice" },
        { "CloseButton", "Settings.Close" },
        { "Close.Btn", "Settings.Close" },
        { "Row_Resolution", "Settings.Resolution" },
        { "Row_DisplayMode", "Settings.DisplayMode" },
        { "Row_Language", "Settings.Language" },
        { "Row_MouseSensitivity", "Settings.MouseSensitivity" },
        { "Row_ChatFontSize", "Settings.ChatFontSize" },
        { "Row_DigitCheer", "Settings.DigitCheer" },
        { "Row_Master", "Settings.MasterVolume" },
        { "Row_BGM", "Settings.BgmVolume" },
        { "Row_SFX", "Settings.SfxVolume" },
        { "Row_InputDevice", "Settings.InputDevice" },
        { "Row_MicVolume", "Settings.MicVolume" },
        { "Row_MicMute", "Settings.MicMute" },
        { "Btn_Cancel", "Settings.Cancel" },
        { "Btn_Reset", "Settings.Reset" },
        { "Btn_Apply", "Settings.Apply" },
        { "EmptyState", "Settings.TeamVoice.Empty" },
    };

    [MenuItem("Tools/Setup Setting UI Localization")]
    public static void Run()
    {
        StringTableCollection collection = EnsureCollection();
        FillTranslations(collection);
        WirePanel(collection);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupSettingUILocalization] SettingUI table + LocalizeStringEvent + TeamVoice Steam slots done.");
    }

    static StringTableCollection EnsureCollection()
    {
        StringTableCollection existing = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Localization"))
            AssetDatabase.CreateFolder("Assets", "Localization");
        if (!AssetDatabase.IsValidFolder(TableFolder))
            AssetDatabase.CreateFolder("Assets/Localization", "StringTables");

        var locales = LocalizationEditorSettings.GetLocales();
        return LocalizationEditorSettings.CreateStringTableCollection(TableName, TableFolder, locales);
    }

    static void FillTranslations(StringTableCollection collection)
    {
        SharedTableData shared = collection.SharedData;
        foreach (var kv in Entries)
        {
            string key = kv.Key;
            SharedTableData.SharedTableEntry sharedEntry = shared.GetEntry(key) ?? shared.AddKey(key);

            foreach (StringTable table in collection.StringTables)
            {
                string code = table.LocaleIdentifier.Code;
                if (!kv.Value.TryGetValue(code, out string value))
                    value = kv.Value.TryGetValue("en", out string en) ? en : key;

                StringTableEntry entry = table.GetEntry(sharedEntry.Id);
                if (entry == null) table.AddEntry(sharedEntry.Id, value);
                else entry.Value = value;

                EditorUtility.SetDirty(table);
            }
        }

        EditorUtility.SetDirty(shared);
        EditorUtility.SetDirty(collection);
    }

    static void WirePanel(StringTableCollection collection)
    {
        Transform panel = FindInOpenScenes("Setting_Panel");
        if (panel == null)
        {
            Debug.LogError("[SetupSettingUILocalization] Setting_Panel not found.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Wire Setting UI Localization");

        // Rename cheer-name rows → teammate slots + empty state
        PrepareTeamVoiceRows(panel);

        // LocalizeStringEvent on static labels
        TextMeshProUGUI[] tmps = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        int wired = 0;
        for (int i = 0; i < tmps.Length; i++)
        {
            TextMeshProUGUI tmp = tmps[i];
            if (tmp.name == "Value" || tmp.name == "Item Label") continue;
            if (tmp.transform.parent != null && tmp.transform.parent.name == "Dropdown") continue;

            string bindKey = ResolveBindKey(tmp);
            if (bindKey == null) continue;

            LocalizeStringEvent loc = tmp.GetComponent<LocalizeStringEvent>();
            if (loc == null) loc = Undo.AddComponent<LocalizeStringEvent>(tmp.gameObject);

            loc.StringReference.SetReference(TableName, bindKey);

            while (loc.OnUpdateString.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(loc.OnUpdateString, 0);
            UnityEventTools.AddPersistentListener(loc.OnUpdateString, tmp.SetText);

            EditorUtility.SetDirty(loc);
            EditorUtility.SetDirty(tmp);
            wired++;
        }

        // OptionsMenuController display-mode LocalizedString fields
        OptionsMenuController options = panel.GetComponent<OptionsMenuController>();
        if (options != null)
        {
            SetLocalizedField(options, "displayModeExclusiveLabel", "Settings.DisplayMode.Exclusive");
            SetLocalizedField(options, "displayModeWindowedLabel", "Settings.DisplayMode.Windowed");
            SetLocalizedField(options, "displayModeBorderlessLabel", "Settings.DisplayMode.Borderless");
            EditorUtility.SetDirty(options);
        }

        // OptionsTeamVoicePanel wiring
        WireTeamVoicePanel(panel);

        EditorUtility.SetDirty(panel.gameObject);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Debug.Log($"[SetupSettingUILocalization] LocalizeStringEvent wired: {wired}");
    }

    static void PrepareTeamVoiceRows(Transform panel)
    {
        Transform content = FindRecursive(panel, "Content_TeamVoice");
        if (content == null) return;

        RenameIfExists(content, "Row_GumaVoice", "Row_Teammate_0");
        RenameIfExists(content, "Row_DanhoVoice", "Row_Teammate_1");
        RenameIfExists(content, "Row_SsukVoice", "Row_Teammate_2");

        Transform empty = FindChild(content, "EmptyState");
        if (empty == null)
        {
            GameObject go = new GameObject("EmptyState", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "EmptyState");
            go.transform.SetParent(content, false);
            go.layer = content.gameObject.layer;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 48f);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 48f;
            le.preferredHeight = 48f;

            GameObject textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            RectTransform tr = textGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "No teammates";
            tmp.fontSize = 22;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/Noto/NotoSansKR-Regular SDF.asset");
            if (font != null) tmp.font = font;
            empty = go.transform;
        }

        // Clear cheer-name placeholder text on teammate labels (runtime fills Steam names)
        for (int i = 0; i < 3; i++)
        {
            Transform row = FindChild(content, "Row_Teammate_" + i);
            if (row == null) continue;
            Transform label = FindChild(row, "Label");
            if (label == null) continue;
            TextMeshProUGUI tmp = label.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = "—";
            // Remove LocalizeStringEvent if previously attached
            LocalizeStringEvent old = label.GetComponent<LocalizeStringEvent>();
            if (old != null) Undo.DestroyObjectImmediate(old);
        }
    }

    static void WireTeamVoicePanel(Transform panel)
    {
        Transform content = FindRecursive(panel, "Content_TeamVoice");
        if (content == null) return;

        OptionsTeamVoicePanel voice = content.GetComponent<OptionsTeamVoicePanel>();
        if (voice == null) voice = Undo.AddComponent<OptionsTeamVoicePanel>(content.gameObject);

        var so = new SerializedObject(voice);
        SerializedProperty rowsProp = so.FindProperty("rows");
        rowsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            Transform row = FindChild(content, "Row_Teammate_" + i);
            SerializedProperty elem = rowsProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("root").objectReferenceValue = row != null ? row.gameObject : null;
            Transform label = row != null ? FindChild(row, "Label") : null;
            elem.FindPropertyRelative("nameLabel").objectReferenceValue =
                label != null ? label.GetComponent<TMP_Text>() : null;
            Transform slider = row != null ? FindChild(row, "Slider") : null;
            elem.FindPropertyRelative("volumeSlider").objectReferenceValue =
                slider != null ? slider.GetComponent<Slider>() : null;
        }

        Transform empty = FindChild(content, "EmptyState");
        so.FindProperty("emptyState").objectReferenceValue = empty != null ? empty.gameObject : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(voice);
    }

    static string ResolveBindKey(TextMeshProUGUI tmp)
    {
        // Walk up a few parents for known row/tab/button names
        Transform t = tmp.transform;
        for (int depth = 0; depth < 4 && t != null; depth++)
        {
            if (LabelBindings.TryGetValue(t.name, out string key))
            {
                // Only bind the primary Label/Text under that node (not Value %)
                if (tmp.name == "Value") return null;
                if (t.name.StartsWith("Row_") && tmp.name != "Label" && tmp.name != "Text" && tmp.name != "Text (TMP)")
                    return null;
                return key;
            }
            t = t.parent;
        }
        return null;
    }

    static void SetLocalizedField(Object target, string fieldName, string entryKey)
    {
        FieldInfo field = target.GetType().GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
        {
            Debug.LogWarning("[SetupSettingUILocalization] Missing field: " + fieldName);
            return;
        }

        object current = field.GetValue(target);
        LocalizedString ls = current as LocalizedString ?? new LocalizedString();
        ls.SetReference(TableName, entryKey);
        field.SetValue(target, ls);
    }

    static void RenameIfExists(Transform parent, string from, string to)
    {
        Transform t = FindChild(parent, from);
        if (t != null) t.name = to;
    }

    static Transform FindInOpenScenes(string name)
    {
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindRecursive(root.transform, name);
                if (found != null) return found;
            }
        }
        return null;
    }

    static Transform FindRecursive(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform f = FindRecursive(t.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);
        return null;
    }

    static Dictionary<string, Dictionary<string, string>> BuildEntries()
    {
        var d = new Dictionary<string, Dictionary<string, string>>();

        void Add(string key, string en, string ko, string ja, string zhHans, string zhHant,
            string ru, string de, string fr, string es, string es419, string ptBr, string pl)
        {
            d[key] = new Dictionary<string, string>
            {
                { "en", en }, { "ko", ko }, { "ja", ja },
                { "zh-Hans", zhHans }, { "zh-Hant", zhHant },
                { "ru", ru }, { "de", de }, { "fr", fr },
                { "es", es }, { "es-419", es419 }, { "pt-BR", ptBr }, { "pl", pl },
            };
        }

        Add("Settings.Tab.General",
            "General", "일반", "一般", "常规", "一般",
            "Общие", "Allgemein", "Général", "General", "General", "Geral", "Ogólne");
        Add("Settings.Tab.Sound",
            "Sound", "사운드", "サウンド", "声音", "聲音",
            "Звук", "Audio", "Son", "Sonido", "Sonido", "Som", "Dźwięk");
        Add("Settings.Tab.TeamVoice",
            "Team Voice", "팀 보이스", "チームボイス", "队友语音", "隊友語音",
            "Голос команды", "Team-Chat", "Voix d'équipe", "Voz del equipo", "Voz del equipo", "Voz do time", "Głos drużyny");

        Add("Settings.Resolution",
            "Resolution", "해상도", "解像度", "分辨率", "解析度",
            "Разрешение", "Auflösung", "Résolution", "Resolución", "Resolución", "Resolução", "Rozdzielczość");
        Add("Settings.DisplayMode",
            "Display Mode", "화면 모드", "画面モード", "显示模式", "顯示模式",
            "Режим экрана", "Anzeigemodus", "Mode d'affichage", "Modo de pantalla", "Modo de pantalla", "Modo de exibição", "Tryb wyświetlania");
        Add("Settings.DisplayMode.Exclusive",
            "Fullscreen", "전체화면", "フルスクリーン", "全屏", "全螢幕",
            "Полный экран", "Vollbild", "Plein écran", "Pantalla completa", "Pantalla completa", "Tela cheia", "Pełny ekran");
        Add("Settings.DisplayMode.Windowed",
            "Windowed", "창모드", "ウィンドウ", "窗口", "視窗",
            "Оконный", "Fenstermodus", "Fenêtré", "Ventana", "Ventana", "Janela", "Okno");
        Add("Settings.DisplayMode.Borderless",
            "Borderless Window", "테두리 없는 창모드", "ボーダーレス", "无边框窗口", "無邊框視窗",
            "Без рамки", "Rahmenlos", "Fenêtre sans bord", "Sin bordes", "Sin bordes", "Sem bordas", "Bez ramek");
        Add("Settings.Language",
            "Language", "언어", "言語", "语言", "語言",
            "Язык", "Sprache", "Langue", "Idioma", "Idioma", "Idioma", "Język");
        Add("Settings.ChatFontSize",
            "Chat Font Size", "채팅 글자 크기", "チャット文字サイズ", "聊天字号", "聊天字級",
            "Размер шрифта чата", "Chat-Schriftgröße", "Taille du chat", "Tamaño del chat", "Tamaño del chat", "Tamanho do chat", "Rozmiar czatu");
        Add("Settings.MouseSensitivity",
            "Mouse Sensitivity", "마우스 감도", "マウス感度", "鼠标灵敏度", "滑鼠靈敏度",
            "Чувствительность мыши", "Mausempfindlichkeit", "Sensibilité de la souris", "Sensibilidad del ratón", "Sensibilidad del mouse", "Sensibilidade do mouse", "Czułość myszy");
        Add("Settings.DigitCheer",
            "Cheer with Number Keys", "숫자키로 응원하기", "数字キーで応援", "用数字键加油", "用數字鍵加油",
            "Поддержка цифрами", "Cheer mit Zifferntasten", "Cheer (touches numériques)", "Cheer con teclas numéricas", "Cheer con teclas numéricas", "Cheer com teclas numéricas", "Cheer klawiszami cyfr");

        Add("Settings.MasterVolume",
            "Master Volume", "마스터 볼륨", "マスター音量", "主音量", "主音量",
            "Общая громкость", "Gesamtlautstärke", "Volume principal", "Volumen general", "Volumen general", "Volume principal", "Głośność ogólna");
        Add("Settings.BgmVolume",
            "BGM Volume", "BGM 볼륨", "BGM音量", "BGM音量", "BGM音量",
            "Громкость BGM", "BGM-Lautstärke", "Volume BGM", "Volumen BGM", "Volumen BGM", "Volume BGM", "Głośność BGM");
        Add("Settings.SfxVolume",
            "SFX Volume", "SFX 볼륨", "SFX音量", "音效音量", "音效音量",
            "Громкость SFX", "SFX-Lautstärke", "Volume SFX", "Volumen SFX", "Volumen SFX", "Volume SFX", "Głośność SFX");
        Add("Settings.InputDevice",
            "Input Device", "입력 장치", "入力デバイス", "输入设备", "輸入裝置",
            "Устройство ввода", "Eingabegerät", "Périphérique d'entrée", "Dispositivo de entrada", "Dispositivo de entrada", "Dispositivo de entrada", "Urządzenie wejściowe");
        Add("Settings.MicVolume",
            "Mic Volume", "마이크 볼륨", "マイク音量", "麦克风音量", "麥克風音量",
            "Громкость микрофона", "Mikrofonlautstärke", "Volume micro", "Volumen del micrófono", "Volumen del micrófono", "Volume do microfone", "Głośność mikrofonu");
        Add("Settings.MicMute",
            "Mic Mute", "마이크 음소거", "マイクミュート", "麦克风静音", "麥克風靜音",
            "Микрофон выкл.", "Mikrofon stumm", "Micro coupé", "Silenciar micrófono", "Silenciar micrófono", "Silenciar microfone", "Wycisz mikrofon");

        Add("Settings.Cancel",
            "Cancel", "취소", "キャンセル", "取消", "取消",
            "Отмена", "Abbrechen", "Annuler", "Cancelar", "Cancelar", "Cancelar", "Anuluj");
        Add("Settings.Reset",
            "Default", "기본값", "デフォルト", "默认", "預設",
            "По умолчанию", "Standard", "Par défaut", "Predeterminado", "Predeterminado", "Padrão", "Domyślne");
        Add("Settings.Apply",
            "Apply", "적용", "適用", "应用", "套用",
            "Применить", "Übernehmen", "Appliquer", "Aplicar", "Aplicar", "Aplicar", "Zastosuj");
        Add("Settings.Close",
            "X", "X", "X", "X", "X",
            "X", "X", "X", "X", "X", "X", "X");

        Add("Settings.TeamVoice.Empty",
            "No teammates", "팀원이 없습니다", "チームメンバーがいません", "没有队友", "沒有隊友",
            "Нет товарищей", "Keine Teammitglieder", "Aucun coéquipier", "Sin compañeros", "Sin compañeros", "Sem companheiros", "Brak sojuszników");

        return d;
    }
}
#endif
