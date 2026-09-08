using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파괴된 바닥 타일이 복구될 때 살짝 부풀었다 가라앉는 시각 연출 — M 스테이지 바닥 함정
/// (<see cref="TongueController"/> / <see cref="MouthBossJawSmash"/>) 전용. 다른 파괴 오브젝트
/// (Breakable 등)는 이 연출을 쓰지 않는다.
///
/// 판정(SetActive)은 호출부가 이 클래스를 부르기 전에 이미 끝내 둘 것 — 여기서는 보이는 것만 만진다.
///
/// 두 함정 모두 우리 컴포넌트가 붙어 있지 않은 타일 GameObject 배열을 밖에서 껐다 켜므로, 컴포넌트라면
/// 공짜로 얻었을 것들(원래 스케일 캐시 / 코루틴 핸들 / 파괴 시 취소 / 페이즈 종료 시 원복)을 타일 단위로
/// 대신 들고 있는다. 이 부기를 두 함정이 각자 복제하지 않게 하는 것이 이 클래스의 존재 이유다.
///
/// 스케일은 원래 크기 "이상"으로만 움직인다(1 → 1+진폭 → 1) — Renderer와 Collider가 같은 Transform인
/// FloorTile에서 콜라이더가 원래보다 작아지는 순간이 있으면 그 칸에 선 플레이어가 빠져 낙사한다.
/// 대신 커지는 쪽 부작용은 남는다: 진폭 0.06이면 5 × 1 × 5 타일의 윗면이 약 3cm 올라가고 좌우로 0.15m씩
/// 이웃 칸을 침범해, 복구 순간 근처에 선 플레이어가 살짝 들리거나 밀릴 수 있다. 진폭을 키울 때 감안할 것.
/// </summary>
public sealed class TileRestorePopGroup
{
    readonly MonoBehaviour _host;

    // 타일별 원래 스케일. 그 타일이 처음 부서지기 직전(=팝이 한 번도 안 돈 상태)에만 캐시하므로
    // 팝 도중의 부푼 값이 기준으로 굳는 일이 없다.
    readonly Dictionary<GameObject, Vector3> _originalScale = new();

    // 지금 돌고 있는 팝. 자연 종료 시 코루틴이 스스로 자기 항목을 지우므로 "도는 중"으로 신뢰할 수 있다.
    readonly Dictionary<GameObject, Coroutine> _running = new();

    /// <param name="host">코루틴을 돌릴 함정 컴포넌트. host가 꺼지면 팝도 같이 죽는다.</param>
    public TileRestorePopGroup(MonoBehaviour host) => _host = host;

    /// <summary>
    /// 타일을 끄기 직전에 호출 — 원래 스케일을 캐시하고, 팝이 아직 돌고 있으면 접는다.
    /// (복구 직후 같은 칸이 다시 부서질 때 부푼 스케일로 굳는 걸 막는다.)
    /// </summary>
    public void OnTileBroken(GameObject tile)
    {
        if (tile == null) return;
        if (!_originalScale.ContainsKey(tile))
            _originalScale[tile] = tile.transform.localScale;
        Stop(tile);
    }

    /// <summary>
    /// 타일을 켠 직후에 호출 — 팝 시작. duration이나 amplitude가 0이면 아무 것도 안 한다(연출 없는
    /// 즉시 복구). 한 번도 부서진 적 없는 타일은 기준 스케일이 없으므로 건너뛴다.
    /// </summary>
    public void Play(GameObject tile, float duration, float amplitude, float jitter)
    {
        if (tile == null) return;
        Stop(tile);

        if (duration <= 0f || amplitude <= 0f) return;
        // host가 비활성화되는 중(OnDisable에서 복구를 지나가는 경로)이면 코루틴이 곧바로 죽는다 —
        // 시작하지 않는 게 맞다.
        if (_host == null || !_host.isActiveAndEnabled) return;
        if (!_originalScale.TryGetValue(tile, out Vector3 rest)) return;

        _running[tile] = _host.StartCoroutine(PopRoutine(tile, rest, duration, amplitude, jitter));
    }

    /// <summary>진행 중인 팝을 접고 캐시된 원래 스케일로 되돌린다.</summary>
    public void Stop(GameObject tile)
    {
        if (tile == null) return;
        if (_running.TryGetValue(tile, out Coroutine routine) && routine != null && _host != null)
            _host.StopCoroutine(routine);
        _running.Remove(tile);
        if (_originalScale.TryGetValue(tile, out Vector3 rest))
            tile.transform.localScale = rest;
    }

    /// <summary>
    /// host의 OnDisable에서 호출 — StopAllCoroutines()가 팝을 자체 정리 없이 죽였을 수 있으므로
    /// 지금까지 만졌던 모든 타일의 스케일을 원래 값으로 되돌린다(부푼 채 멈춘 타일 방지).
    /// </summary>
    public void ResetAll()
    {
        foreach (var pair in _originalScale)
        {
            if (pair.Key != null)
                pair.Key.transform.localScale = pair.Value;
        }
        _running.Clear();
    }

    IEnumerator PopRoutine(GameObject tile, Vector3 rest, float duration, float amplitude, float jitter)
    {
        // 전역 RNG 스트림은 필요할 때만 당긴다. 이 값이 머신마다 달라도 판정은 갈리지 않는다 —
        // 콜라이더가 원래보다 작아지는 순간이 없고, 타일 추첨 쪽은 InitState로 시드를 직접 세운 뒤
        // state를 되돌리므로(TongueController.PickSeededRegion / MouthBossJawSmash.ShuffleSeeded)
        // 이 draw에 영향받지 않는다.
        if (jitter > 0f)
            duration += Random.Range(-jitter, jitter);
        duration = Mathf.Max(0.01f, duration);

        Transform tr = tile.transform;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            if (tile == null)
            {
                _running.Remove(tile);
                yield break;
            }
            tr.localScale = rest * (1f + amplitude * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }

        if (tile != null)
            tr.localScale = rest;
        _running.Remove(tile);
    }
}
