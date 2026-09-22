using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Tutorial·Interlude 모이는 곳(TutorialGatherZone)의 Start 간판을 월드에 표시한다.
///
///  [대기]       Start 간판. 모이는 곳 자체는 도착 지점과 같은 발판(ZonePadVisual)이라 인원 표시는 하지 않는다.
///  [카운트다운] 전원이 모이면 간판 글씨를 숨기고 큰 숫자 3·2·1. 숫자가 바뀔 때마다 countdownSfx 1회.
///               누가 나가서 리셋되면 다시 Start 글씨.
///
/// 판정·카운트다운 진행은 TutorialNetworkManager(Tutorial) / InterludeNetworkManager(Interlude)가 Host에서
/// 소유 — 여기선 이벤트 구독과 표시만 한다. 둘 중 씬에 있는 쪽 하나만 연결한다(이벤트 3종이 동일).
/// </summary>
public class TutorialGatherDisplay : MonoBehaviour
{
    [Tooltip("Tutorial 씬: TutorialNetworkManager. Interlude 씬에선 비워 둔다.")]
    [FormerlySerializedAs("gate")]
    [SerializeField] TutorialNetworkManager tutorialGate;
    [Tooltip("Interlude 씬: InterludeNetworkManager. Tutorial 씬에선 비워 둔다.")]
    [SerializeField] InterludeNetworkManager interludeGate;

    [Header("간판")]
    [Tooltip("Start 글씨 판. 카운트다운 동안 숨김.")]
    [SerializeField] Renderer signLabel;
    [Tooltip("카운트다운 숫자(TextMeshPro 3D). 카운트다운 동안만 보임.")]
    [SerializeField] TMP_Text countdownText;

    [Header("SFX")]
    [Tooltip("3·2·1 숫자가 바뀔 때마다 1회.")]
    [SerializeField] SFXId countdownSfx = SFXId.Minigame_CountdownTick;

    float _maxTick;
    int _digit;

    void Awake()
    {
        if (tutorialGate != null)
        {
            tutorialGate.OnGateCountdownTick.AddListener(HandleTick);
            tutorialGate.OnGateCountdownReset.AddListener(HandleReset);
            tutorialGate.OnGateCountdownComplete.AddListener(HandleReset);
        }
        if (interludeGate != null)
        {
            interludeGate.OnGateCountdownTick.AddListener(HandleTick);
            interludeGate.OnGateCountdownReset.AddListener(HandleReset);
            interludeGate.OnGateCountdownComplete.AddListener(HandleReset);
        }
    }

    void OnDestroy()
    {
        if (tutorialGate != null)
        {
            tutorialGate.OnGateCountdownTick.RemoveListener(HandleTick);
            tutorialGate.OnGateCountdownReset.RemoveListener(HandleReset);
            tutorialGate.OnGateCountdownComplete.RemoveListener(HandleReset);
        }
        if (interludeGate != null)
        {
            interludeGate.OnGateCountdownTick.RemoveListener(HandleTick);
            interludeGate.OnGateCountdownReset.RemoveListener(HandleReset);
            interludeGate.OnGateCountdownComplete.RemoveListener(HandleReset);
        }
    }

    void Start() => HandleReset();

    // ── 카운트다운 ───────────────────────────────────────────────

    /// <summary>대기 중에도 duration 값으로 Tick이 오므로(TutorialNetworkManager), 지금까지 본 최댓값보다
    /// 줄어든 Tick부터 카운트다운으로 본다.</summary>
    void HandleTick(float remaining)
    {
        if (remaining > _maxTick) _maxTick = remaining;
        if (remaining >= _maxTick - 0.001f || remaining <= 0f) return;

        int digit = Mathf.CeilToInt(remaining);
        if (digit == _digit) return;
        _digit = digit;

        if (signLabel != null) signLabel.enabled = false;
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = digit.ToString();
        }
        if (SFXManager.Instance != null) SFXManager.Instance.Play(countdownSfx);
    }

    void HandleReset()
    {
        _digit = 0;
        if (signLabel != null) signLabel.enabled = true;
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }
}
