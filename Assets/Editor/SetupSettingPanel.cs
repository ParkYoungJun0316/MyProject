#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// TitleCanvas/Setting_Panel 기초 히어라키 생성 (1회용 셋업).
/// Menu: Tools / Setup Setting Panel (Title)
/// </summary>
public static class SetupSettingPanel
{
    const string PanelPathHint = "Setting_Panel";

    static readonly Color Pink = new Color(1f, 0.72f, 0.78f, 1f);
    static readonly Color TrackFill = new Color(1f, 0.72f, 0.78f, 1f);
    static readonly Color TrackBg = new Color(0.85f, 0.85f, 0.85f, 1f);

    [MenuItem("Tools/Setup Setting Panel (Title)")]
    public static void Run()
    {
        var panelT = FindInOpenScenes("Setting_Panel");
        if (panelT == null)
        {
            Debug.LogError("[SetupSettingPanel] Setting_Panel not found in open scenes.");
            return;
        }

        var panel = panelT.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(panel, "Setup Setting Panel");

        var titleSp = LoadSprite("Assets/Figma/Setting/Title.png");
        var selectSp = LoadSprite("Assets/Figma/Setting/SelectBG.png");
        var notSelectSp = LoadSprite("Assets/Figma/Setting/NotSelectBG.png");
        var dropdownBg = LoadSprite("Assets/Figma/Setting/DropdownBG.png");
        var dropdownArrow = LoadSprite("Assets/Figma/Setting/DropdownArrow.png");
        var toggleOn = LoadSprite("Assets/Figma/Setting/ToggleOn.png");
        var toggleOff = LoadSprite("Assets/Figma/Setting/ToggleOff.png");
        var fontKr = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/Noto/NotoSansKR-Regular SDF.asset");
        var fontEn = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/Fredoka-Bold SDF.asset");

        // wipe previous generated nodes (keep BG + tab buttons)
        DestroyNamedChildren(panelT, "TitleBadge", "CloseButton", "ContentRoot", "Footer");

        var b0 = FindChild(panelT, "Tab_General") ?? FindChild(panelT, "Button");
        var b1 = FindChild(panelT, "Tab_Sound") ?? FindChild(panelT, "Button (1)");
        var b2 = FindChild(panelT, "Tab_TeamVoice") ?? FindChild(panelT, "Button (2)");
        if (b0 == null || b1 == null || b2 == null)
        {
            Debug.LogError("[SetupSettingPanel] Tab buttons missing under Setting_Panel.");
            return;
        }

        b0.name = "Tab_General";
        b1.name = "Tab_Sound";
        b2.name = "Tab_TeamVoice";
        SetTabLabel(b0, "일반", fontKr);
        SetTabLabel(b1, "사운드", fontKr);
        SetTabLabel(b2, "팀 보이스", fontKr, 26);
        if (selectSp != null) b0.GetComponent<Image>().sprite = selectSp;
        if (notSelectSp != null)
        {
            b1.GetComponent<Image>().sprite = notSelectSp;
            b2.GetComponent<Image>().sprite = notSelectSp;
        }

        // Title
        var titleGo = CreateUiObject("TitleBadge", panelT);
        var titleRt = titleGo.GetComponent<RectTransform>();
        StretchCenter(titleRt, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(260f, 56f));
        var titleImg = titleGo.AddComponent<Image>();
        titleImg.sprite = titleSp;
        titleImg.preserveAspect = true;

        // Close
        var closeGo = CreateUiObject("CloseButton", panelT);
        var closeRt = closeGo.GetComponent<RectTransform>();
        StretchCenter(closeRt, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(44f, 44f));
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = Pink;
        var closeBtn = closeGo.AddComponent<Button>();
        CreateLabel(closeGo.transform, "X", fontEn, 28, TextAlignmentOptions.Center);

        // Content root
        var contentRoot = CreateUiObject("ContentRoot", panelT);
        var crRt = contentRoot.GetComponent<RectTransform>();
        StretchCenter(crRt, new Vector2(0.5f, 0.5f), new Vector2(90f, 20f), new Vector2(560f, 360f));

        var cGeneral = CreateContent(contentRoot.transform, "Content_General");
        var cSound = CreateContent(contentRoot.transform, "Content_Sound");
        var cVoice = CreateContent(contentRoot.transform, "Content_TeamVoice");
        cSound.SetActive(false);
        cVoice.SetActive(false);

        // --- General ---
        // 밝기는 SoundAndSettingsDesign.md §8 확정으로 스코프 제외(URP Volume 인프라 필요 대비 실익 낮음,
        // 게임이 어두운 톤도 아님) — Row_Brightness 생성하지 않음.
        var resolutionDd = CreateDropdownRow(cGeneral.transform, "Row_Resolution", "해상도", fontKr, dropdownBg, dropdownArrow);
        var displayModeDd = CreateDropdownRow(cGeneral.transform, "Row_DisplayMode", "화면 모드", fontKr, dropdownBg, dropdownArrow);
        var languageDd = CreateDropdownRow(cGeneral.transform, "Row_Language", "언어", fontKr, dropdownBg, dropdownArrow);

        // --- Sound (wired volumes + mic) ---
        // 출력 장치(헤드셋) 선택은 Unity 표준 API로 불가능(OS 기본 출력 장치로만 재생, 네이티브 플러그인
        // 없이는 지원 불가) — Row_OutputDevice 생성하지 않음. Windows 사운드 설정에서 변경하도록 안내.
        CreateSliderRow(cSound.transform, "Row_Master", "마스터 볼륨", fontKr, out var masterSlider, out _);
        CreateSliderRow(cSound.transform, "Row_BGM", "BGM 볼륨", fontKr, out var bgmSlider, out _);
        CreateSliderRow(cSound.transform, "Row_SFX", "SFX 볼륨", fontKr, out var sfxSlider, out _);
        var micDeviceDd = CreateDropdownRow(cSound.transform, "Row_InputDevice", "입력 장치", fontKr, dropdownBg, dropdownArrow);
        CreateSliderRow(cSound.transform, "Row_MicVolume", "마이크 볼륨", fontKr, out _, out _, placeholder: true);
        var micMuteToggle = CreateToggleRow(cSound.transform, "Row_MicMute", "마이크 음소거", fontKr, toggleOn, toggleOff);

        // --- Team voice stubs ---
        CreateSliderRow(cVoice.transform, "Row_GumaVoice", "GUMA Voice", fontKr, out _, out _, placeholder: true);
        CreateSliderRow(cVoice.transform, "Row_DanhoVoice", "DANHO Voice", fontKr, out _, out _, placeholder: true);
        CreateSliderRow(cVoice.transform, "Row_SsukVoice", "SSUK Voice", fontKr, out _, out _, placeholder: true);

        // Footer
        // 취소/적용 버튼은 제거됨 — pull 방식(값 즉시 적용) 구조라 "취소"할 스냅샷이 없고 "적용"도
        // 실질적으로 닫기와 동일해 라벨-동작 불일치 버그였음(SoundAndSettingsDesign.md §9.2-②).
        // 닫기는 우상단 Close.Btn(X) 하나로 통일.
        var footer = CreateUiObject("Footer", panelT);
        var fRt = footer.GetComponent<RectTransform>();
        StretchCenter(fRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(160f, 56f));
        CreateFooterButton(footer.transform, "Btn_Reset", "초기화", new Vector2(0f, 0f), notSelectSp, fontKr);

        // Components on panel
        var tabs = panel.GetComponent<OptionsPanelTabs>();
        if (tabs == null) tabs = Undo.AddComponent<OptionsPanelTabs>(panel);
        var options = panel.GetComponent<OptionsMenuController>();
        if (options == null) options = Undo.AddComponent<OptionsMenuController>(panel);

        // Wire OptionsPanelTabs
        var tabsSo = new SerializedObject(tabs);
        tabsSo.FindProperty("selectedSprite").objectReferenceValue = selectSp;
        tabsSo.FindProperty("unselectedSprite").objectReferenceValue = notSelectSp;
        tabsSo.FindProperty("defaultTabIndex").intValue = 0;
        var tabsProp = tabsSo.FindProperty("tabs");
        tabsProp.arraySize = 3;
        WireTab(tabsProp.GetArrayElementAtIndex(0), b0.GetComponent<Button>(), cGeneral, b0.GetComponent<Image>());
        WireTab(tabsProp.GetArrayElementAtIndex(1), b1.GetComponent<Button>(), cSound, b1.GetComponent<Image>());
        WireTab(tabsProp.GetArrayElementAtIndex(2), b2.GetComponent<Button>(), cVoice, b2.GetComponent<Image>());
        tabsSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire OptionsMenuController
        var optSo = new SerializedObject(options);
        optSo.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
        optSo.FindProperty("bgmVolumeSlider").objectReferenceValue = bgmSlider;
        optSo.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
        optSo.FindProperty("languageDropdown").objectReferenceValue = languageDd;
        optSo.FindProperty("displayModeDropdown").objectReferenceValue = displayModeDd;
        optSo.FindProperty("resolutionDropdown").objectReferenceValue = resolutionDd;
        optSo.FindProperty("micMuteToggle").objectReferenceValue = micMuteToggle;
        optSo.FindProperty("micDeviceDropdown").objectReferenceValue = micDeviceDd;
        optSo.ApplyModifiedPropertiesWithoutUndo();

        // Wire TitleMenuController
        var titleMenu = Object.FindFirstObjectByType<TitleMenuController>(FindObjectsInactive.Include);
        if (titleMenu != null)
        {
            var tmSo = new SerializedObject(titleMenu);
            tmSo.FindProperty("settingsPanel").objectReferenceValue = panel;
            tmSo.ApplyModifiedPropertiesWithoutUndo();

            ClearPersistent(closeBtn);
            UnityEventTools.AddVoidPersistentListener(closeBtn.onClick, titleMenu.OnClickCloseSettings);
            EditorUtility.SetDirty(closeBtn);
            EditorUtility.SetDirty(titleMenu);
        }
        else
        {
            Debug.LogWarning("[SetupSettingPanel] TitleMenuController not found — close not wired.");
        }

        panel.SetActive(false);
        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(panel.scene);
        Selection.activeGameObject = panel;
        Debug.Log("[SetupSettingPanel] Done. Setting_Panel wired. Polish layout in Inspector as needed.");
    }

