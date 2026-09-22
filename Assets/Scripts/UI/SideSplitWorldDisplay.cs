using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// SideSplit 라운드를 월드에 직접 표시한다 — 화면 UI·문구 로컬라이제이션 없음.
///
///  [카운트다운] 간판의 모양을 가리고 큰 숫자 3·2·1. 숫자가 바뀔 때마다 countdownSfx 1회.
///               (이 동안 zoneRig가 재배치돼도 네 간판이 똑같아 보여서 공개 순간 "다른 자리에서 나타남")
///  [공개]       간판 모양 + 간판 아래 하트 패널: 하트 개수 = 필요 인원, 색 하트 = 그 색 필수, 빈 하트 = 아무나.
///               0명 방향은 하트만 없다 — 간판·타이머는 네 방향 모두 똑같이 보여서, 판단은 하트로만 한다.
///
///  [정보 비대칭 — MinigameDesign.md §1.9.2] 하트 패널은 **자기 담당 모양 하나만** 보인다.
///               담당은 모양↔설계슬롯 4색 1:1 고정(ownerColor)이고, 이번 판에 없는 색의 모양은
///               SessionColorSlotMap의 "빠진 색 슬롯 → Common" 규칙대로 전원에게 공개된다.
///               그래서 인원별 분기 코드가 없고, 어느 인원에서도 각자 "나만 아는 패널 1개"를 갖는다.
///               이 클래스는 표시 전용·클라이언트 로컬이라 머신마다 다르게 보여도 규칙이 깨지지 않는다 —
///               판정은 Host의 SideSplitChallenge.Judge()가 이것과 무관하게 따로 한다.
///  [진행]       네 간판 모두 글씨 뒤 배경이 꽉 찬 색에서 한쪽으로 줄어든다(채움 텍스처 알파 =
///               위치 그라데이션, 머티리얼 _Cutoff로 잘라냄). 마지막 warnSeconds초는 경고색.
///               공개 직후 읽는 시간(SideSplitChallenge.revealReadHold) 동안은 꽉 찬 채 멈춰 있고,
///               줄기 시작하는 순간부터 timerSfx 루프.
///  [결과]       채움이 성공/실패 색으로 꽉 참.
///
/// SideSplitChallenge 이벤트(전 머신 동일 시점 발동)만 구독 — 판정·진행 상태는 소유하지 않는다.
/// 모든 표시 오브젝트가 zoneRig 자식이면 재배치를 그대로 따라간다.
/// </summary>
public class SideSplitWorldDisplay : MonoBehaviour
{
    [System.Serializable]
    public class DirectionView
    {
        public SideSplitDirection direction;
        [Tooltip("이 간판 모양의 담당 색(설계슬롯). 그 색 플레이어에게만 하트 패널이 보인다. " +
                 "이번 판에 없는 색이면 주인 없는 모양이 되어 전원에게 공개된다(§1.9.2).")]
        public PlayerColorType ownerColor = PlayerColorType.Blue;
        [Tooltip("간판 배경(모양 없는 판).")]
        public Renderer signBackground;
        [Tooltip("모양 판(★/얼굴/음표/꿀떡). 카운트다운 동안 숨김.")]
        public Renderer signLabel;
        [Tooltip("카운트다운 숫자(TextMeshPro 3D). 카운트다운 동안만 보임.")]
        public TMP_Text countdownText;
        [Tooltip("하트를 담는 패널.")]
        public Renderer heartPanel;
        [Tooltip("하트가 가로로 나열될 기준점(패널 중앙, 패널 앞면 쪽).")]
        public Transform heartRow;
        [Tooltip("남은 시간 채움 — 간판 글씨 뒤 배경.")]
        public Renderer[] timerFills;

        [System.NonSerialized] public List<SpriteRenderer> hearts = new List<SpriteRenderer>();
    }

    [SerializeField] SideSplitChallenge challenge;
    [SerializeField] DirectionView[] views;

    [Header("하트")]
    [SerializeField] Sprite heartAny;
    [SerializeField] Sprite heartBlue;
    [SerializeField] Sprite heartPurple;
    [SerializeField] Sprite heartGreen;
    [SerializeField] Sprite heartYellow;
    [Tooltip("빈 하트(아무나) 색. 흰 스프라이트에 곱해진다.")]
    [SerializeField] Color anyHeartColor = new Color(0.72f, 1f, 0.93f, 1f);
    [SerializeField] float heartSize = 1.9f;
    [SerializeField] float heartGap = 0.5f;

    [Header("남은 시간 채움")]
    [SerializeField] Color fillColor = new Color(0.25f, 0.72f, 0.62f, 1f);
    [SerializeField] Color fillWarnColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    [SerializeField] Color fillSuccessColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    [SerializeField] Color fillFailColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    [Tooltip("남은 시간이 이 값 이하면 경고색.")]
    [SerializeField] float warnSeconds = 1f;

    [Header("SFX")]
    [Tooltip("3·2·1 숫자가 바뀔 때마다 1회.")]
    [SerializeField] SFXId countdownSfx = SFXId.Minigame_CountdownTick;
    [Tooltip("채움이 줄어드는 동안 루프(읽는 시간엔 안 울림).")]
    [SerializeField] SFXId timerSfx = SFXId.Minigame_TimerTick;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int CutoffId    = Shader.PropertyToID("_Cutoff");

