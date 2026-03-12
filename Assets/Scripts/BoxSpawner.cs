using System.Collections;
using UnityEngine;

/// <summary>
/// 바닥에서 박스를 솟아오르게 스폰하는 컴포넌트.
///
/// [동작 순서]
///  1. Spawn() 호출
///  2. BulgeMesh가 0→1로 1초간 커짐 (플레이어 밀침 포함)
///  3. 박스 생성 + 위로 튕겨냄
///  4. BulgeMesh가 1→0으로 서서히 사라짐
///
/// [설정]
///  - bulgeMesh     : 자식 오브젝트 BulgeMesh Transform
///  - boxPrefab     : 스폰할 박스 프리팹 (PushableBox 포함)
///  - spawnPoint    : 박스 스폰 위치 (없으면 이 오브젝트 위치)
///  - groundParticle: 바닥 파티클 (추후 연결)
/// </summary>
public class BoxSpawner : MonoBehaviour
{
    [Header("BulgeMesh")]
    [Tooltip("자식 오브젝트 BulgeMesh의 Transform")]
    public Transform bulgeMesh;

    [Tooltip("0→1 팽창 시간(초)")]
    public float bulgeRiseTime = 1f;

    [Tooltip("1→0 수축 시간(초)")]
    public float bulgeFallTime = 0.6f;

    [Header("박스 스폰")]
    [Tooltip("스폰할 박스 색. boxPrefab이 비어있으면 이 값으로 프리팹 로드")]
    public PushableBox.BoxOwnerColor spawnColor = PushableBox.BoxOwnerColor.Blue;

    [Tooltip("스폰할 박스 프리팹. 비우면 spawnColor 기반으로 자동 로드")]
    public GameObject boxPrefab;

    [Tooltip("박스 스폰 기준 위치. 비우면 이 오브젝트 위치 사용")]
    public Transform spawnPoint;

    [Tooltip("박스에 가할 위쪽 임펄스 크기")]
    public float launchImpulse = 6f;

    [Header("플레이어 밀침")]
    [Tooltip("팽창 중 플레이어를 감지할 반경")]
    public float pushRadius = 1.2f;

    [Tooltip("플레이어에게 가할 수평 임펄스 크기")]
    public float pushForce = 8f;

    [Tooltip("감지할 레이어 (Player 레이어 설정)")]
    public LayerMask playerLayer;

    [Header("파티클 (추후 연결)")]
    [Tooltip("바닥 파티클 시스템. 비워두면 생략")]
    public ParticleSystem groundParticle;

    bool _isSpawning;

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>박스 스폰 시퀀스 시작. 이미 실행 중이면 무시.</summary>
    public void Spawn()
    {
        if (_isSpawning) return;
        StartCoroutine(SpawnRoutine());
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator SpawnRoutine()
    {
        _isSpawning = true;

        // BulgeMesh 초기 크기 0으로
        if (bulgeMesh != null) bulgeMesh.localScale = Vector3.zero;

        // 파티클 시작
        if (groundParticle != null) groundParticle.Play();

        // ① 팽창: 0 → 1, 이 동안 플레이어 밀침
        yield return StartCoroutine(BulgeRoutine(0f, 1f, bulgeRiseTime, pushPlayers: true));

        // ② 박스 스폰 + 위로 튕겨냄
        SpawnBox();

        // ③ 수축: 1 → 0
        yield return StartCoroutine(BulgeRoutine(1f, 0f, bulgeFallTime, pushPlayers: false));

        // 파티클 정지 (루프형 파티클의 경우)
        if (groundParticle != null) groundParticle.Stop();

        if (bulgeMesh != null) bulgeMesh.localScale = Vector3.zero;

        _isSpawning = false;
    }

    IEnumerator BulgeRoutine(float from, float to, float duration, bool pushPlayers)
    {
        if (bulgeMesh == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float scale = Mathf.Lerp(from, to, t);
            bulgeMesh.localScale = new Vector3(scale, scale, scale);

            // 팽창 중에만 플레이어 밀침
            if (pushPlayers)
                PushNearbyPlayers();

            yield return null;
        }

        bulgeMesh.localScale = new Vector3(to, to, to);
    }

    void SpawnBox()
    {
        var prefab = ResolveBoxPrefab();
        if (prefab == null) return;

        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        GameObject instance = Instantiate(prefab, pos, Quaternion.identity);

        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(Vector3.up * launchImpulse, ForceMode.Impulse);
    }

    GameObject ResolveBoxPrefab()
    {
        if (boxPrefab != null) return boxPrefab;

        var name = spawnColor switch
        {
            PushableBox.BoxOwnerColor.Common => "PushableBox.C",
            PushableBox.BoxOwnerColor.Blue   => "PushableBox.B",
            PushableBox.BoxOwnerColor.Red   => "PushableBox.R",
            PushableBox.BoxOwnerColor.Green => "PushableBox.G",
            PushableBox.BoxOwnerColor.Yellow => "PushableBox.Y",
            _ => "PushableBox.C"
        };

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefab/{name}.prefab");
#else
        return Resources.Load<GameObject>(name);
#endif
    }

    void PushNearbyPlayers()
    {
        // playerLayer = 0 이면 모든 레이어에서 Player 컴포넌트로 판별
        Vector3 center = spawnPoint != null ? spawnPoint.position : transform.position;

        Collider[] hits = playerLayer.value != 0
            ? Physics.OverlapSphere(center, pushRadius, playerLayer)
            : Physics.OverlapSphere(center, pushRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            Player player = hits[i].GetComponentInParent<Player>();
            if (player == null || player.IsDead) continue;

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb == null || rb.isKinematic) continue;

            // 중심에서 바깥쪽 수평 방향으로 밀침
            Vector3 dir = hits[i].transform.position - center;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Random.insideUnitSphere; // 정중앙이면 랜덤 방향
            dir.y = 0f;
            dir.Normalize();

            rb.AddForce(dir * pushForce, ForceMode.Impulse);
        }
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: Spawn")]
    void Debug_Spawn() => Spawn();

    void OnDrawGizmos()
    {
        Vector3 center = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawSphere(center, pushRadius);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        Gizmos.DrawWireSphere(center, pushRadius);
    }
}
