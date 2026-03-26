using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 바닥에서 boulder가 솟아오른 뒤 SpinRoller를 활성화하는 스포너.
///
/// [동작 순서]
///  1. Spawn() 호출
///  2. boulderRoot 스케일 0 → 1로 riseTime 초 동안 커짐
///  3. SpinRoller.Activate() 호출 → boulder가 웨이포인트 경로를 따라 굴러감
///
/// [설정 방법]
///  1. boulder GameObject에 이 스크립트 + SpinRoller 부착
///  2. SpinRoller.autoStart = false, waypoints 배열 설정
///  3. boulderRoot에 시각적으로 스케일 애니메이션 받을 Transform 연결
///     (비워두면 이 GameObject 자체가 스케일 대상)
///  4. PlayerTriggerZone.OnPlayerEnter → BoulderSpawner.Spawn() 연결
/// </summary>
public class BoulderSpawner : MonoBehaviour
{
    [Header("Boulder")]
    [Tooltip("스케일 애니메이션을 적용할 Transform. 비우면 이 GameObject 사용")]
    [SerializeField] Transform boulderRoot = null;

    [Tooltip("SpinRoller 컴포넌트. 비우면 이 GameObject에서 자동 탐색")]
    [SerializeField] SpinRoller roller = null;

    [Header("솟아오르기 애니메이션")]
    [Tooltip("스케일 0→1 도달까지 걸리는 시간(초). 0이면 즉시 소환")]
    [SerializeField] float riseTime = 0f;

    [Tooltip("Spawn() 호출 후 솟아오르기 시작까지 대기 시간(초)")]
    [SerializeField] float riseDelay = 0f;

    [Header("파티클 (선택)")]
    [Tooltip("솟아오를 때 재생할 바닥 파티클. 비워두면 생략")]
    [SerializeField] ParticleSystem groundParticle = null;

    [Header("이벤트")]
    public UnityEvent OnSpawnStarted;
    public UnityEvent OnRollingStarted;

    bool _isSpawning;

    void Awake()
    {
        if (roller == null)
            roller = GetComponent<SpinRoller>() ?? GetComponentInChildren<SpinRoller>();

        if (boulderRoot == null)
            boulderRoot = transform;

        // 시작 시 스케일 0으로 숨김
        boulderRoot.localScale = Vector3.zero;
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>boulder 소환 시작. 이미 실행 중이면 무시.</summary>
    public void Spawn()
    {
        if (_isSpawning) return;
        StartCoroutine(SpawnRoutine());
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator SpawnRoutine()
    {
        _isSpawning = true;
        OnSpawnStarted?.Invoke();

        if (riseDelay > 0f)
            yield return new WaitForSeconds(riseDelay);

        if (groundParticle != null)
            groundParticle.Play();

        // 스케일 0 → 1
        if (riseTime > 0f)
        {
            float elapsed = 0f;
            while (elapsed < riseTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / riseTime));
                boulderRoot.localScale = Vector3.one * t;
                yield return null;
            }
        }

        boulderRoot.localScale = Vector3.one;

        if (groundParticle != null)
            groundParticle.Stop();

        // SpinRoller 활성화 → 굴러가기 시작
        if (roller != null)
            roller.Activate();

        OnRollingStarted?.Invoke();
        _isSpawning = false;
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: Spawn")]
    void Debug_Spawn() => Spawn();

    [ContextMenu("테스트: 리셋 (스케일 0)")]
    void Debug_Reset()
    {
        if (boulderRoot != null)
            boulderRoot.localScale = Vector3.zero;
        _isSpawning = false;
    }
}
