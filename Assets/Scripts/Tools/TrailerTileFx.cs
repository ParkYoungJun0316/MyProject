using UnityEngine;

/// <summary>
/// 트레일러 촬영 전용(Marketing_ 씬에서 Timeline Signal로 호출). 바닥 타일 하나를 게임 함정과 똑같이
/// 깨고(TileDebrisUtil.BreakTile + SetActive false) 되살린다(SetActive true + TileRestoreRewindGroup 되감기).
/// GridTileCollapse.BreakTile/RestoreTile, TongueController, MouthBossJawSmash와 같은 유틸·순서를 쓴다.
/// 게임 로직에는 붙지 않는다 — 네트워크·판정 없음.
/// </summary>
public class TrailerTileFx : MonoBehaviour
{
    public GameObject tile;
    public GameObject debrisPrefab;
    public float debrisLifetime = 1f;
    public float impulseMin = 0.5f;
    public float impulseMax = 1.5f;
    public int seed;

    [Tooltip("라운드 중 자동 복구 되감기")]
    public TileRewindSettings autoRewind = new TileRewindSettings { duration = 0.5f, distanceMin = 0.5f, distanceMax = 1f, stagger = 0.05f, sfxId = SFXId.None };

    [Tooltip("정산·외침 뒤 일괄 복구 되감기")]
    public TileRewindSettings fullRewind = new TileRewindSettings { duration = 1.2f, distanceMin = 2f, distanceMax = 5f, stagger = 0.3f, sfxId = SFXId.None };

    TileRestoreRewindGroup _rewind;
    GameObject _debris;
    int _cycle;

    TileRestoreRewindGroup Rewind => _rewind ??= new TileRestoreRewindGroup(this);

    public void Break()
    {
        if (tile == null || !tile.activeSelf) return;
        Rewind.OnTileBroken(tile);
        if (debrisPrefab != null)
        {
            if (_debris != null) Destroy(_debris);
            _debris = TileDebrisUtil.BreakTile(tile, debrisPrefab, debrisLifetime, impulseMin, impulseMax, seed ^ (++_cycle * 0x27220A95));
        }
        tile.SetActive(false);
    }

    public void RestoreAuto() => Restore(autoRewind);
    public void RestoreFull() => Restore(fullRewind);

    void Restore(TileRewindSettings s)
    {
        if (tile == null || tile.activeSelf) return;
        tile.SetActive(true);
        Rewind.Play(tile, debrisPrefab, s);
    }
}
