using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 팀 공유 목숨(ReviveSystemDesign.md §4) 상시 HUD — 아이콘 + 남은 개수. 읽기 전용(네트워크 쓰기 없음).
/// 이벤트 대신 매 프레임 폴링: Client는 NV 초기값이 스폰 데이터로 들어오면 OnValueChanged가 불리지 않는다.
///
/// [왜 강조가 필요한가 — §11.3]
/// 목숨은 이 게임의 유일한 실패 자원인데 **소모되는 장면을 아무도 못 볼 가능성이 크다.** 본인은 죽고
/// 1초 만에 맵 반대편으로 옮겨져 HUD 구석 숫자를 볼 여유가 없고, 팀원은 저 멀리서 일어난 일이라
/// 화면에 아무 변화가 없다. §1.2에서 협동 요소(살리러 가기)를 포기하면서 팀 긴장을 목숨 하나에
/// 몰아줬으므로, 그게 안 보이면 긴장 장치가 통째로 작동하지 않는다. 그래서 두 가지만 얹는다:
///   ① 소모 순간 아이콘 펄스 + 색 플래시 (전원 화면에서 동일하게)
///   ② 목숨 0이면 상시 빨강 — "다음에 죽으면 끝"이 실질 긴장 지점이다
/// 문구가 하나도 없어 로컬라이제이션도, Noto Static 베이킹도 필요 없다. "누가 죽었는지"는 일부러
/// 안 알린다 — 어차피 즉시 부활하므로 행동에 영향을 주지 않는 정보다.
///
/// [숨김] StageNetworkState 없는 씬(튜토리얼 — 목숨 제한 없음), 목숨 미초기화, 솔로(항상 0이라 의미 없음).
/// [구성] content = 아이콘+숫자를 담은 **자식** 오브젝트. 이 컴포넌트가 붙은 오브젝트를 넣으면 꺼진 뒤 다시 켜지지 않는다.
/// </summary>
public class TeamLivesUI : MonoBehaviour
{
    [SerializeField] GameObject content;
    [SerializeField] TextMeshProUGUI countText;

    [Header("소모 강조 (§11.3)")]
    [Tooltip("펄스를 줄 아이콘. 비워두면 countText만 강조된다.")]
    [SerializeField] Graphic icon;

    [Tooltip("목숨이 줄어드는 순간의 강조 시간(초).")]
    [SerializeField] float pulseDuration = 1f;

    [Tooltip("펄스 최대 배율. 1.6이면 순간적으로 1.6배까지 커졌다 원래 크기로 돌아온다.")]
    [SerializeField] float pulseScale = 1.6f;

    [Tooltip("소모 순간 플래시 색.")]
    [SerializeField] Color pulseColor = new Color(1f, 0.35f, 0.35f);

    [Tooltip("목숨 0일 때 상시 유지할 경고색.")]
    [SerializeField] Color zeroColor = new Color(1f, 0.25f, 0.25f);

    int _shown = int.MinValue;
    float _pulseEndTime = -1f;

    // 인스펙터에 칠해둔 원래 색 — 펄스/경고에서 되돌릴 기준값. Awake에서 1회 캡처한다.
    Color _iconBaseColor  = Color.white;
    Color _textBaseColor  = Color.white;
    Vector3 _iconBaseScale = Vector3.one;
    Vector3 _textBaseScale = Vector3.one;

    void Awake()
    {
        if (icon != null)
        {
            _iconBaseColor = icon.color;
            _iconBaseScale = icon.rectTransform.localScale;
        }
        if (countText != null)
        {
            _textBaseColor = countText.color;
            _textBaseScale = countText.rectTransform.localScale;
        }
    }

    void Update()
    {
        int lives = ReadVisibleLives();

        if (lives != _shown)
        {
            // ⚠️ 감소일 때만 펄스. 최초 확정(-1 → 인원−1)과 스테이지 진입 시 값 세팅에서 울리면 안 된다(§11.3).
            bool consumed = _shown >= 0 && lives >= 0 && lives < _shown;
            _shown = lives;

            if (content != null) content.SetActive(lives >= 0);
            if (countText != null && lives >= 0) countText.text = lives.ToString();

            if (consumed) _pulseEndTime = Time.time + Mathf.Max(0.01f, pulseDuration);
        }

        ApplyEmphasis(lives);
    }

    /// <summary>펄스(감소 직후 1초)와 0 경고(상시)를 아이콘·숫자에 반영. 둘 다 아니면 원래 값으로 되돌린다.</summary>
    void ApplyEmphasis(int lives)
    {
        bool  pulsing = Time.time < _pulseEndTime;
        bool  warning = lives == 0;

        // 1 → 0으로 떨어지며 남은 펄스 구간: 진행도 t는 1에서 0으로 내려간다.
        float t = pulsing ? Mathf.Clamp01((_pulseEndTime - Time.time) / Mathf.Max(0.01f, pulseDuration)) : 0f;
        float scaleMul = 1f + (pulseScale - 1f) * t;

        Color iconTarget = warning ? zeroColor : _iconBaseColor;
        Color textTarget = warning ? zeroColor : _textBaseColor;
        if (pulsing)
        {
            iconTarget = Color.Lerp(iconTarget, pulseColor, t);
            textTarget = Color.Lerp(textTarget, pulseColor, t);
        }

        if (icon != null)
        {
            icon.color = iconTarget;
            icon.rectTransform.localScale = _iconBaseScale * scaleMul;
        }
        if (countText != null)
        {
            countText.color = textTarget;
            countText.rectTransform.localScale = _textBaseScale * scaleMul;
        }
    }

    /// <summary>화면에 표시할 남은 목숨. 숨겨야 하면 -1.</summary>
    public static int ReadVisibleLives()
    {
        var state = StageNetworkState.Instance;
        if (state == null || PlayerSpawnCoordinator.EntryCount <= 1) return -1;
        return state.TeamLivesRemaining;
    }
}
