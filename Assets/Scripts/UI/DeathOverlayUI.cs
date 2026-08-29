using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 사망 시 화면에 뜨는 오버레이.
/// 다른 UI 스크립트(PlayerHPUI/BossHealthBarUI 등)와 동일한 패턴 — 배치·폰트·크기는
/// Prefab/씬에서 직접 만든 UI 요소를 Inspector에서 연결해서 쓴다. 이 스크립트는 위치를
/// 강제로 세팅하지 않고, 연결된 요소의 텍스트/색만 갱신한다.
///
/// [필수 연결 (Inspector)]
///  - canvasGroup : 이 오버레이의 표시/숨김을 담당할 CanvasGroup (비워두면 자기 GameObject에서 탐색)
///  - background  : 배경 Image — 사망자 고유색으로 tint됨
///  - mainText    : "OO 사망" 텍스트
///
/// [배경 — 플레이테스트 피드백]
/// "죽은지 잘 모르겠다". 사망 즉시(1프레임 후) 씬이 리로드돼(현재는
/// StageNetworkState.deathReloadDelay로 연장됨) 그 사이를 채워줄 전용 연출이 없었다. 이 게임은
/// 1명 사망 = 전원 리로드(§11)라, 본인이 안 죽었어도 팀원이 죽으면 화면이 갑자기 리셋된다 —
/// 그 이유를 알려주기 위해 사망자 CheerName("berry 사망")과 그 사람의 고유색(배경/텍스트)을 표시한다.
/// 본인/팀원 구분 없이 동일 로직 — 배경색이 곧 "누가 죽었는지"를 알려준다.
///
/// [트리거]
/// 각 Player의 PlayerEvents.OnDied — Owner/비Owner 모두 Player.Die()/SyncDeadFlag()에서 이미
/// 전 클라이언트에 복제되어 호출되므로(§11), 여기서 추가 네트워크 브릿지가 필요 없다.
/// </summary>
public class DeathOverlayUI : MonoBehaviour
{
    [Header("연결 (Prefab/씬에서 직접 배치 후 연결)")]
    [Tooltip("표시/숨김용 CanvasGroup. 비워두면 이 GameObject에서 자동 탐색.")]
    [SerializeField] CanvasGroup      canvasGroup;
    [Tooltip("배경 Image. 사망자 고유색으로 tint됨. 비워두면 배경 색 반영을 건너뜀.")]
    [SerializeField] Image            background;
    [Tooltip("\"{CheerName} 사망\" 텍스트.")]
    [SerializeField] TextMeshProUGUI  mainText;

    [Header("문구")]
    [Tooltip("DeathUI 테이블 Death.Format — \"{0} 사망\". 비어 있으면 한국어 폴백.")]
    [SerializeField] LocalizedString deathMessage;

    [Header("타이밍(초)")]
    [SerializeField] float fadeInDuration  = 0.25f;
    [SerializeField] float popInDuration   = 0.25f;
    [SerializeField] float popInStartScale = 1.6f;

    [Header("배경")]
    [Tooltip("사망자 고유색을 배경에 입힐 때 쓸 알파(투명도).")]
    [SerializeField] float backgroundAlpha = 0.85f;

    Coroutine _fadeRoutine;
    Coroutine _popRoutine;

    readonly List<(PlayerEvents events, Action handler)> _subs = new();

    // ── 초기화 ───────────────────────────────────────────────────

    void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) return;

        canvasGroup.alpha          = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable   = false;
    }

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

        int colorIndex = ResolveColorIndex(deadPlayer);

        if (mainText != null)
            mainText.text = FormatDeathMessage(GetPlayerCheerName(colorIndex));

        Color accent = PlayerColorUtil.GetUniqueColor(
            colorIndex >= 0 && colorIndex < PlayerColorUtil.ColorOrder.Length
                ? PlayerColorUtil.ColorOrder[colorIndex]
                : PlayerColorType.Blue);

        // 배경은 사망자의 고유색 그대로(누가 죽었는지 배경만 봐도 알 수 있게) — 본인/팀원 구분 없이 동일.
        if (background != null)
            background.color = new Color(accent.r, accent.g, accent.b, backgroundAlpha);
        if (mainText != null)
            mainText.color = ReadableTextColorFor(accent);

        Show();
    }

    const string FallbackDeathFormat = "{0} 사망";

    /// <summary>DeathUI/Death.Format. 테이블 미연결·미로드면 한국어 폴백 (OptionsMenuController와 동일).</summary>
    string FormatDeathMessage(string displayName)
    {
        if (deathMessage != null && !deathMessage.IsEmpty)
        {
            string localized = deathMessage.GetLocalizedString(displayName);
            if (!string.IsNullOrEmpty(localized)) return localized;
        }
        return string.Format(FallbackDeathFormat, displayName);
    }

    /// <summary>배경 고유색의 밝기에 따라 검/흰 쪽으로 살짝 기울여 대비를 확보(노랑 배경엔 어둡게, 어두운 배경엔 밝게).</summary>
    static Color ReadableTextColorFor(Color bgColor)
    {
        float luminance = 0.299f * bgColor.r + 0.587f * bgColor.g + 0.114f * bgColor.b;
        Color baseColor = luminance > 0.55f ? Color.black : Color.white;
        return Color.Lerp(baseColor, bgColor, 0.15f);
    }

    void Show()
    {
        if (canvasGroup != null)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeIn());
        }

        if (mainText != null)
        {
            if (_popRoutine != null) StopCoroutine(_popRoutine);
            _popRoutine = StartCoroutine(PopIn());
        }
    }

    IEnumerator FadeIn()
    {
        float from    = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, 1f, elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;
        // 이후 페이드아웃 없음 — 곧 씬 리로드로 자연스럽게 사라짐(StageNetworkState.deathReloadDelay).
    }

    /// <summary>메인 텍스트가 크게 튀어나왔다가 원래 크기로 톡 떨어지는 펀치 스케일 연출.</summary>
    IEnumerator PopIn()
    {
        Transform textTransform = mainText.transform;
        textTransform.localScale = Vector3.one * popInStartScale;

        float elapsed = 0f;
        while (elapsed < popInDuration)
        {
            elapsed += Time.deltaTime;
            float t     = elapsed / popInDuration;
            float eased = 1f - (1f - t) * (1f - t); // ease-out
            textTransform.localScale = Vector3.one * Mathf.Lerp(popInStartScale, 1f, eased);
            yield return null;
        }
        textTransform.localScale = Vector3.one;
    }

    // ── 색/이름 조회 (TeamStatusUI.ResolveColorIndex와 동일 로직) ──

    static int ResolveColorIndex(Player player)
    {
        if (player == null) return -1;
        NetworkObject net = player.GetComponent<NetworkObject>();
        if (net != null && PlayerSpawnCoordinator.TryGetColor(net.OwnerClientId, out var sessionColor))
            return Array.IndexOf(PlayerColorUtil.ColorOrder, sessionColor);
        return Array.IndexOf(PlayerColorUtil.ColorOrder, player.playerColorType);
    }

    static string GetPlayerCheerName(int colorIndex)
    {
        string name = CheerService.GetCheerName(colorIndex);
        return string.IsNullOrEmpty(name) ? "???" : name;
    }
}
