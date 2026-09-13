using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 사망/다운 시 화면에 뜨는 오버레이.
/// 다른 UI 스크립트(PlayerHPUI/BossHealthBarUI 등)와 동일한 패턴 — 배치·폰트·크기는
/// Prefab/씬에서 직접 만든 UI 요소를 Inspector에서 연결해서 쓴다. 이 스크립트는 위치를
/// 강제로 세팅하지 않고, 연결된 요소의 텍스트/색만 갱신한다.
///
/// [필수 연결 (Inspector)]
///  - canvasGroup : 이 오버레이의 표시/숨김을 담당할 CanvasGroup (비워두면 자기 GameObject에서 탐색)
///  - background  : 배경 Image — 대상 고유색으로 tint됨
///  - mainText    : "OO 사망" / "OO 다운" 텍스트
///
/// [배경 — 플레이테스트 피드백]
/// "죽은지 잘 모르겠다". 사망 즉시(1프레임 후) 씬이 리로드돼(현재는
/// StageNetworkState.deathReloadDelay로 연장됨) 그 사이를 채워줄 전용 연출이 없었다. 이 게임은
/// 1명 사망 = 전원 리로드(§11)라, 본인이 안 죽었어도 팀원이 죽으면 화면이 갑자기 리셋된다 —
/// 그 이유를 알려주기 위해 대상 CheerName("BERRY Died")과 그 사람의 고유색(배경/텍스트)을 표시한다.
/// 본인/팀원 구분 없이 동일 로직 — 배경색이 곧 "누가 죽었는지"를 알려준다.
///
/// [다운(2026-09-14 추가) — DownedReviveSystemDesign.md §6 "BERRY DOWN"]
/// 사망과 동일한 연출(페이드인+팝인, 고유색 배경)을 재사용하되, 사망은 곧 씬 리로드가 컷해주는
/// 반면 다운은 리로드 없이 게임이 계속되므로 <see cref="downHoldDuration"/> 후 자동 페이드아웃한다.
/// 여러 명이 순차로 다운되면(§6 "각각 순서대로 표시 가능") 뒤 이벤트가 앞 연출을 그대로 이어받아
/// 재생한다 — 별도 큐 없이 Show 계열 재호출 시 진행 중 코루틴을 멈추고 새로 시작하는 기존 방식 그대로.
///
/// [트리거]
/// 각 Player의 PlayerEvents.OnDied/OnDowned — Owner/비Owner 모두 이미 전 클라이언트에 복제되어
/// 호출되므로(§11, PlayerDownState의 IsDowned NV 콜백) 여기서 추가 네트워크 브릿지가 필요 없다.
/// </summary>
public class DeathOverlayUI : MonoBehaviour
{
    [Header("연결 (Prefab/씬에서 직접 배치 후 연결)")]
    [Tooltip("표시/숨김용 CanvasGroup. 비워두면 이 GameObject에서 자동 탐색.")]
    [SerializeField] CanvasGroup      canvasGroup;
    [Tooltip("배경 Image. 대상 고유색으로 tint됨. 비워두면 배경 색 반영을 건너뜀.")]
    [SerializeField] Image            background;
    [Tooltip("\"{CheerName} 사망/다운\" 텍스트.")]
    [SerializeField] TextMeshProUGUI  mainText;

    [Header("문구")]
    [Tooltip("DeathUI 테이블 Death.Format — \"{0} 사망\". 비어 있으면 한국어 폴백.")]
    [SerializeField] LocalizedString deathMessage;
    [Tooltip("DeathUI 테이블 Down.Format — \"{0} 다운\". 비어 있으면 한국어 폴백.")]
    [SerializeField] LocalizedString downMessage;

    [Header("타이밍(초)")]
    [SerializeField] float fadeInDuration  = 0.25f;
    [SerializeField] float popInDuration   = 0.25f;
    [SerializeField] float popInStartScale = 1.6f;

    [Header("다운 전용 — 자동 숨김(초)")]
    [Tooltip("사망과 달리 다운은 리로드가 없어 이 시간 뒤 자동으로 사라진다.")]
    [SerializeField] float downHoldDuration     = 2f;
    [SerializeField] float downFadeOutDuration  = 0.3f;

    [Header("배경")]
    [Tooltip("대상 고유색을 배경에 입힐 때 쓸 알파(투명도).")]
    [SerializeField] float backgroundAlpha = 0.85f;

    Coroutine _fadeRoutine;
    Coroutine _popRoutine;
    Coroutine _autoHideRoutine;

    readonly List<Action> _unsubscribers = new();