    MaterialPropertyBlock _mpb;
    AudioSource _timerLoop;
    float _roundTotal;
    int _lastCountdownDigit;

    // 이 머신의 배정색 — 하트 패널을 누구에게 보일지 정하는 데만 쓴다(§1.9.2).
    bool _hasMyColor;
    PlayerColorType _myColor;

    void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (challenge == null) return;

        challenge.OnCountdownTick.AddListener(HandleCountdownTick);
        challenge.OnRoundReady.AddListener(HandleRoundReady);
        challenge.OnTimerTick.AddListener(HandleTimerTick);
        challenge.OnRoundSuccess.AddListener(HandleSuccess);
        challenge.OnRoundFailed.AddListener(HandleFail);
        challenge.OnAllCleared.AddListener(HandleAllCleared);
    }

    void Start() => ResetIdle();

    void OnDestroy()
    {
        StopTimerLoop();
        if (challenge == null) return;

        challenge.OnCountdownTick.RemoveListener(HandleCountdownTick);
        challenge.OnRoundReady.RemoveListener(HandleRoundReady);
        challenge.OnTimerTick.RemoveListener(HandleTimerTick);
        challenge.OnRoundSuccess.RemoveListener(HandleSuccess);
        challenge.OnRoundFailed.RemoveListener(HandleFail);
        challenge.OnAllCleared.RemoveListener(HandleAllCleared);
    }

    void OnDisable() => StopTimerLoop();

    // ── 이벤트 ───────────────────────────────────────────────────

    void HandleCountdownTick(float left)
    {
        int digit = Mathf.CeilToInt(left);
        if (digit == _lastCountdownDigit) return;

        bool entering = _lastCountdownDigit == 0;
        _lastCountdownDigit = digit;

        if (entering)
        {
            StopTimerLoop();
            foreach (var v in views)
            {
                SetHearts(v, 0, null);
                SetVisible(v.signLabel, false);
                SetVisible(v.heartPanel, false); // 빈 패널을 미리 띄우지 않는다 — 공개 때 내 것만 나타난다
                SetFills(v, false, 1f, fillColor);
            }
        }

        foreach (var v in views)
        {
            if (v.countdownText == null) continue;
            v.countdownText.gameObject.SetActive(true);
            v.countdownText.text = digit.ToString();
        }

        if (SFXManager.Instance != null) SFXManager.Instance.Play(countdownSfx);
    }

    void HandleRoundReady(SideSplitRoundInfo info)
    {
        _lastCountdownDigit = 0;
        _roundTotal = challenge.CurrentRoundTimeLimit;
        RefreshMyColor();

        foreach (var v in views)
        {
            bool mine   = IsPanelMine(v);
            int count   = GetCount(info, v.direction);
            bool colored = info.hasColorRequirement && info.colorDirection == v.direction;

            SetCountdownVisible(v, false);
            SetVisible(v.signLabel, true);
            SetVisible(v.heartPanel, mine);
            // 남의 담당 자리는 하트를 0개로 — 인원을 "0명"으로 보여주는 게 아니라 아예 안 보여주는 것이다.
            // 패널까지 꺼지므로 "비었다"와 "모른다"가 화면에서 구분된다.
            SetHearts(v, mine ? count : 0, colored ? GetHeartSprite(info.requiredColor) : null);
            SetFills(v, true, 1f, fillColor);
        }
        // 틱 루프는 읽는 시간(revealReadHold)이 끝나 채움이 줄기 시작할 때 HandleTimerTick에서 시작.
    }

    void HandleTimerTick(float remaining)
    {
        if (_roundTotal <= 0f) return;

        if (_timerLoop == null && remaining > 0f && remaining < _roundTotal)
            StartTimerLoop();

        float frac = Mathf.Clamp01(remaining / _roundTotal);
        Color c = remaining <= warnSeconds ? fillWarnColor : fillColor;

        foreach (var v in views)
            SetFills(v, true, frac, c);

        if (remaining <= 0f) StopTimerLoop();
    }

    void HandleSuccess() => ShowResult(fillSuccessColor);
    void HandleFail()    => ShowResult(fillFailColor);

    void HandleAllCleared()
    {
        StopTimerLoop();
        ResetIdle();
    }

    // ── 표시 ─────────────────────────────────────────────────────

    void ShowResult(Color c)
    {
        StopTimerLoop();
        _roundTotal = 0f;
        foreach (var v in views)
            SetFills(v, true, 1f, c);
    }

    void ResetIdle()
    {
        _roundTotal = 0f;
        _lastCountdownDigit = 0;
        if (views == null) return;

        foreach (var v in views)
        {
            SetHearts(v, 0, null);
            SetCountdownVisible(v, false);
            SetVisible(v.signLabel, true);
            SetVisible(v.heartPanel, false);
            SetFills(v, false, 1f, fillColor);
        }
    }

    // ── 정보 비대칭 (§1.9.2) ─────────────────────────────────────

    /// <summary>
    /// 이 간판의 하트 패널이 이 머신에 보이는가.
    ///
    ///  · 주인 없는 모양(담당 색이 이번 판에 없음) → <b>전원 공개</b>. SessionColorSlotMap의
    ///    "빠진 색 슬롯 → Common(누구나)" 규칙(2026-09-18 확정)을 그대로 쓴다. 그래서 인원별
    ///    분기가 없고 솔로 특례도 필요 없다 — 1인은 담당 1 + 주인 없는 3이라 저절로 전부 보인다.
    ///  · 담당 색이 내 색 → 나에게만 공개.
    ///
    /// 시드를 쓰지 않는 결정적 계산이다. 랜덤 배정은 시드가 필요하고 시드는 사망 리로드 때
    /// RPC로 와서 "색이 정해지지 않은 창"이 생긴다 — SessionColorSlotMap이 구 재배정 규칙을
    /// 폐기한 이유와 같아서 되풀이하지 않는다.
    /// </summary>
    bool IsPanelMine(DirectionView v)
    {
        if (SessionColorSlotMap.Resolve(v.ownerColor) == PlayerColorType.Common) return true;
        return _hasMyColor && v.ownerColor == _myColor;
    }

    /// <summary>clientId→색 SSOT(PlayerSpawnCoordinator)에서 이 머신의 배정색을 조회 — 라운드마다 다시 당긴다.
    /// 못 얻으면 담당 없음으로 떨어져 주인 없는 모양만 보인다(표시가 멈추거나 예외가 나지 않는다).</summary>
    void RefreshMyColor()
    {
        var nm = NetworkManager.Singleton;
        _hasMyColor = nm != null && nm.IsListening
                      && PlayerSpawnCoordinator.TryGetColor(nm.LocalClientId, out _myColor);
    }

    void SetHearts(DirectionView v, int count, Sprite requiredSprite)
    {
        if (v.heartRow == null) return;

        while (v.hearts.Count < count)
        {
            var go = new GameObject("Heart");
            go.transform.SetParent(v.heartRow, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 1; // 반투명 패널보다 항상 앞
            v.hearts.Add(sr);
        }

        float total = count * heartSize + Mathf.Max(0, count - 1) * heartGap;
        for (int i = 0; i < v.hearts.Count; i++)
        {
            var sr = v.hearts[i];
            bool on = i < count;
            sr.gameObject.SetActive(on);
            if (!on) continue;

            bool isRequired = i == 0 && requiredSprite != null;
            sr.sprite = isRequired ? requiredSprite : heartAny;
            sr.color  = isRequired ? Color.white : anyHeartColor;

            Vector2 spriteSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            float s = spriteSize.x > 0f ? heartSize / spriteSize.x : 1f;
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.transform.localPosition = new Vector3(-total * 0.5f + heartSize * 0.5f + i * (heartSize + heartGap), 0f, 0f);
            sr.transform.localRotation = Quaternion.identity;
        }
    }

    void SetFills(DirectionView v, bool visible, float fraction, Color color)
    {
        if (v.timerFills == null) return;
        // 채움 텍스처 알파는 위치 그라데이션(한쪽 끝=1 → 반대쪽 끝≈0). cutoff를 올리면 반대쪽부터 잘려 줄어든다.
        // 알파 0인 바깥 영역은 cutoff가 0보다 커야 잘리므로 하한을 둔다.
        float cutoff = Mathf.Clamp(1f - fraction, 0.002f, 1f);
        foreach (var r in v.timerFills)
        {
            if (r == null) continue;
            r.enabled = visible && fraction > 0f;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetFloat(CutoffId, cutoff);
            r.SetPropertyBlock(_mpb);
        }
    }

    static void SetCountdownVisible(DirectionView v, bool visible)
    {
        if (v.countdownText != null) v.countdownText.gameObject.SetActive(visible);
    }

    static void SetVisible(Renderer r, bool visible)
    {
        if (r != null) r.enabled = visible;
    }

    // ── SFX ──────────────────────────────────────────────────────

    void StartTimerLoop()
    {
        StopTimerLoop();
        if (SFXManager.Instance != null)
            _timerLoop = SFXManager.Instance.PlayLoop(timerSfx);
    }

    void StopTimerLoop()
    {
        if (_timerLoop == null) return;
        if (SFXManager.Instance != null) SFXManager.Instance.StopLoop(_timerLoop);
        else Destroy(_timerLoop.gameObject);
        _timerLoop = null;
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    static int GetCount(SideSplitRoundInfo info, SideSplitDirection dir) => dir switch
    {
        SideSplitDirection.Left  => info.leftCount,
        SideSplitDirection.Right => info.rightCount,
        SideSplitDirection.Front => info.frontCount,
        SideSplitDirection.Back  => info.backCount,
        _                        => 0,
    };

    Sprite GetHeartSprite(PlayerColorType color) => color switch
    {
        PlayerColorType.Blue   => heartBlue,
        PlayerColorType.Purple => heartPurple,
        PlayerColorType.Green  => heartGreen,
        PlayerColorType.Yellow => heartYellow,
        _                      => heartAny,
    };
}
