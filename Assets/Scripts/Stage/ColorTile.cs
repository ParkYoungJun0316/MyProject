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

    [Header("이벤트 (선택)")]
    [Tooltip("올바른 플레이어가 올라섰을 때 (시각 피드백 등)")]
    public UnityEvent OnCompleted;

    [Tooltip("올바른 플레이어가 내려갔을 때")]
    public UnityEvent OnUncompleted;

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
        Player p = other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;
        if (p.playerColorType != requiredColorType) return;

        // 고유색 모드로 전환된 순간 완료
        if (p.isUniqueColor && !_isCompleted)
        {
            _isCompleted = true;
            OnCompleted?.Invoke();
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

        Player p = other.GetComponentInParent<Player>();
        if (p == null) return;
        if (p.playerColorType != requiredColorType) return;

        _isCompleted = false;
        OnUncompleted?.Invoke();
    }
}