    // RebuildSubscriptions 재호출 코얼레싱 — TeamStatusUI.RequestRebuild와 동일 이유/패턴
    bool _rebuildPending;

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
        PlayerSpawnCoordinator.OnPlayersReady += RequestRebuild;
        PlayerSpawnCoordinator.OnRosterChanged += RequestRebuild;
        if (PlayerSpawnCoordinator.IsReady) RequestRebuild();
    }

    /// <summary>(다시) 켜질 때 구독을 따라잡는다 — 이유는 TeamStatusUI.OnEnable과 동일.</summary>
    void OnEnable() => RequestRebuild();

    /// <summary>비활성화 중 예약된 리빌드는 코루틴이 정지되므로 플래그를 되돌려 놓는다.</summary>
    void OnDisable() => _rebuildPending = false;

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= RequestRebuild;
        PlayerSpawnCoordinator.OnRosterChanged -= RequestRebuild;
        UnsubscribeAll();
    }

    /// <summary>
    /// RebuildSubscriptions() 재호출을 다음 프레임으로 1회만 합친다.
    /// 이유는 TeamStatusUI.RequestRebuild와 동일(§ 배치 스폰 중복 + Despawn 타이밍, NetworkDesign.md 참고).
    /// </summary>
    void RequestRebuild()
    {
        if (_rebuildPending) return;
        // 비활성이면 플래그를 세우지 않는다 — StartCoroutine이 시작되지 않는데 _rebuildPending만
        // 남으면 복구 경로가 없어 OnDied 구독이 영구히 안 붙는다(TeamStatusUI.RequestRebuild와
        // 동일 원인·동일 수정, 2026-09-05). 놓친 요청은 OnEnable이 따라잡는다.
        if (!isActiveAndEnabled) return;
        _rebuildPending = true;
        StartCoroutine(RebuildNextFrame());
    }

    IEnumerator RebuildNextFrame()
    {
        yield return null;
        _rebuildPending = false;
        RebuildSubscriptions();
    }

    // ── 구독 (TeamStatusUI.RefreshSlots와 동일한 재구성 패턴) ────────

    void RebuildSubscriptions()
    {
        if (!isActiveAndEnabled) return;

        UnsubscribeAll();

        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p == null) continue;
            var net = p.GetComponent<NetworkObject>();
            if (net != null && !net.IsSpawned) continue;
            PlayerEvents events = p.GetComponent<PlayerEvents>();
            if (events == null) continue;

            Player captured = p;
            Action diedHandler = () => HandlePlayerDied(captured);
            Action downedHandler = () => HandlePlayerDowned(captured);
            events.OnDied += diedHandler;
            events.OnDowned += downedHandler;
            _unsubscribers.Add(() =>
            {
                events.OnDied -= diedHandler;
                events.OnDowned -= downedHandler;
            });
        }
    }

    void UnsubscribeAll()
    {
        foreach (var unsubscribe in _unsubscribers) unsubscribe();
        _unsubscribers.Clear();
    }

    // ── 사망 / 다운 처리 ──────────────────────────────────────────

    void HandlePlayerDied(Player deadPlayer)
    {
        if (deadPlayer == null) return;
        ApplyMessage(deadPlayer, FormatDeathMessage);

        // 사망은 곧 리로드가 컷해준다 — 직전에 다운 배너의 자동 숨김이 예약돼 있었다면 취소해서
        // 사망 배너가 중간에 꺼지지 않게 한다.
        if (_autoHideRoutine != null)
        {
            StopCoroutine(_autoHideRoutine);
            _autoHideRoutine = null;
        }
        Show();
    }

    void HandlePlayerDowned(Player downedPlayer)
    {
        if (downedPlayer == null) return;
        ApplyMessage(downedPlayer, FormatDownMessage);
        ShowDowned();
    }

    /// <summary>대상 고유색으로 배경/텍스트를 갱신 — 본인/팀원 구분 없이 동일 로직.</summary>
    void ApplyMessage(Player target, Func<string, string> formatter)
    {
        int colorIndex = ResolveColorIndex(target);

        if (mainText != null)
            mainText.text = formatter(GetPlayerCheerName(colorIndex));

        Color accent = PlayerColorUtil.GetUniqueColor(
            colorIndex >= 0 && colorIndex < PlayerColorUtil.ColorOrder.Length
                ? PlayerColorUtil.ColorOrder[colorIndex]
                : PlayerColorType.Blue);

        // 배경은 대상의 고유색 그대로(누가 죽었는지/다운됐는지 배경만 봐도 알 수 있게).
        if (background != null)
            background.color = new Color(accent.r, accent.g, accent.b, backgroundAlpha);
        if (mainText != null)
            mainText.color = ReadableTextColorFor(accent);
    }

    const string FallbackDeathFormat = "{0} 사망";
    const string FallbackDownFormat  = "{0} 다운";

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

    /// <summary>DeathUI/Down.Format. 테이블 미연결·미로드면 한국어 폴백.</summary>
    string FormatDownMessage(string displayName)
    {
        if (downMessage != null && !downMessage.IsEmpty)
        {
            string localized = downMessage.GetLocalizedString(displayName);
            if (!string.IsNullOrEmpty(localized)) return localized;
        }
        return string.Format(FallbackDownFormat, displayName);
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

    /// <summary>Show()와 동일한 등장 연출 + downHoldDuration 후 자동 페이드아웃(사망과 달리 리로드가 없다).</summary>
    void ShowDowned()
    {
        Show();

        if (canvasGroup == null) return;
        if (_autoHideRoutine != null) StopCoroutine(_autoHideRoutine);
        _autoHideRoutine = StartCoroutine(AutoHideAfterDown());
    }

    IEnumerator AutoHideAfterDown()
    {
        yield return new WaitForSeconds(fadeInDuration + downHoldDuration);

        float from    = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < downFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, 0f, elapsed / downFadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
        _autoHideRoutine = null;
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
        return string.IsNullOrEmpty(name) ? "???" : name.ToUpperInvariant();
    }
}
