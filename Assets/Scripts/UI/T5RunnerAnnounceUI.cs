using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// T.Stage5 라운드 시작 알림 — "OO님이 러너" + 3·2·1 카운트다운.
/// `TStage5RunnerRedesign.md` §1.1 · §1.5.
///
/// [표시 구간]
/// 라운드 시작(`roundStartServerTime`)부터 `introSeconds`(3초) 동안만. 그 뒤엔 자동으로 숨는다.
/// 값을 전부 NV에서 읽으므로 **Host/Client가 같은 순간에 같은 글자**를 본다 — 알림용 RPC가 없다.
///
/// [문구]
/// 러너 이름은 CheerName 고정값(`CheerService.GetCheerName`) — DeathOverlayUI와 같은 소스다.
/// Steam/OS DisplayName이 아니다.
/// 내가 러너일 때는 이름 대신 "당신이 러너" 쪽 문구를 쓴다 — 자기 이름을 3인칭으로 읽는 건 어색하다.
///
/// [로컬라이즈]
/// LocalizedString 2개(러너 지목 / 본인 지목)를 인스펙터에 물린다. 테이블이 아직 없거나 로드
/// 전이면 한국어 폴백을 쓴다 — DeathOverlayUI·OptionsMenuController와 동일한 패턴.
///
/// [네트워크] 쓰기 없음. 읽기·표시 전용.
///
/// [씬 설정] Tip/Objective와 겹치지 않는 자리에 Text 하나를 두고 이 스크립트를 붙인 뒤
/// messageText / countdownText를 물린다. 둘 다 비워도 죽지 않는다(그 줄만 안 보임).
/// </summary>
public class T5RunnerAnnounceUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("\"OO님이 러너\" 문구")]
    [SerializeField] TextMeshProUGUI messageText;

    [Tooltip("3·2·1 숫자. 비우면 숫자를 표시하지 않는다.")]
    [SerializeField] TextMeshProUGUI countdownText;

    [Header("타이밍")]
    [Tooltip("라운드 디렉터의 introSeconds와 같은 값으로 맞출 것. 알림이 보이는 시간.")]
    [SerializeField] float introSeconds = 3f;

    [Header("로컬라이즈")]
    [Tooltip("다른 사람이 러너일 때. {0} = 러너 CheerName")]
    [SerializeField] LocalizedString runnerMessage;

    [Tooltip("내가 러너일 때. 인자 없음")]
    [SerializeField] LocalizedString youAreRunnerMessage;

    const string FallbackRunnerFormat = "{0}님이 러너입니다";
    const string FallbackYouAreRunner = "당신이 러너입니다";

    int _shownRound   = int.MinValue;
    int _shownSeconds = int.MinValue;

    void Awake() => SetVisible(false);

    void Update()
    {
        var net = StageNetworkState.Instance;
        var nm  = NetworkManager.Singleton;
        if (net == null || nm == null || !nm.IsListening) { SetVisible(false); return; }

        T5RoundState st = net.T5Round;
        if (st.roundIndex < 0) { SetVisible(false); return; }

        double now       = nm.ServerTime.Time;
        double introEnds = st.roundStartServerTime + introSeconds;
        if (now >= introEnds) { SetVisible(false); return; }

        SetVisible(true);

        // 라운드가 바뀌었을 때만 문구를 다시 만든다(매 프레임 문자열 조립 방지).
        if (st.roundIndex != _shownRound)
        {
            _shownRound = st.roundIndex;
            if (messageText != null) messageText.text = BuildMessage(net, nm);
        }

        int seconds = Mathf.Max(1, Mathf.CeilToInt((float)(introEnds - now)));
        if (seconds != _shownSeconds)
        {
            _shownSeconds = seconds;
            if (countdownText != null) countdownText.text = seconds.ToString();
        }
    }

    // ── 내부 ────────────────────────────────────────────────────

    string BuildMessage(StageNetworkState net, NetworkManager nm)
    {
        ulong runnerId = net.T5CurrentRunnerClientId;

        if (runnerId == nm.LocalClientId)
            return Localize(youAreRunnerMessage, FallbackYouAreRunner);

        string name = RunnerName(runnerId);

        if (runnerMessage != null && !runnerMessage.IsEmpty)
        {
            string localized = runnerMessage.GetLocalizedString(name);
            if (!string.IsNullOrEmpty(localized)) return localized;
        }
        return string.Format(FallbackRunnerFormat, name);
    }

    /// <summary>러너의 CheerName. TeamStatusUI/PlayerNameTagUI와 같은 해석 경로.</summary>
    static string RunnerName(ulong clientId)
    {
        if (!PlayerSpawnCoordinator.TryGetColor(clientId, out PlayerColorType color)) return "???";

        int    index = System.Array.IndexOf(PlayerColorUtil.ColorOrder, color);
        string name  = CheerService.GetCheerName(index);
        return string.IsNullOrEmpty(name) ? "???" : name.ToUpper();
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

    void SetVisible(bool visible)
    {
        if (messageText   != null && messageText.gameObject.activeSelf   != visible) messageText.gameObject.SetActive(visible);
        if (countdownText != null && countdownText.gameObject.activeSelf != visible) countdownText.gameObject.SetActive(visible);

        if (!visible) { _shownRound = int.MinValue; _shownSeconds = int.MinValue; }
    }
}
