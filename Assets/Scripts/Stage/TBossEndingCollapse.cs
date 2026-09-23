using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// T.Boss 엔딩 연출 — Bossdown 대화가 끝나면 사탕(Sphere)이 한 번 들렸다가 내리찍고,
/// 그 순간 P4 바닥이 부서져 전원이 추락한 채 다음 씬으로 넘어간다.
/// M.Boss의 MouthBossJawSmash.ForceBreakAllTilesForEnding과 같은 자리(Bossdown OnAllReady)에 들어가는 T판.
///
/// [배선]
///  Bossdown PhaseDialogueGate.OnAllReady → Play()   (기존 SceneFlowRelay.LoadNextScene 연결은 제거)
///  OnImpact                               → SceneFlowRelay.LoadNextScene
///  LoadNextScene을 대화 종료가 아니라 충돌 순간에 거는 이유: 그때부터 전환 대기(clearToTransitionDelay)가
///  돌아야 추락이 암전 직전까지 보인다. 추락 중 낙사(Die 애니·사망)는 그대로 둔다 — 클리어 후
///  실패·리로드는 StageNetworkState.BeginStageResetOnServer 가드가 막는다(ReviveSystemDesign.md §2).
///
/// [네트워크] 새 RPC·NV 없음. OnAllReady가 Host ClientRpc로 전 머신에 오므로 각 머신이 로컬로 같은
///  연출을 재생한다. 추락은 Owner 이동(CNT) 그대로, 파편 시드는 칸 인덱스라 전 머신 동일.
///
/// [바닥] P4 Ground는 칸으로 나뉘지 않은 통판이라 판정(SetActive false)은 한 번에 끄고, 보이는 부분
///  (enclosingWalls 안쪽 사각형)만 격자로 쪼개 파편을 뿌린다. 벽 밖은 벽에 가려 보이지 않는다.
/// </summary>
public class TBossEndingCollapse : MonoBehaviour
{
    [Header("사탕")]
    [Tooltip("식도 끝 Sphere(BackGround/Candy). 연출 동안 AdvancingWall은 정지시킨다.")]
    [SerializeField] Transform candy;

    [Tooltip("내리찍은 뒤 사탕 밑면이 바닥 윗면에서 떨어져 멈추는 높이(m).\n" +
             "플레이어 키보다 높게 — 사탕 콜라이더는 켜진 채라 닿으면 플레이어를 밀어낸다.")]
    [SerializeField] float headClearance = 3f;

    [Tooltip("내리찍기 전에 위로 들리는 높이(m). 0이면 들지 않고 바로 내리찍는다.")]
    [SerializeField] float windupHeight = 3f;

    [SerializeField] float windupDuration = 0.6f;

    [Tooltip("들린 상태로 멈춰 있는 시간(초).")]
    [SerializeField] float windupHold = 0.2f;

    [Tooltip("내리찍는 데 걸리는 시간(초). 가속(ease-in)으로 떨어진다.")]
    [SerializeField] float slamDuration = 0.25f;

    [Header("바닥")]
    [Tooltip("P4 Ground (5). 충돌 순간 SetActive(false) — 콜라이더가 꺼져 전원 추락.")]
    [SerializeField] GameObject floor;

    [Tooltip("P4 벽 4개(BossWall_F/B/L/R)의 콜라이더. 파편은 이 벽들 안쪽 사각형에만 뿌린다.\n" +
             "비우면 바닥 전체 크기 기준(maxCellsPerSide로 개수 제한).")]
    [SerializeField] Collider[] enclosingWalls;

    [Header("파편")]
    [Tooltip("M.Boss와 같은 FloorTileShards(5 × 1 × 5). 비우면 파편 없이 바닥만 사라진다.")]
    [SerializeField] GameObject debrisPrefab;

    [Tooltip("debrisPrefab 한 변 길이(m). FloorTileShards = 5.")]
    [SerializeField] float debrisPrefabSize = 5f;

    [Tooltip("쪼갤 칸 한 변 목표 길이(m). 영역을 이 크기에 가깝게 나눈다.")]
    [SerializeField] float debrisCellSize = 5f;

    [SerializeField] int maxCellsPerSide = 6;

    [SerializeField] float debrisLifetime = 3f;
    [SerializeField] float debrisImpulseMin = 2f;
    [SerializeField] float debrisImpulseMax = 5f;

    [Header("파괴음 (Breakable_Destroy, M.Boss 엔딩과 동일)")]
    [SerializeField] float breakSfxMinDistance = 5f;
    [SerializeField] float breakSfxMaxDistance = 50f;
    [SerializeField] AudioRolloffMode breakSfxRolloffMode = AudioRolloffMode.Logarithmic;

    [Header("이벤트")]
    [Tooltip("바닥이 부서지는 순간. → SceneFlowRelay.LoadNextScene")]
    public UnityEvent OnImpact;

    const int DebrisSeedSalt = 0x7B055E4D;

    bool _played;

    /// <summary>Bossdown PhaseDialogueGate.OnAllReady에 연결. 두 번째 호출부터는 무시.</summary>
    public void Play()
    {
        if (_played) return;
        _played = true;
        StartCoroutine(PlayRoutine());
    }

    IEnumerator PlayRoutine()
    {
        if (candy != null)
            yield return CandySlam();

        BreakFloor();
        OnImpact?.Invoke();
    }

