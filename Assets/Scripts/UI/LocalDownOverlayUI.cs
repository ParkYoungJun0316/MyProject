using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using TMPro;

/// <summary>
/// 본인(로컬 Owner)이 다운됐을 때만 뜨는 화면 오버레이 — DownedReviveSystemDesign.md §6 "다운 진입 (본인)".
/// 팀원 다운은 TeamStatusUI, 완전사망은 DeathOverlayUI 담당이고 이 컴포넌트는 로컬 표시 전용(네트워크 쓰기 없음).
///
/// [구성 (Inspector)]
///  - grayFader    : 회색 전체화면 Image + CanvasGroup에 붙인 ScreenFader. MouthController용 인스턴스와 공유 금지.
///  - contentGroup : 타이머 + 안내 문구 컨테이너. grayFader보다 위(뒤 형제)에 배치.
///  - timerText / guideText : 흰색 고정(회색 막 위 가독성).
///
/// [연출]
/// 회색 막 알파 = 1 − RemainingDownTime / DownTimeoutDuration. 코루틴이 아니라 매 프레임 남은 시간으로
/// 구동해야 부활 시전 중 정지·캔슬 시 복원(§3, §9.2)과 타이머 숫자가 어긋나지 않는다.
/// 부활 → 막·문구 페이드아웃. 완전사망 → 문구만 즉시 숨기고 막은 리로드까지 유지(DeathOverlayUI가 이어받음).
/// </summary>
public class LocalDownOverlayUI : MonoBehaviour
{
    [Header("회색 막")]
    [SerializeField] ScreenFader grayFader;
    [SerializeField] float grayFadeOutDuration = 0.4f;

    [Header("안내 UI")]
    [SerializeField] CanvasGroup contentGroup;
    [SerializeField] TextMeshProUGUI timerText;
    [SerializeField] TextMeshProUGUI guideText;
    [Tooltip("남은 팀 목숨 강조 표시(DownedReviveSystemDesign.md §4B) — TeamLivesUI와 같은 값을 가까이서 다시 보여준다.")]
    [SerializeField] TextMeshProUGUI livesText;
    [Tooltip("목숨 아이콘+숫자 묶음. 목숨을 표시하지 않는 경우(튜토리얼·솔로) 통째로 숨긴다.")]
    [SerializeField] GameObject livesGroup;
    [SerializeField] Color textColor = Color.white;
    [SerializeField] float contentFadeDuration = 0.25f;

    [Header("문구")]
    [Tooltip("DeathUI 테이블 Down.Guide. 비어 있으면 한국어 폴백.")]
    [SerializeField] LocalizedString guideMessage;
    [Tooltip("DeathUI 테이블 Down.Reviving. 비어 있으면 한국어 폴백.")]
    [SerializeField] LocalizedString revivingMessage;

    const string FallbackGuide    = "쓰러졌습니다! 팀원이 곁에서 [E]를 누르면 부활합니다.";
    const string FallbackReviving = "부활 중...";

    Player _player;
    PlayerEvents _events;
    PlayerDownState _downState;

    bool _showing;
    int _shownSeconds = -1;
    int _shownReviving = -1; // -1 미표시, 0 안내 문구, 1 부활 중
    int _shownLives = int.MinValue;
    Coroutine _contentFade;

    void Awake()
    {
        if (contentGroup != null)
        {
            contentGroup.alpha = 0f;
            contentGroup.blocksRaycasts = false;
            contentGroup.interactable = false;
        }
        if (timerText != null) timerText.color = textColor;
        if (guideText != null) guideText.color = textColor;
        if (livesText != null) livesText.color = textColor;
    }

    void Start()
    {
        PlayerSpawnCoordinator.OnPlayersReady += Bind;
        if (PlayerSpawnCoordinator.IsReady) Bind();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= Bind;
        Unbind();
    }

    // ── 로컬 플레이어 바인딩 ───────────────────────────────────────

