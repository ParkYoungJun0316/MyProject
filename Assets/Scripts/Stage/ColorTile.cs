using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 타일 — ColorTileChallenge에서 생성/관리됨.
///
/// [동작]
///  requiredColorType에 맞는 플레이어가 올라서면 IsCompleted = true.
///  현재 흑/백/고유색 모드와 무관하게 playerColorType으로만 판별.
///  틀린 색 플레이어가 올라서도 완료되지 않음.
///
/// [테스트 모드]
///  ignorePlayerCheck = true 시 플레이어 색/isUniqueColor 체크 없이 누구든 밟으면 활성화.
///  DirectionalBarrierRound에서 디버그용으로 사용.
///
/// [설정]
///  Collider(Is Trigger = true) 필수.
///  ColorTileChallenge.Activate() 호출 시 자동 생성되므로 직접 씬에 배치 불필요.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ColorTile : MonoBehaviour
{
    [Header("타일 색상")]
    [Tooltip("이 타일이 요구하는 플레이어 고유색")]
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

    bool _isCompleted;

    /// <summary>요구 색상의 플레이어가 현재 타일 위에 있으면 true.</summary>
    public bool IsCompleted => _isCompleted;

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>ColorTileChallenge에서 색상 설정 시 호출.</summary>
    public void Setup(PlayerColorType colorType)
    {
        requiredColorType = colorType;
    }

    // ── 충돌 감지 ────────────────────────────────────────────────

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other) => CheckPlayer(other);
    void OnTriggerStay(Collider other)  => CheckPlayer(other);

    void CheckPlayer(Collider other)
    {
        if (ignorePlayerCheck)
        {
            if (_isCompleted) return;
            _isCompleted = true;
            PlayPressSfx();
            OnCompleted?.Invoke();
            OnActivatedCallback?.Invoke(requiredColorType);
            return;
        }

        Player p = other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;
        if (p.playerColorType != requiredColorType) return;

        // 고유색 모드로 전환된 순간 완료
        if (p.isUniqueColor && !_isCompleted)
        {
            _isCompleted = true;
            PlayPressSfx();
            OnCompleted?.Invoke();
            OnActivatedCallback?.Invoke(requiredColorType);
        }
        // 고유색 → 흑/백으로 전환된 순간 취소
        else if (!p.isUniqueColor && _isCompleted)
        {
            _isCompleted = false;
            OnUncompleted?.Invoke();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!_isCompleted) return;

        if (ignorePlayerCheck)
        {
            _isCompleted = false;
            return;
        }

        Player p = other.GetComponentInParent<Player>();
        if (p == null) return;
        if (p.playerColorType != requiredColorType) return;

        _isCompleted = false;
        OnUncompleted?.Invoke();
    }

    // 로컬 3D 재생 — 타일 트리거는 각 머신이 CNT 위치로 이미 점유를 안다(RPC 불필요).
    // 2D였을 때는 원격 플레이어가 밟은 타일도 내 귀 옆에서 나는 것처럼 풀볼륨으로 들렸음
    // (2026-09-01 수정) — 타일 위치 기준 3D로 전환.
    void PlayPressSfx()
    {
        if (pressSfxId == SFXId.None) return;
        SFXManager.Instance?.PlayAtPoint(pressSfxId, transform.position, pressMinDistance, pressMaxDistance, pressRolloffMode);
    }
}