    static void WireTab(SerializedProperty elem, Button button, GameObject content, Image bg)
    {
        elem.FindPropertyRelative("button").objectReferenceValue = button;
        elem.FindPropertyRelative("content").objectReferenceValue = content;
        elem.FindPropertyRelative("buttonBackground").objectReferenceValue = bg;
    }

    static void ClearPersistent(Button button)
    {
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
    }

    static Transform FindInOpenScenes(string name)
    {
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindRecursive(root.transform, name);
                if (t != null) return t;
            }
        }
        return null;
    }

    static Transform FindRecursive(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var f = FindRecursive(t.GetChild(i), name);
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

    static void DestroyNamedChildren(Transform parent, params string[] names)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var ch = parent.GetChild(i);
            foreach (var n in names)
            {
                if (ch.name == n)
                {
                    Undo.DestroyObjectImmediate(ch.gameObject);
                    break;
                }
            }
        }
    }

    static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return go;
    }

    static void StretchCenter(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void SetTabLabel(Transform tab, string text, TMP_FontAsset font, float size = 28f)
    {
        var tmp = tab.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null) return;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.black;
        tmp.alignment = TextAlignmentOptions.Center;
        if (font != null) tmp.font = font;
    }

    static GameObject CreateContent(Transform parent, string name)
    {
        var go = CreateUiObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var v = go.AddComponent<VerticalLayoutGroup>();
        v.spacing = 6f;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = false;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.padding = new RectOffset(4, 4, 4, 4);
        return go;
    }

    static GameObject CreateRow(Transform parent, string name, float height = 64f)
    {
        var go = CreateUiObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, height);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        return go;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string text, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var go = CreateUiObject("Text", parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.black;
        tmp.alignment = align;
        if (font != null) tmp.font = font;
        return tmp;
    }

    static void CreateRowLabel(Transform row, string text, TMP_FontAsset font)
    {
        var go = CreateUiObject("Label", row);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(0f, 28f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 22;
        tmp.color = Color.black;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (font != null) tmp.font = font;
    }

    static void CreateSliderRow(Transform parent, string name, string label, TMP_FontAsset font,
        out Slider slider, out TextMeshProUGUI valueText, bool placeholder = false)
    {
        var row = CreateRow(parent, name, 64f);
        CreateRowLabel(row.transform, label + (placeholder ? " (미연동)" : ""), font);

        var sliderGo = CreateUiObject("Slider", row.transform);
        var sRt = sliderGo.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, 0f);
        sRt.anchorMax = new Vector2(1f, 0f);
        sRt.pivot = new Vector2(0.5f, 0f);
        sRt.anchoredPosition = new Vector2(-30f, 4f);
        sRt.sizeDelta = new Vector2(-80f, 24f);

        var bg = CreateUiObject("Background", sliderGo.transform);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.25f);
        bgRt.anchorMax = new Vector2(1f, 0.75f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = TrackBg;

        var fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        var faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = new Vector2(0f, 0.25f);
        faRt.anchorMax = new Vector2(1f, 0.75f);
        faRt.offsetMin = new Vector2(5f, 0f);
        faRt.offsetMax = new Vector2(-5f, 0f);
        var fill = CreateUiObject("Fill", fillArea.transform);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = TrackFill;

        var handleArea = CreateUiObject("Handle Slide Area", sliderGo.transform);
        var haRt = handleArea.GetComponent<RectTransform>();
        haRt.anchorMin = Vector2.zero;
        haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(10f, 0f);
        haRt.offsetMax = new Vector2(-10f, 0f);
        var handle = CreateUiObject("Handle", handleArea.transform);
        var hRt = handle.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(24f, 24f);
        var hImg = handle.AddComponent<Image>();
        hImg.color = Color.white;

        slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = hRt;
        slider.targetGraphic = hImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.7f;
        if (placeholder) slider.interactable = false;

        var valueGo = CreateUiObject("Value", row.transform);
        var vRt = valueGo.GetComponent<RectTransform>();
        vRt.anchorMin = new Vector2(1f, 0f);
        vRt.anchorMax = new Vector2(1f, 0f);
        vRt.pivot = new Vector2(1f, 0f);
        vRt.anchoredPosition = new Vector2(0f, 4f);
        vRt.sizeDelta = new Vector2(56f, 24f);
        valueText = valueGo.AddComponent<TextMeshProUGUI>();
        valueText.text = "70%";
        valueText.fontSize = 18;
        valueText.color = Color.black;
        valueText.alignment = TextAlignmentOptions.MidlineRight;
        if (font != null) valueText.font = font;

        var pct = sliderGo.AddComponent<SliderValuePercentLabel>();
        var pctSo = new SerializedObject(pct);
        pctSo.FindProperty("label").objectReferenceValue = valueText;
        pctSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static TMP_Dropdown CreateDropdownRow(Transform parent, string name, string label, TMP_FontAsset font,
        Sprite bgSprite, Sprite arrowSprite, bool stub = false)
    {
        var row = CreateRow(parent, name, 64f);
        CreateRowLabel(row.transform, label + (stub ? " (미연동)" : ""), font);

        var ddGo = CreateUiObject("Dropdown", row.transform);
        var ddRt = ddGo.GetComponent<RectTransform>();
        ddRt.anchorMin = new Vector2(0f, 0f);
        ddRt.anchorMax = new Vector2(1f, 0f);
        ddRt.pivot = new Vector2(0.5f, 0f);
        ddRt.anchoredPosition = new Vector2(0f, 2f);
        ddRt.sizeDelta = new Vector2(0f, 34f);

        var ddImg = ddGo.AddComponent<Image>();
        ddImg.sprite = bgSprite;
        ddImg.type = bgSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        ddImg.color = Color.white;

        var labelGo = CreateUiObject("Label", ddGo.transform);
        var lRt = labelGo.GetComponent<RectTransform>();
        lRt.anchorMin = Vector2.zero;
        lRt.anchorMax = Vector2.one;
        lRt.offsetMin = new Vector2(12f, 2f);
        lRt.offsetMax = new Vector2(-28f, -2f);
        var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
        labelTmp.text = stub ? "—" : "옵션";
        labelTmp.fontSize = 18;
        labelTmp.color = Color.black;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (font != null) labelTmp.font = font;

        var arrowGo = CreateUiObject("Arrow", ddGo.transform);
        var aRt = arrowGo.GetComponent<RectTransform>();
        aRt.anchorMin = new Vector2(1f, 0.5f);
        aRt.anchorMax = new Vector2(1f, 0.5f);
        aRt.pivot = new Vector2(1f, 0.5f);
        aRt.anchoredPosition = new Vector2(-8f, 0f);
        aRt.sizeDelta = new Vector2(18f, 18f);
        var aImg = arrowGo.AddComponent<Image>();
        aImg.sprite = arrowSprite;
        aImg.preserveAspect = true;

        // Minimal template so TMP_Dropdown is valid
        var template = CreateUiObject("Template", ddGo.transform);
        var tRt = template.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0f);
        tRt.anchorMax = new Vector2(1f, 0f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, 2f);
        tRt.sizeDelta = new Vector2(0f, 120f);
        template.AddComponent<Image>().color = Color.white;
        var scroll = template.AddComponent<ScrollRect>();

        var viewport = CreateUiObject("Viewport", template.transform);
        var vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;
        viewport.AddComponent<Image>().color = Color.white;
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        var content = CreateUiObject("Content", viewport.transform);
        var cRt = content.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(0f, 32f);

        var item = CreateUiObject("Item", content.transform);
        var iRt = item.GetComponent<RectTransform>();
        iRt.anchorMin = new Vector2(0f, 0.5f);
        iRt.anchorMax = new Vector2(1f, 0.5f);
        iRt.sizeDelta = new Vector2(0f, 28f);
        var toggle = item.AddComponent<Toggle>();
        var itemBg = CreateUiObject("Item Background", item.transform);
        var ibRt = itemBg.GetComponent<RectTransform>();
        ibRt.anchorMin = Vector2.zero;
        ibRt.anchorMax = Vector2.one;
        ibRt.offsetMin = Vector2.zero;
        ibRt.offsetMax = Vector2.zero;
        var itemBgImg = itemBg.AddComponent<Image>();
        itemBgImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        var itemLabel = CreateUiObject("Item Label", item.transform);
        var ilRt = itemLabel.GetComponent<RectTransform>();
        ilRt.anchorMin = Vector2.zero;
        ilRt.anchorMax = Vector2.one;
        ilRt.offsetMin = new Vector2(12f, 1f);
        ilRt.offsetMax = new Vector2(-12f, -1f);
        var itemTmp = itemLabel.AddComponent<TextMeshProUGUI>();
        itemTmp.fontSize = 16;
        itemTmp.color = Color.black;
        if (font != null) itemTmp.font = font;
        toggle.targetGraphic = itemBgImg;
        toggle.graphic = null;

        scroll.viewport = vpRt;
        scroll.content = cRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        template.SetActive(false);

        var dd = ddGo.AddComponent<TMP_Dropdown>();
        dd.template = tRt;
        dd.captionText = labelTmp;
        dd.itemText = itemTmp;
        dd.targetGraphic = ddImg;
        if (stub)
        {
            dd.ClearOptions();
            dd.AddOptions(new System.Collections.Generic.List<string> { "—" });
            dd.interactable = false;
        }
        return dd;
    }

    static Toggle CreateToggleRow(Transform parent, string name, string label, TMP_FontAsset font, Sprite onSp, Sprite offSp)
    {
        var row = CreateRow(parent, name, 56f);
        CreateRowLabel(row.transform, label, font);

        var toggleGo = CreateUiObject("Toggle", row.transform);
        var tRt = toggleGo.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0f);
        tRt.anchorMax = new Vector2(0f, 0f);
        tRt.pivot = new Vector2(0f, 0f);
        tRt.anchoredPosition = new Vector2(0f, 2f);
        tRt.sizeDelta = new Vector2(72f, 36f);
        var bg = toggleGo.AddComponent<Image>();
        bg.sprite = offSp != null ? offSp : null;
        bg.color = Color.white;
        bg.preserveAspect = true;
        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.isOn = false;

        var swap = toggleGo.AddComponent<ToggleSpriteSwap>();
        var swapSo = new SerializedObject(swap);
        swapSo.FindProperty("toggle").objectReferenceValue = toggle;
        swapSo.FindProperty("targetImage").objectReferenceValue = bg;
        swapSo.FindProperty("onSprite").objectReferenceValue = onSp;
        swapSo.FindProperty("offSprite").objectReferenceValue = offSp;
        swapSo.ApplyModifiedPropertiesWithoutUndo();

        return toggle;
    }

    static Button CreateFooterButton(Transform parent, string name, string label, Vector2 pos, Sprite sp, TMP_FontAsset font)
    {
        var go = CreateUiObject(name, parent);
        var rt = go.GetComponent<RectTransform>();
        StretchCenter(rt, new Vector2(0.5f, 0.5f), pos, new Vector2(120f, 48f));
        var img = go.AddComponent<Image>();
        if (sp != null) img.sprite = sp;
        else img.color = Pink;
        var btn = go.AddComponent<Button>();
        CreateLabel(go.transform, label, font, 24, TextAlignmentOptions.Center);
        return btn;
    }
}
#endif