    void Bind()
    {
        Player local = FindLocalOwnerPlayer();
        if (ReferenceEquals(local, _player)) return;

        Unbind();
        _player = local;
        if (_player == null) return;

        _events = _player.GetComponent<PlayerEvents>();
        _downState = _player.GetComponent<PlayerDownState>();
        if (_events != null)
        {
            _events.OnDowned += EnterDown;
            _events.OnRevived += ExitDown;
            _events.OnDied += HandleDied;
        }

        if (_downState != null && _downState.IsDowned && !_player.IsDead) EnterDown();
    }

    void Unbind()
    {
        if (_events != null)
        {
            _events.OnDowned -= EnterDown;
            _events.OnRevived -= ExitDown;
            _events.OnDied -= HandleDied;
        }
        _events = null;
        _downState = null;
        _player = null;
    }

    static Player FindLocalOwnerPlayer()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var net = p.GetComponent<NetworkObject>();
            if (net != null && net.IsSpawned && net.IsOwner) return p;
        }
        return null;
    }

    // ── 상태 전환 ─────────────────────────────────────────────────

    void EnterDown()
    {
        if (_showing || _downState == null) return;
        _showing = true;
        _shownSeconds = -1;
        _shownReviving = -1;
        FadeContent(1f);
        Refresh();
    }

    void ExitDown()
    {
        if (!_showing) return;
        _showing = false;
        FadeContent(0f);
        if (grayFader != null) grayFader.FadeIn(grayFadeOutDuration);
    }

    void HandleDied()
    {
        if (!_showing) return;
        _showing = false;
        if (_contentFade != null)
        {
            StopCoroutine(_contentFade);
            _contentFade = null;
        }
        if (contentGroup != null) contentGroup.alpha = 0f;
    }

    void Update()
    {
        if (!_showing) return;
        if (_player == null || _downState == null || _player.IsDead || !_downState.IsDowned)
        {
            ExitDown();
            return;
        }
        Refresh();
    }

    void Refresh()
    {
        float total = _downState.DownTimeoutDuration;
        float remaining = _downState.RemainingDownTime;
        if (grayFader != null && total > 0f) grayFader.SetProgress(1f - remaining / total);

        int seconds = Mathf.CeilToInt(remaining);
        if (timerText != null && seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            timerText.text = seconds.ToString();
        }

        int reviving = _downState.IsBeingRevived ? 1 : 0;
        if (guideText != null && reviving != _shownReviving)
        {
            _shownReviving = reviving;
            guideText.text = reviving == 1
                ? Localize(revivingMessage, FallbackReviving)
                : Localize(guideMessage, FallbackGuide);
        }

        // 팀 공유 목숨(§4B) — TeamLivesUI와 같은 값을 본인 다운 화면에서 다시 강조 표시.
        int lives = TeamLivesUI.ReadVisibleLives();
        if (lives != _shownLives)
        {
            _shownLives = lives;
            if (livesGroup != null) livesGroup.SetActive(lives >= 0);
            if (livesText != null && lives >= 0) livesText.text = lives.ToString();
        }
    }

    static string Localize(LocalizedString localized, string fallback)
    {
        if (localized != null && !localized.IsEmpty)
        {
            string value = localized.GetLocalizedString();
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return fallback;
    }

    // ── 문구 페이드 ───────────────────────────────────────────────

    void FadeContent(float target)
    {
        if (contentGroup == null) return;
        if (_contentFade != null) StopCoroutine(_contentFade);
        _contentFade = StartCoroutine(FadeContentRoutine(target));
    }

    IEnumerator FadeContentRoutine(float target)
    {
        float from = contentGroup.alpha;
        float elapsed = 0f;
        while (elapsed < contentFadeDuration)
        {
            elapsed += Time.deltaTime;
            contentGroup.alpha = Mathf.Lerp(from, target, elapsed / contentFadeDuration);
            yield return null;
        }
        contentGroup.alpha = target;
        _contentFade = null;
    }
}
