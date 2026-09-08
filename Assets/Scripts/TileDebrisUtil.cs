using UnityEngine;

/// <summary>
/// 타일류 오브젝트 파괴 시 파편 스폰 공용 유틸.
/// tile의 현재 Renderer.sharedMaterial을 그대로 파편에 입혀 흑/백/Reveal 등
/// 실시간 색이 항상 일치하도록 한다(별도 흑/백 조각 프리팹 불필요).
/// TongueController 외에 Breakable 등 다른 파괴 연출에서도 재사용 가능 — tile 자체의
/// 비활성화/렌더 끄기는 호출부 책임(이 유틸은 파편 스폰만 담당).
///
/// [스폰 위치] 대상의 Renderer bounds 중심에 스폰한다 — 피벗이 밑바닥이나 한쪽에 붙은 오브젝트
/// (Lump 피벗 = 밑면, FrontTooth 피벗 = bounds 중심에서 z −3.1)에서 파편이 바닥 밑이나 허공에서
/// 태어나는 걸 막는다. FloorTile은 피벗 = bounds 중심이라 결과가 같다(혀·턱 연출 무변화).
/// Renderer가 없거나 bounds가 무효면 transform.position으로 되돌아간다.
///
/// [크기] 파편 프리팹은 유닛 크기(약 1)로 만들어 두고 대상과의 크기 차이는 호출부가 debrisScale로
/// 보정한다 — 공용 러블 조각 하나(`Assets/Prefab/RubbleShards.prefab`)를 Lump·Tooth 등 크기가
/// 다른 오브젝트에 재사용하기 위한 규약. 부모 없이 스폰하므로 대상 부모의 스케일은 물려받지 않는다.
/// 반대로 대상 크기에 맞춰 만든 전용 파편(FloorTile 기준 5 × 1 × 5인 FloorTileShards)은 debrisScale = 1로 쓴다.
///
/// [결정성] 임펄스는 호출부가 준 seed로만 뽑는다 — 전 머신이 같은 seed를 주면 파편이 똑같이 튄다.
/// 전역 UnityEngine.Random은 건드리지 않는다(FloorManager.HandleFloorRollChanged와 같은 원칙).
/// </summary>
public static class TileDebrisUtil
{
    // 파편이 플레이어를 밀면 이동 권한(Owner + ClientNetworkTransform)이 로컬 연출에 오염된다 —
    // 파편은 머신마다 별개 물체이므로 플레이어와 충돌해선 안 된다. Physics 설정(레이어 매트릭스)을
    // 건드리지 않고 per-collider excludeLayers로만 차단.
    static int _playerLayerMask = -1;

    static int PlayerLayerMask
    {
        get
        {
            if (_playerLayerMask < 0)
                _playerLayerMask = LayerMask.GetMask("Player", "PlayerStealth", "PlayerDead");
            return _playerLayerMask;
        }
    }

    /// <summary>
    /// debrisPrefab을 tile 위치/회전으로 스폰하고, tile의 현재 재질을 그대로 입힌 뒤
    /// seed로 결정된 방향의 임펄스 + 토크를 준다. debrisLifetime&gt;0이면 그 후 자동 삭제.
    /// debrisScale(기본 1)은 공용 파편 하나를 여러 크기의 오브젝트(Lump·Tooth 등)에 재사용할 때
    /// 호출부가 스케일만 보정하도록 — 파편 프리팹 자체는 원본 유닛 크기 그대로 둔다.
    /// 반환값은 스폰된 파편 루트 — 호출부가 조기 정리(복구 등)에 쓸 수 있다. 스폰 안 하면 null.
    /// </summary>
    public static GameObject BreakTile(GameObject tile, GameObject debrisPrefab, float debrisLifetime,
                                        float impulseMin, float impulseMax, int seed, float debrisScale = 1f)
    {
        if (tile == null || debrisPrefab == null) return null;

        Renderer tileRend = tile.GetComponent<Renderer>() ?? tile.GetComponentInChildren<Renderer>(true);
        Material mat = tileRend != null ? tileRend.sharedMaterial : null;

        // bounds는 씬에 고정된 좌표·메시에서 나오므로 전 머신 동일 — 스폰 위치도 갈리지 않는다.
        Vector3 spawnPos = tile.transform.position;
        if (tileRend != null && tileRend.bounds.size.sqrMagnitude > 0.0001f)
            spawnPos = tileRend.bounds.center;

        GameObject debris = Object.Instantiate(debrisPrefab, spawnPos, tile.transform.rotation);
        if (debrisScale != 1f)
            debris.transform.localScale *= debrisScale;

        if (mat != null)
        {
            Renderer[] debrisRenderers = debris.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < debrisRenderers.Length; i++)
                debrisRenderers[i].sharedMaterial = mat;
        }

        Collider[] cols = debris.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            cols[i].excludeLayers = cols[i].excludeLayers.value | PlayerLayerMask;

        // GetComponentsInChildren의 순회 순서는 같은 프리팹이면 전 머신 동일 —
        // 같은 seed면 조각별 임펄스도 1:1로 같다.
        var rng = new System.Random(seed);
        Rigidbody[] bodies = debris.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Vector3 dir = RandomDirection(rng);
            dir.y = Mathf.Abs(dir.y) + 0.5f;   // 항상 위쪽으로 튀도록 보정
            dir.Normalize();

            float impulse = RandomRange(rng, impulseMin, impulseMax);
            bodies[i].AddForce(dir * impulse, ForceMode.Impulse);
            bodies[i].AddTorque(RandomDirection(rng) * impulse, ForceMode.Impulse);
        }

        if (debrisLifetime > 0f)
            Object.Destroy(debris, debrisLifetime);

        return debris;
    }

    /// <summary>UnityEngine.Random.onUnitSphere의 System.Random 버전 (구면 균일 분포).</summary>
    static Vector3 RandomDirection(System.Random rng)
    {
        float y = (float)rng.NextDouble() * 2f - 1f;
        float phi = (float)rng.NextDouble() * Mathf.PI * 2f;
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
        return new Vector3(r * Mathf.Cos(phi), y, r * Mathf.Sin(phi));
    }

    static float RandomRange(System.Random rng, float min, float max)
        => min + (float)rng.NextDouble() * (max - min);
}
