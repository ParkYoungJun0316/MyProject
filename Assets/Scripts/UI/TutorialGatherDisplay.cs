using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Tutorial 모이는 곳(TutorialGatherZone)을 월드에 표시한다 — SideSplitWorldDisplay와 같은 표시 언어.
///
///  [대기]       Start 간판 + 아래 하트 패널: 접속 인원만큼 하트. 존에 들어온 사람의 하트는 그 사람 색,
///               아직 안 온 사람은 빈 하트 — "누가 안 왔는지"가 보인다.
///  [카운트다운] 전원이 모이면 간판 글씨를 숨기고 큰 숫자 3·2·1. 숫자가 바뀔 때마다 countdownSfx 1회.
///               누가 나가서 리셋되면 다시 Start 글씨.
///
/// 판정·카운트다운 진행은 TutorialNetworkManager(Tutorial) / InterludeNetworkManager(Interlude)가 Host에서
/// 소유 — 여기선 이벤트 구독과 표시만 한다. 둘 중 씬에 있는 쪽 하나만 연결한다(이벤트 3종이 동일).
/// 존 점유는 각 머신이 로컬 트리거로 보는 값이라 머신마다 한 프레임 정도 어긋날 수 있다(표시 전용).
/// </summary>
public class TutorialGatherDisplay : MonoBehaviour
{
    [Tooltip("Tutorial 씬: TutorialNetworkManager. Interlude 씬에선 비워 둔다.")]
    [FormerlySerializedAs("gate")]
    [SerializeField] TutorialNetworkManager tutorialGate;
    [Tooltip("Interlude 씬: InterludeNetworkManager. Tutorial 씬에선 비워 둔다.")]
    [SerializeField] InterludeNetworkManager interludeGate;
    [SerializeField] TutorialGatherZone zone;

    [Header("간판")]
    [Tooltip("Start 글씨 판. 카운트다운 동안 숨김.")]
    [SerializeField] Renderer signLabel;
    [Tooltip("카운트다운 숫자(TextMeshPro 3D). 카운트다운 동안만 보임.")]
    [SerializeField] TMP_Text countdownText;

    [Header("하트")]
    [Tooltip("하트가 가로로 나열될 기준점(하트 패널 중앙, 앞면 쪽).")]
    [SerializeField] Transform heartRow;
    [SerializeField] Sprite heartEmpty;
    [SerializeField] Sprite heartBlue;
    [SerializeField] Sprite heartPurple;
    [SerializeField] Sprite heartGreen;
    [SerializeField] Sprite heartYellow;
    [Tooltip("빈 하트(아직 안 온 사람) 색. 흰 스프라이트에 곱해진다.")]
    [SerializeField] Color emptyHeartColor = new Color(0.72f, 1f, 0.93f, 1f);
    [SerializeField] float heartSize = 2.2f;
    [SerializeField] float heartGap = 0.6f;

    [Header("SFX")]
    [Tooltip("3·2·1 숫자가 바뀔 때마다 1회.")]
    [SerializeField] SFXId countdownSfx = SFXId.Minigame_CountdownTick;

    readonly List<SpriteRenderer> _hearts = new List<SpriteRenderer>();
    readonly List<(ulong ClientId, PlayerColorType Color)> _roster = new List<(ulong, PlayerColorType)>();
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

    void Update() => RefreshHearts();

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

    // ── 하트 ─────────────────────────────────────────────────────

    void RefreshHearts()
    {
        if (heartRow == null) return;

        _roster.Clear();
        foreach (var entry in PlayerSpawnCoordinator.GetAllEntries()) _roster.Add(entry);
        _roster.Sort((a, b) => PlayerColorUtil.ColorTypeToIndex(a.Color).CompareTo(PlayerColorUtil.ColorTypeToIndex(b.Color)));

        int count = _roster.Count;
        while (_hearts.Count < count)
        {
            var go = new GameObject("Heart");
            go.transform.SetParent(heartRow, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1; // 반투명 패널보다 항상 앞
            _hearts.Add(sr);
        }

        float total = count * heartSize + Mathf.Max(0, count - 1) * heartGap;
        for (int i = 0; i < _hearts.Count; i++)
        {
            var sr = _hearts[i];
            bool on = i < count;
            if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on);
            if (!on) continue;

            bool arrived = zone != null && zone.Contains(_roster[i].ClientId);
            sr.sprite = arrived ? GetHeartSprite(_roster[i].Color) : heartEmpty;
            sr.color  = arrived ? Color.white : emptyHeartColor;

            Vector2 spriteSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            float s = spriteSize.x > 0f ? heartSize / spriteSize.x : 1f;
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.transform.localPosition = new Vector3(-total * 0.5f + heartSize * 0.5f + i * (heartSize + heartGap), 0f, 0f);
            sr.transform.localRotation = Quaternion.identity;
        }
    }

    Sprite GetHeartSprite(PlayerColorType color) => color switch
    {
        PlayerColorType.Blue   => heartBlue,
        PlayerColorType.Purple => heartPurple,
        PlayerColorType.Green  => heartGreen,
        PlayerColorType.Yellow => heartYellow,
        _                      => heartEmpty,
    };
}
