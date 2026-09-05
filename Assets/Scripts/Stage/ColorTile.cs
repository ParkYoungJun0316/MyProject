using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 타일 — ColorTileChallenge에서 생성/관리됨.
///
/// 구 클리어(Setup): requiredColorType 플레이어가 올라서면 IsCompleted.
///  - 고유색(Blue/Purple/Green/Yellow): 그 색 신원(playerColorType) 플레이어만, isUniqueColor=true일 때.
///  - 공용(Black/White, DirectionalBarrier): 신원 무관 — Player.isBlack 토글이 타일 색과 일치하면
///    누구든(ColorWall.IsColorMatch와 동일 판정).
/// 점수제(SetupQuota): occupySeconds 동안 유효 점유 → HoldReady. 발 떼면 리셋.
///  - 공용(Black/White)은 토글 무관 완전히 "아무나"(IsValidQuotaOccupant) — 구 클리어와 다른 규칙.
/// DirectionalBarrier 디버그: ignorePlayerCheck 시 누구든 즉시 완료.
/// Collider(Is Trigger) 필수.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ColorTile : MonoBehaviour
{
    [Header("타일 색상")]
    [Tooltip("고유색(Blue/Purple/Green/Yellow, 신원 일치) 또는 공용 흑백(Black/White). 구 클리어 모드의 흑백은 Player.isBlack 토글 일치가 필요, 점수제는 완전히 아무나. Common/Danger는 쓰지 않음.")]
    [SerializeField] PlayerColorType requiredColorType = PlayerColorType.Blue;

    [Header("테스트")]
    [Tooltip("true: 플레이어 색/isUniqueColor 체크 없이 누구든 밟으면 활성화")]
    public bool ignorePlayerCheck = false;

    [Header("사운드 (3D)")]
    [Tooltip("고유색으로 정확히 인식됐을 때 재생. None이면 무음. 발을 뗄 때(OnUncompleted)는 재생하지 않음.")]
    [SerializeField] SFXId pressSfxId = SFXId.Pad_Press;
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] float pressMinDistance = 5f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] float pressMaxDistance = 20f;
    [SerializeField] AudioRolloffMode pressRolloffMode = AudioRolloffMode.Logarithmic;

    [Header("이벤트 (선택)")]
    [Tooltip("올바른 플레이어가 올라섰을 때 (시각 피드백 등)")]
    public UnityEvent OnCompleted;

    [Tooltip("올바른 플레이어가 내려갔을 때")]
    public UnityEvent OnUncompleted;

    /// <summary>타일이 활성화됐을 때 색상을 넘겨주는 콜백. DirectionalBarrierRound 등에서 연결.</summary>
    public Action<PlayerColorType> OnActivatedCallback;

    public PlayerColorType RequiredColorType => requiredColorType;

    readonly HashSet<Player> _occupants = new HashSet<Player>();

    bool _quotaMode;
    float _occupySeconds = 2f;
    float _heldTime;
    bool _isCompleted;

    /// <summary>구 클리어: 요구 색 플레이어가 위에 있으면 true. 점수제: 쓰지 않음(HoldReady).</summary>
    public bool IsCompleted => _isCompleted;

    /// <summary>점수제: occupySeconds 동안 유효 점유가 유지되면 true.</summary>
    public bool HoldReady => _quotaMode && _occupants.Count > 0 && _heldTime >= _occupySeconds;

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>DirectionalBarrier 등에서 색상 설정. 점수제는 SetupQuota.</summary>
    public void Setup(PlayerColorType colorType)
    {
        requiredColorType = colorType;
        _quotaMode = false;
        _heldTime = 0f;
        _occupants.Clear();
        _isCompleted = false;
    }

    /// <summary>점수제 타일. 고유색은 isUniqueColor+색 일치. Black/White는 아무 생존 플레이어나.</summary>
    public void SetupQuota(PlayerColorType colorType, float occupySeconds)
    {
        requiredColorType = colorType;
        _occupySeconds = Mathf.Max(0.01f, occupySeconds);
        _quotaMode = true;
        _heldTime = 0f;
        _occupants.Clear();
        _isCompleted = false;
    }

    /// <summary>
    /// 점수 직후 리셋 — 다음 점수까지 occupySeconds를 처음부터 다시 채워야 한다.
    /// 점유 집계도 비운다: 이 호출은 타일이 다른 칸으로 순간이동하는 시점이라 지금 등록된 점유자는
    /// 이미 그 위에 없는데, 물리 OnTriggerExit은 다음 스텝에야 오므로 비우지 않으면 그 한 프레임
    /// 동안 없는 사람의 점유가 계속 누적된다. 비우면서 OnUncompleted를 직접 발동시키는 이유는,
    /// 뒤늦게 오는 그 Exit이 이제 Remove에 실패해서(이미 비었으므로) 시각 연출이 눌린 채로
    /// 남아버리기 때문이다.
    /// </summary>
    public void ResetHold()
    {
        _heldTime = 0f;
        if (_occupants.Count == 0) return;

        _occupants.Clear();
        OnUncompleted?.Invoke();
    }

    public void PlayScoreSfx() => PlayPressSfx();

    // ── 점유 ────────────────────────────────────────────────────

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Update()
    {
        if (!_quotaMode) return;

        if (_occupants.Count > 0)
        {
            _occupants.RemoveWhere(p => p == null || p.IsDead);
            if (_occupants.Count > 0)
                _heldTime += Time.deltaTime;
            else
                _heldTime = 0f;
        }
        else
            _heldTime = 0f;
    }

    void OnTriggerEnter(Collider other) => TryAddOccupant(other);
    void OnTriggerStay(Collider other)  => TryAddOccupant(other);

    void OnTriggerExit(Collider other)
    {
        // GetComponent (GetComponentInParent 아님) — 아래 TryAddOccupant 주석 참고.
        Player p = other.GetComponent<Player>();
        if (p == null) return;

        if (_quotaMode)
        {
            if (_occupants.Remove(p) && _occupants.Count == 0)
                OnUncompleted?.Invoke();
            return;
        }

        if (!_isCompleted) return;

        if (ignorePlayerCheck)
        {
            _isCompleted = false;
            return;
        }

        // 공용(흑/백) 타일은 신원(playerColorType) 필터가 없다 — 누구든 밟을 수 있으므로 나갈 때도
        // 신원으로 거르지 않고 무조건 해제. 고유 타일은 기존처럼 그 색 담당 플레이어가 나갈 때만 해제.
        if (!PlayerColorUtil.IsSharedTileColor(requiredColorType) && p.playerColorType != requiredColorType) return;
        _isCompleted = false;
        OnUncompleted?.Invoke();
    }

    void TryAddOccupant(Collider other)
    {
        // [버그 수정 2026-09-05] Player 콜라이더는 하나가 아니다 — 몸통 CapsuleCollider(루트,
        // isTrigger=false)와 PlayerPunchHitbox의 SphereCollider(자식, isTrigger=true, 항상 켜짐,
        // PlayerPunchHitbox.cs)가 같은 플레이어에 동시에 존재한다. GetComponentInParent<Player>()로
        // 찾으면 둘 다 "같은 Player"로 잡히는데, 두 콜라이더가 타일 경계를 서로 다른 순간에
        // 드나들어(예: 몸통이 먼저 빠져나가며 _isCompleted=false → 아직 안 빠진 펀치 히트박스가
        // 곧이어 OnTriggerStay로 다시 걸리며 _isCompleted=true) 벗어나는 순간에도 PlayPressSfx()가
        // 다시 불렸다. GetComponent(부모 탐색 없음)로 바꾸면 Player와 같은 GameObject에 있는 몸통
        // 콜라이더만 통과하고, 자식인 펀치 히트박스는 걸러진다 — PressurePad.OnTriggerEnter가
        // 원래부터 GetComponent를 썼던 이유(발판은 이 버그가 없었음)와 동일한 원칙으로 통일.
        Player p = other.GetComponent<Player>();
        if (p == null) return;

        if (ignorePlayerCheck)
        {
            if (p.IsDead) return;
            if (_isCompleted) return;
            _isCompleted = true;
            PlayPressSfx();
            OnCompleted?.Invoke();
            OnActivatedCallback?.Invoke(requiredColorType);
            return;
        }

        if (p.IsDead) return;

        if (_quotaMode)
        {
            if (!IsValidQuotaOccupant(p)) return;
            if (_occupants.Add(p) && _occupants.Count == 1)
                OnCompleted?.Invoke();
            return;
        }

        // 고유 타일(Blue/Purple/Green/Yellow): 그 색 신원(playerColorType) 플레이어만 관여 — 이 게임엔
        // 같은 고유색이 둘일 수 없으므로 신원 필터만으로 "그 사람"이 특정된다.
        // 공용 타일(Black/White): 신원과 무관 — 지금 흑/백 토글(Player.isBlack, isUniqueColor=false
        // 상태)이 타일 색과 맞는 "누구든" 밟을 수 있다(ColorWall.IsBlackOnly/WhiteOnly와 동일 판정).
        // [버그 수정 2026-09-05] 예전엔 흑/백도 playerColorType으로 비교해서 항상 불일치 —
        // 플레이어 고유색은 절대 Black/White가 될 수 없으니 흑/백 타일이 영원히 안 올라왔다.
        bool isSharedColor = PlayerColorUtil.IsSharedTileColor(requiredColorType);

        if (!isSharedColor && p.playerColorType != requiredColorType) return;

        bool colorMatches = isSharedColor
            ? !p.isUniqueColor && (p.isBlack == (requiredColorType == PlayerColorType.Black))
            : p.isUniqueColor;

        if (colorMatches && !_isCompleted)
        {
            _isCompleted = true;
            PlayPressSfx();
            OnCompleted?.Invoke();
            OnActivatedCallback?.Invoke(requiredColorType);
        }
        else if (!colorMatches && _isCompleted)
        {
            _isCompleted = false;
            OnUncompleted?.Invoke();
        }
    }

    bool IsValidQuotaOccupant(Player p)
    {
        if (p == null || p.IsDead) return false;
        if (PlayerColorUtil.IsSharedTileColor(requiredColorType))
            return true;
        return p.isUniqueColor && p.playerColorType == requiredColorType;
    }

    void PlayPressSfx()
    {
        if (pressSfxId == SFXId.None) return;
        SFXManager.Instance?.PlayAtPoint(pressSfxId, transform.position, pressMinDistance, pressMaxDistance, pressRolloffMode);
    }
}
