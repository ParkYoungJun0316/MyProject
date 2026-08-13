using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 사망 시 화면에 뜨는 전용 오버레이.
/// UI.prefab의 Canvas 밑에 빈 GameObject로 배치하고 이 스크립트를 붙이면 된다 — 자식 비주얼은
/// Awake에서 스스로 만든다(ObjectiveUI/TeamStatusUI와 동일 패턴). 추가 Inspector 연결 불필요.
///
/// [배경 — 플레이테스트 피드백]
/// "죽은지 잘 모르겠다". 사망 즉시(1프레임 후) 씬이 리로드돼(현재는
/// StageNetworkState.deathReloadDelay로 연장됨) 그 사이를 채워줄 전용 연출이 없었다. 이 게임은
/// 1명 사망 = 전원 리로드(§11)라, 본인이 안 죽었어도 팀원이 죽으면 화면이 갑자기 리셋된다 —
/// 그 이유를 알려주기 위해 본인 사망("사망!!")과 팀원 사망("OO 사망")을 구분 표시한다.
///
/// [트리거]
/// 각 Player의 PlayerEvents.OnDied — Owner/비Owner 모두 Player.Die()/SyncDeadFlag()에서 이미
/// 전 클라이언트에 복제되어 호출되므로(§11), 여기서 추가 네트워크 브릿지가 필요 없다.
/// </summary>
public class DeathOverlayUI : MonoBehaviour
{
    [Header("문구")]
    [SerializeField] string selfDeathMessage             = "사망!!";
    [SerializeField] string teammateDeathMessageFormat   = "{0} 사망";
    [SerializeField] string subMessage                   = "재시작합니다...";

    [Header("색상")]
    [SerializeField] Color vignetteColor = new Color(0.4f, 0f, 0f, 0.85f);
    [SerializeField] Color textColor     = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("타이밍(초)")]
    [SerializeField] float fadeInDuration = 0.25f;

    [Header("텍스트 크기")]
    [SerializeField] float mainFontSize = 64f;
    [SerializeField] float subFontSize  = 24f;

    CanvasGroup     _canvasGroup;
    TextMeshProUGUI _mainText;
    TextMeshProUGUI _subText;
    Coroutine       _fadeRoutine;

    readonly List<(PlayerEvents events, Action handler)> _subs = new();

    // ── 초기화 ───────────────────────────────────────────────────

    void Awake() => BuildVisual();

    void Start()
    {
        PlayerSpawnCoordinator.OnPlayersReady += RebuildSubscriptions;
        if (PlayerSpawnCoordinator.IsReady) RebuildSubscriptions();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= RebuildSubscriptions;
        UnsubscribeAll();
    }

    void BuildVisual()
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable   = false;

        RectTransform selfRt = GetComponent<RectTransform>();
        if (selfRt == null) selfRt = gameObject.AddComponent<RectTransform>();
        selfRt.anchorMin = Vector2.zero;
        selfRt.anchorMax = Vector2.one;
        selfRt.offsetMin = Vector2.zero;
        selfRt.offsetMax = Vector2.zero;

        Image bg = gameObject.AddComponent<Image>();
        bg.color = vignetteColor;

        _mainText      = CreateText("MainText", mainFontSize, new Vector2(0f, 0.15f), new Vector2(1f, 0.6f));
        _subText       = CreateText("SubText",  subFontSize,  new Vector2(0f, 0.05f), new Vector2(1f, 0.15f));
        _subText.text  = subMessage;
    }

    TextMeshProUGUI CreateText(string name, float size, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize   = size;
        tmp.fontStyle  = FontStyles.Bold;
        tmp.color      = textColor;
        tmp.alignment  = TextAlignmentOptions.Center;
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return tmp;
    }

    // ── 구독 (TeamStatusUI.BuildSlots와 동일한 재구성 패턴) ────────

    void RebuildSubscriptions()
    {
        UnsubscribeAll();

        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p == null) continue;
            PlayerEvents events = p.GetComponent<PlayerEvents>();
            if (events == null) continue;

            Player captured = p;
            Action handler = () => HandlePlayerDied(captured);
            events.OnDied += handler;
            _subs.Add((events, handler));
        }
    }

    void UnsubscribeAll()
    {
        foreach (var (events, handler) in _subs)
            if (events != null) events.OnDied -= handler;
        _subs.Clear();
    }

    // ── 사망 처리 ─────────────────────────────────────────────────

    void HandlePlayerDied(Player deadPlayer)
    {
        if (deadPlayer == null) return;

        NetworkObject net = deadPlayer.GetComponent<NetworkObject>();
        bool isLocalDeath  = (net != null && net.IsOwner) || deadPlayer.isOwnerControlled;

        if (isLocalDeath)
        {
            _mainText.text = selfDeathMessage;
        }
        else
        {
            int colorIndex = ResolveColorIndex(deadPlayer);
            _mainText.text = string.Format(teammateDeathMessageFormat, GetPlayerDisplayName(colorIndex));
        }

        Show();
    }

    void Show()
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float from    = _canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, 1f, elapsed / fadeInDuration);
            yield return null;
        }
        _canvasGroup.alpha = 1f;
        // 이후 페이드아웃 없음 — 곧 씬 리로드로 자연스럽게 사라짐(StageNetworkState.deathReloadDelay).
    }

    // ── 색/이름 조회 (TeamStatusUI.ResolveColorIndex와 동일 로직) ──

    static int ResolveColorIndex(Player player)
    {
        if (player == null) return -1;
        NetworkObject net = player.GetComponent<NetworkObject>();
        if (net != null && PlayerSpawnCoordinator.TryGetColor(net.OwnerClientId, out var sessionColor))
            return Array.IndexOf(LobbyNetworkManager.ColorOrder, sessionColor);
        return Array.IndexOf(LobbyNetworkManager.ColorOrder, player.playerColorType);
    }

    static string GetPlayerDisplayName(int colorIndex)
    {
        string name = GameSession.Instance != null ? GameSession.Instance.GetSessionDisplayName(colorIndex) : null;
        return string.IsNullOrEmpty(name) ? "???" : name;
    }
}
