using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>파편이 모여드는 출발 방향. 연출 확정 전 비교용(PlaytestLog #13).</summary>
public enum TileRewindOrigin { Random, Below, Above }

/// <summary>복구 되감기 연출 설정 — 함정 Inspector에 그대로 노출된다.</summary>
[System.Serializable]
public class TileRewindSettings
{
    [Tooltip("파편이 제자리로 모이는 시간(초). 0이면 연출 없이 즉시 복구.")]
    public float duration = 0.8f;

    [Tooltip("파편 출발 거리 최소/최대(m) — 타일 중심 기준.")]
    public float distanceMin = 2f;
    public float distanceMax = 5f;

    [Tooltip("출발 방향. Random=전방향 / Below=아래 반구 / Above=위 반구.")]
    public TileRewindOrigin origin = TileRewindOrigin.Random;

    [Tooltip("타일마다 시작을 0~이 값(초) 사이로 랜덤 지연 — 여러 칸이 동시에 복구될 때 우르르 모이는 느낌.")]
    public float stagger = 0.15f;

    [Tooltip("되감기가 시작되는 순간 재생할 효과음(2D). **복구 1회당 1번만** — 같은 프레임에 여러 칸이\n" +
             "복구돼도(최대 24칸) 한 번만 난다. None이면 무음. 클립은 SFXLibrary의 ReverseTime 슬롯.")]
    public SFXId sfxId = SFXId.ReverseTime;
}

/// <summary>
/// 파괴된 바닥 타일이 복구될 때 **흩어진 파편이 랜덤 위치에서 날아와 다시 맞춰지는** 시각 연출 —
/// M 스테이지 바닥 함정(<see cref="TongueController"/> / <see cref="MouthBossJawSmash"/>) 전용.
/// 구 TileRestorePopGroup(부풀었다 가라앉는 팝)은 2026-09-22 사용자 결정으로 폐기하고 이것으로 대체했다.
///
/// [판정은 건드리지 않는다] 호출부가 타일을 SetActive(true)로 **먼저** 켜 둔다 — 콜라이더는 복구 명령
/// 순간부터 유효하고, 여기서는 되감기 동안 타일 **Renderer만** 꺼 둔다. 그래서 머신마다 연출 타이밍이
/// 달라도 낙사 판정은 갈리지 않는다(새 RPC·NV 없음).
///
/// [왜 실제 파편을 되감지 않나] 파괴 파편은 수명(2초) 뒤 삭제되고 구멍 아래로 떨어진다. 복구는 몇 초~수십
/// 초 뒤라 되감을 대상이 없다. 대신 같은 파편 프리팹을 복구 순간 새로 스폰해, 프리팹 안의 **조립된
/// 조각 배치를 도착점**으로 삼고 랜덤 위치에서 출발시킨다. 물리·궤적 기록·수명 관리가 전부 필요 없다.
///
/// 두 함정 모두 우리 컴포넌트가 없는 타일 GameObject 배열을 밖에서 껐다 켜므로, 타일별 진행 상태
/// (코루틴 / 스폰한 파편 / 꺼 둔 Renderer)를 여기서 대신 들고 있는다.
/// </summary>
public sealed class TileRestoreRewindGroup
{
    sealed class Running
    {
        public Coroutine routine;
        public GameObject shards;
        public Renderer[] hidden;
    }

    readonly MonoBehaviour _host;
    readonly Dictionary<GameObject, Running> _running = new();
    readonly System.Random _rng = new(); // 순수 시각 연출 — 머신마다 달라도 된다. 전역 Random 스트림은 건드리지 않는다.

    /// <param name="host">코루틴을 돌릴 함정 컴포넌트. host가 꺼지면 연출도 같이 죽는다(ResetAll로 정리).</param>
    public TileRestoreRewindGroup(MonoBehaviour host) => _host = host;

    /// <summary>타일을 끄기 직전에 호출 — 이 칸의 되감기가 아직 돌고 있으면 접는다.</summary>
    public void OnTileBroken(GameObject tile) => Stop(tile);

    /// <summary>
    /// 타일을 켠 **직후** 호출. 설정이 꺼져 있거나(duration 0) 파편 프리팹이 없거나 host가 꺼지는 중이면
    /// 아무것도 안 한다(= 즉시 복구).
    /// </summary>
    public void Play(GameObject tile, GameObject shardPrefab, TileRewindSettings s)
    {
        if (tile == null) return;
        Stop(tile);
        if (s == null || s.duration <= 0f || shardPrefab == null) return;
        if (_host == null || !_host.isActiveAndEnabled) return;

        Renderer[] rends = tile.GetComponentsInChildren<Renderer>(false);
        Renderer main = tile.GetComponent<Renderer>() ?? (rends.Length > 0 ? rends[0] : null);
        Material mat = main != null ? main.sharedMaterial : null;
        Vector3 center = main != null && main.bounds.size.sqrMagnitude > 0.0001f ? main.bounds.center : tile.transform.position;

        // TileDebrisUtil.BreakTile과 같은 스폰 규약(렌더러 중심 · 타일 회전 · 현재 재질).
        GameObject shards = Object.Instantiate(shardPrefab, center, tile.transform.rotation);
        foreach (Renderer r in shards.GetComponentsInChildren<Renderer>(true))
            if (mat != null) r.sharedMaterial = mat;
        // 되감기 파편은 순수 연출 — 물리·충돌을 끈다(플레이어·다른 파편과 부딪히지 않게).
        foreach (Collider c in shards.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (Rigidbody rb in shards.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.detectCollisions = false; }

        var hidden = new List<Renderer>();
        foreach (Renderer r in rends)
            if (r.enabled) { r.enabled = false; hidden.Add(r); }

        var run = new Running { shards = shards, hidden = hidden.ToArray() };
        _running[tile] = run;
        run.routine = _host.StartCoroutine(RewindRoutine(tile, run, s));

        PlaySfxOncePerRestore(s);
    }

    // 복구는 항상 한 프레임 안에서 타일 배열을 한 바퀴 돌며 Play를 부른다(RestoreAll/RestoreAllTiles) —
    // 그래서 "같은 프레임 = 같은 복구"로 묶어 1회만 재생한다.
    int _lastSfxFrame = -1;

    void PlaySfxOncePerRestore(TileRewindSettings s)
    {
        if (s.sfxId == SFXId.None || Time.frameCount == _lastSfxFrame) return;
        _lastSfxFrame = Time.frameCount;
        SFXManager.Instance?.Play(s.sfxId);
    }

    /// <summary>진행 중인 되감기를 접는다 — 파편 삭제 + 꺼 둔 Renderer 복원.</summary>
    public void Stop(GameObject tile)
    {
        if (tile == null || !_running.TryGetValue(tile, out Running run)) return;
        if (run.routine != null && _host != null) _host.StopCoroutine(run.routine);
        Finish(tile, run);
    }

    /// <summary>host의 OnDisable에서 호출 — StopAllCoroutines()가 정리 없이 죽였을 수 있는 전부를 원복.</summary>
    public void ResetAll()
    {
        foreach (var pair in new List<KeyValuePair<GameObject, Running>>(_running))
            Finish(pair.Key, pair.Value);
        _running.Clear();
    }

    void Finish(GameObject tile, Running run)
    {
        if (run.shards != null) Object.Destroy(run.shards);
        if (run.hidden != null)
            foreach (Renderer r in run.hidden)
                if (r != null) r.enabled = true;
        _running.Remove(tile);
    }

    IEnumerator RewindRoutine(GameObject tile, Running run, TileRewindSettings s)
    {
        // 조각 = Rigidbody가 달린 자식(파편 프리팹 규약). 없으면 직계 자식 전부.
        var pieces = new List<Transform>();
        foreach (Rigidbody rb in run.shards.GetComponentsInChildren<Rigidbody>(true))
            if (rb.transform != run.shards.transform) pieces.Add(rb.transform);
        if (pieces.Count == 0)
            foreach (Transform c in run.shards.transform) pieces.Add(c);

        int n = pieces.Count;
        var toPos = new Vector3[n]; var toRot = new Quaternion[n];
        var fromPos = new Vector3[n]; var fromRot = new Quaternion[n];
        Vector3 center = run.shards.transform.position;
        for (int i = 0; i < n; i++)
        {
            toPos[i] = pieces[i].position;
            toRot[i] = pieces[i].rotation;
            fromPos[i] = center + RandomDir(s.origin) * Range(s.distanceMin, Mathf.Max(s.distanceMin, s.distanceMax));
            fromRot[i] = Random3DRotation();
            pieces[i].SetPositionAndRotation(fromPos[i], fromRot[i]);
        }

        float delay = s.stagger > 0f ? Range(0f, s.stagger) : 0f;
        // 대기 동안에도 파편은 출발점에 떠 있다 — 복구 순간부터 "모이기 시작"이 보이게.
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float t = 0f;
        while (t < s.duration)
        {
            if (tile == null || run.shards == null) break;
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / s.duration);
            float e = p * p * p; // ease-in: 처음엔 천천히, 끝에서 빨려 들어가 "탁" 붙는다
            for (int i = 0; i < n; i++)
                if (pieces[i] != null)
                    pieces[i].SetPositionAndRotation(Vector3.LerpUnclamped(fromPos[i], toPos[i], e),
                                                     Quaternion.SlerpUnclamped(fromRot[i], toRot[i], e));
            yield return null;
        }

        Finish(tile, run);
    }

    Vector3 RandomDir(TileRewindOrigin origin)
    {
        float y = Range(-1f, 1f);
        float phi = Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
        var d = new Vector3(r * Mathf.Cos(phi), y, r * Mathf.Sin(phi));
        if (origin == TileRewindOrigin.Below) d.y = -Mathf.Abs(d.y);
        else if (origin == TileRewindOrigin.Above) d.y = Mathf.Abs(d.y);
        return d;
    }

    Quaternion Random3DRotation() =>
        Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));

    float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
}