    IEnumerator CandySlam()
    {
        // StopClock이 이미 멈췄지만, 혹시 남은 이동 코루틴이 위치를 덮어쓰지 않게 한 번 더.
        if (candy.TryGetComponent(out AdvancingWall candyWall)) candyWall.Deactivate();
        candy.TryGetComponent(out Rigidbody rb);

        Vector3 start = candy.position;
        Vector3 top = start + Vector3.up * Mathf.Max(0f, windupHeight);

        // 사탕 밑면을 바닥 윗면 + headClearance에 맞춘다. 이미 그보다 낮으면 더 내려가지 않는다
        // (페이즈 시간제한을 바꿔 사탕이 더 내려와 있어도 플레이어를 밀지 않게).
        Vector3 end = start;
        if (TryGetBounds(candy.gameObject, out Bounds candyBounds) && TryGetBounds(floor, out Bounds floorBounds))
        {
            float drop = candyBounds.min.y - (floorBounds.max.y + headClearance);
            if (drop > 0f) end = start + Vector3.down * drop;
        }

        if (windupHeight > 0f)
        {
            yield return MoveCandy(rb, start, top, windupDuration, easeIn: false);
            if (windupHold > 0f) yield return new WaitForSeconds(windupHold);
        }

        yield return MoveCandy(rb, top, end, slamDuration, easeIn: true);
    }

    IEnumerator MoveCandy(Rigidbody rb, Vector3 from, Vector3 to, float duration, bool easeIn)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = easeIn ? k * k : 1f - (1f - k) * (1f - k);
            SetCandyPosition(rb, Vector3.LerpUnclamped(from, to, k));
            yield return null;
        }
        SetCandyPosition(rb, to);
    }

    void SetCandyPosition(Rigidbody rb, Vector3 pos)
    {
        // Kinematic Rigidbody — AdvancingWall과 같은 MovePosition 경로.
        if (rb != null) rb.MovePosition(pos);
        else candy.position = pos;
    }

    void BreakFloor()
    {
        if (floor == null)
        {
            Debug.LogWarning($"[TBossEndingCollapse] floor가 비어 있습니다 — 바닥이 안 부서집니다. ({name})", this);
            return;
        }

        if (TryGetBounds(floor, out Bounds fb))
        {
            float minX = fb.min.x, maxX = fb.max.x, minZ = fb.min.z, maxZ = fb.max.z;
            ShrinkToWalls(fb.center, ref minX, ref maxX, ref minZ, ref maxZ);

            Material mat = floor.TryGetComponent(out Renderer floorRend) ? floorRend.sharedMaterial : null;
            SpawnDebrisGrid(mat, minX, maxX, minZ, maxZ, fb.center.y, floor.transform.rotation);

            SFXManager.Instance?.PlayAtPoint(SFXId.Breakable_Destroy,
                new Vector3((minX + maxX) * 0.5f, fb.max.y, (minZ + maxZ) * 0.5f),
                breakSfxMinDistance, breakSfxMaxDistance, breakSfxRolloffMode);
        }

        floor.SetActive(false);
    }

    /// <summary>
    /// 벽 콜라이더마다 바닥 중심 기준 어느 변에 있는지(x축이냐 z축이냐, 어느 쪽이냐)를 보고
    /// 그 변을 벽의 안쪽 면까지 당긴다. 조임이 끝난 뒤(10×10)의 실제 벽 위치를 그대로 쓰므로
    /// 최종 크기가 바뀌어도 인스펙터 수치를 맞출 필요가 없다.
    /// </summary>
    void ShrinkToWalls(Vector3 c, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
    {
        if (enclosingWalls == null) return;

        foreach (Collider wall in enclosingWalls)
        {
            if (wall == null) continue;
            Bounds b = wall.bounds;
            float dx = b.center.x - c.x;
            float dz = b.center.z - c.z;

            if (Mathf.Abs(dx) > Mathf.Abs(dz))
            {
                if (dx < 0f) minX = Mathf.Max(minX, b.max.x);
                else         maxX = Mathf.Min(maxX, b.min.x);
            }
            else
            {
                if (dz < 0f) minZ = Mathf.Max(minZ, b.max.z);
                else         maxZ = Mathf.Min(maxZ, b.min.z);
            }
        }
    }

    void SpawnDebrisGrid(Material mat, float minX, float maxX, float minZ, float maxZ, float y, Quaternion rot)
    {
        if (debrisPrefab == null) return;

        float width = maxX - minX;
        float depth = maxZ - minZ;
        if (width <= 0f || depth <= 0f) return;

        float cell = Mathf.Max(0.1f, debrisCellSize);
        int nx = Mathf.Clamp(Mathf.CeilToInt(width / cell), 1, Mathf.Max(1, maxCellsPerSide));
        int nz = Mathf.Clamp(Mathf.CeilToInt(depth / cell), 1, Mathf.Max(1, maxCellsPerSide));
        float cellW = width / nx;
        float cellD = depth / nz;
        float scale = Mathf.Min(cellW, cellD) / Mathf.Max(0.01f, debrisPrefabSize);

        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                Vector3 pos = new Vector3(minX + cellW * (ix + 0.5f), y, minZ + cellD * (iz + 0.5f));
                int seed = DebrisSeedSalt ^ ((ix * nz + iz) * unchecked((int)0x9E3779B9));
                TileDebrisUtil.SpawnAt(debrisPrefab, pos, rot, mat, debrisLifetime,
                                       debrisImpulseMin, debrisImpulseMax, seed, scale);
            }
        }
    }

    static bool TryGetBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        if (go == null) return false;

        Renderer r = go.GetComponentInChildren<Renderer>();
        if (r != null) { bounds = r.bounds; return true; }

        if (go.TryGetComponent(out Collider col)) { bounds = col.bounds; return true; }

        return false;
    }
}
