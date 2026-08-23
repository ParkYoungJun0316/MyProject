using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 바로가기 UI — 구 LobbyMenuController 스테이지 드롭다운을 Tutorial 게이트로 이전.
/// 2026-08-22 확정: Dev Build 여부와 무관하게 모든 빌드(Release 포함)에서 항상 사용 가능
/// — Steam 베타 테스트 중 스테이지 스킵이 필요해서 완전 노출로 결정(TutorialNetworkManager도 동일).
///
/// [배치 방법]
/// Tutorial 씬에 빈 GameObject 하나 만들고 이 컴포넌트만 부착하면 끝.
/// 런타임에 스스로 Canvas/버튼을 생성하므로 씬에서 UI를 미리 만들어둘 필요 없음.
/// (단, Tutorial 씬에 EventSystem이 이미 있어야 버튼 클릭이 동작함 — 기존 ESC 메뉴/HUD가 있다면
/// 보통 이미 존재함.)
///
/// [동작]
/// - Host 화면에만 표시. Client/미접속 인스턴스에서는 자기 자신을 제거.
/// - SceneFlowManager.sceneSequence에서 "M."/"T." 접두사 스테이지만 필터링해 버튼으로 나열.
/// - 버튼 클릭 → TutorialNetworkManager.SetDevTargetStage(index)만 로컬로 지정.
///   실제 씬 전환은 기존 게이트(전원 진입 + 3초 카운트다운)를 그대로 통과한 뒤
///   CompleteGate()에서 일어난다 — "게이트 통과하면 어디로 갈지"를 미리 골라두는 역할.
/// - "자동 진행" 버튼으로 지정을 취소하면 원래대로 다음 순번(M.Stage1 등)으로 진행.
/// </summary>
public class TutorialDevStageJumpUI : MonoBehaviour
{
    const string SelectedColorHex = "#4CAF50";
    const string DefaultColorHex  = "#333333";
    const int    AutoAdvanceIndex = -1;

    struct ButtonEntry
    {
        public int   StageIndex;
        public Image BackgroundImage;
    }

    readonly List<ButtonEntry> _entries = new();
    int _selectedIndex = AutoAdvanceIndex;

    IEnumerator Start()
    {
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield return null;

        if (!NetworkManager.Singleton.IsHost)
        {
            Destroy(gameObject);
            yield break;
        }

        while (SceneFlowManager.Instance == null)
            yield return null;

        BuildUI();
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("DevStageJumpCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelGo.transform.SetParent(canvasGo.transform, false);

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(1f, 0.5f);
        panelRect.anchorMax        = new Vector2(1f, 0.5f);
        panelRect.pivot            = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-10f, 0f);

        panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var layout = panelGo.GetComponent<VerticalLayoutGroup>();
        layout.padding                = new RectOffset(8, 8, 8, 8);
        layout.spacing                = 4f;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth      = true;
        layout.childControlHeight     = true;

        var fitter = panelGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        var titleGo = CreateLabel(panelGo.transform, "스테이지 바로가기 (DEV)");
        titleGo.GetComponent<Text>().fontStyle = FontStyle.Bold;

        var autoBtn = CreateButton(panelGo.transform, "자동 진행 (다음 스테이지)", () => OnStageButtonClicked(AutoAdvanceIndex));
        _entries.Add(new ButtonEntry { StageIndex = AutoAdvanceIndex, BackgroundImage = autoBtn.GetComponent<Image>() });

        int count = SceneFlowManager.Instance.SceneCount;
        for (int i = 0; i < count; i++)
        {
            string sceneName = SceneFlowManager.Instance.GetSceneName(i);
            if (sceneName == null) continue;
            if (!sceneName.StartsWith("M.") && !sceneName.StartsWith("T.")) continue;

            int capturedIndex = i;
            var buttonGo = CreateButton(panelGo.transform, sceneName, () => OnStageButtonClicked(capturedIndex));
            _entries.Add(new ButtonEntry { StageIndex = capturedIndex, BackgroundImage = buttonGo.GetComponent<Image>() });
        }

        RefreshButtonColors();
    }

    void OnStageButtonClicked(int index)
    {
        _selectedIndex = index;
        TutorialNetworkManager.Instance?.SetDevTargetStage(index);
        RefreshButtonColors();

        string sceneName = index == AutoAdvanceIndex ? "(자동 진행)" : SceneFlowManager.Instance.GetSceneName(index);
        Debug.Log($"[TutorialDevStageJumpUI] 목표 스테이지 지정 — index={index} scene={sceneName}");
    }

    void RefreshButtonColors()
    {
        foreach (var entry in _entries)
            entry.BackgroundImage.color = entry.StageIndex == _selectedIndex
                ? ParseColor(SelectedColorHex)
                : ParseColor(DefaultColorHex);
    }

    static GameObject CreateLabel(Transform parent, string text)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var t = go.GetComponent<Text>();
        t.text      = text;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = 48;
        t.color     = Color.white;
        t.alignment = TextAnchor.MiddleLeft;

        go.GetComponent<LayoutElement>().preferredHeight = 66f;
        return go;
    }

    static GameObject CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        go.GetComponent<Image>().color = ParseColor(DefaultColorHex);

        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = 84f;
        le.preferredWidth  = 660f;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);

        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var t = textGo.GetComponent<Text>();
        t.text      = label;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = 42;
        t.alignment = TextAnchor.MiddleCenter;
        t.color     = Color.white;

        go.GetComponent<Button>().onClick.AddListener(onClick);
        return go;
    }

    static Color ParseColor(string hex) =>
        ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.gray;
}
