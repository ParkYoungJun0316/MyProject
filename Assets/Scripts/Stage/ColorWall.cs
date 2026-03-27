using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 벽 컴포넌트.
/// 플레이어 현재 색(흑/백/고유색)과 벽 색상을 비교해 반응을 다르게 함.
///
/// [색상 일치]
///  WallMover → ResetToStart() 후 pauseDuration 뒤 Activate() (밀려남 + 잠시 멈춤)
///  WallWaveController → Stop() 후 pauseDuration 뒤 Play()
///
/// [색상 불일치]
///  플레이어에게 damage 적용 (damageInterval마다)
///
/// [설정]
///  wallMover 또는 waveController 중 하나를 연결.
///  Collider Is Trigger 여부에 따라 OnTrigger / OnCollision 모두 처리.
/// </summary>
public class ColorWall : MonoBehaviour
{
    public enum WallColorType { Black, White, Blue, Red, Green, Yellow }

    [Header("벽 색상")]
    [Tooltip("이 벽의 색상. 플레이어 현재 색과 비교.")]
    [SerializeField] WallColorType wallColor = WallColorType.Black;

    [Header("색상 일치 — 멈춤")]
    [Tooltip("색상이 같으면 이 시간(초) 동안 벽 이동 정지")]
    [SerializeField] float pauseDuration = 2f;

    [Header("색상 불일치 — 데미지")]
    [Tooltip("불일치 시 플레이어에게 입히는 데미지")]
    [SerializeField] int damage = 1;

    [Tooltip("연속 데미지 간격(초)")]
    [SerializeField] float damageInterval = 0.5f;

    [Header("연결 컴포넌트 (둘 중 하나 연결)")]
    [Tooltip("개별 벽 이동 컴포넌트")]
    [SerializeField] WallMover wallMover;

    [Tooltip("파형 벽 이동 컴포넌트")]
    [SerializeField] WallWaveController waveController;

    [Header("이벤트 (선택)")]
    [Tooltip("색상 일치 시 호출 (시각 피드백 등)")]
    public UnityEvent OnColorMatch;

    [Tooltip("색상 불일치 데미지 발생 시 호출")]
    public UnityEvent OnColorMismatch;

    float _nextDamageTime;
    bool  _isPaused;

    // ── 충돌 감지 (Trigger / Collider 모두 처리) ─────────────────

    void OnTriggerEnter(Collider other)  => HandleContact(other);
    void OnTriggerStay(Collider other)   => HandleContact(other);
    void OnCollisionEnter(Collision col) => HandleContact(col.collider);
    void OnCollisionStay(Collision col)  => HandleContact(col.collider);

    // ── 내부 ────────────────────────────────────────────────────

    void HandleContact(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;

        if (IsColorMatch(p))
        {
            if (!_isPaused)
            {
                StopAllCoroutines();
                StartCoroutine(PauseRoutine());
                OnColorMatch?.Invoke();
            }
        }
        else
        {
            if (damage > 0 && Time.time >= _nextDamageTime)
            {
                p.TakeDamage(damage, false);
                _nextDamageTime = Time.time + Mathf.Max(damageInterval, 0.05f);
                OnColorMismatch?.Invoke();
            }
        }
    }

    bool IsColorMatch(Player p)
    {
        switch (wallColor)
        {
            case WallColorType.Black:
                return !p.isUniqueColor && p.isBlack;
            case WallColorType.White:
                return !p.isUniqueColor && !p.isBlack;
            case WallColorType.Blue:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Blue;
            case WallColorType.Red:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Red;
            case WallColorType.Green:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Green;
            case WallColorType.Yellow:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Yellow;
            default:
                return false;
        }
    }

    IEnumerator PauseRoutine()
    {
        _isPaused = true;

        // 벽 정지 (WallMover는 시작 위치로 후퇴, WaveController는 정지)
        wallMover?.ResetToStart();
        waveController?.Stop();

        yield return new WaitForSeconds(pauseDuration);

        // 재개
        wallMover?.Activate();
        waveController?.Play();

        _isPaused = false;
    }

    // ── 에디터 ──────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        Color c = wallColor switch
        {
            WallColorType.Black  => Color.black,
            WallColorType.White  => Color.white,
            WallColorType.Blue   => Color.blue,
            WallColorType.Red    => Color.red,
            WallColorType.Green  => Color.green,
            WallColorType.Yellow => Color.yellow,
            _                    => Color.gray
        };
        c.a = 0.4f;
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}
