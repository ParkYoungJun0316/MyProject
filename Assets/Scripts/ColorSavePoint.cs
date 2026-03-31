using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 기반 세이브 포인트.
/// 자식 오브젝트의 ColorSaveZone들 중 requiredCount 개 이상이 동시에 점유되면 활성화.
///
/// [씬 설정]
///  1. 빈 오브젝트에 이 컴포넌트 부착
///  2. 자식으로 ColorSaveZone 오브젝트 4개 배치 (Red / Yellow / Green / Blue)
///     - 각 ColorSaveZone에 Collider(Is Trigger = true) 추가
///     - spawnPoint Transform 설정 (리스폰 위치)
///  3. requiredCount 설정:
///     - 1 = 솔로 테스트 (Blue 1명만 들어가도 활성화)
///     - 4 = 4명 모두 제자리에 있어야 활성화
///
/// [리스폰]
///  활성화 시 각 플레이어의 spawnPoint를 자신의 색상 존 위치로 설정.
///  이후 사망 시 해당 존 위치에서 리스폰.
/// </summary>
public class ColorSavePoint : MonoBehaviour
{
    public enum ActivationMode
    {
        Once,   // 최초 1회 활성화 후 고정
        Always, // 조건 충족할 때마다 재활성화 (리스폰 위치 갱신)
    }

    [Header("세이브 포인트 설정")]
    [Tooltip("활성화에 필요한 존 동시 점유 수\n1 = 솔로 / 4 = 4인 전원 필요")]
    public int requiredCount = 1;

    [Tooltip("세이브 순서. 높은 번호를 밟은 뒤에는 낮은 번호로 덮어쓰지 않음")]
    public int saveOrder = 0;

    [Tooltip("Once: 최초 1회 활성화 후 고정 / Always: 조건 충족마다 재활성화")]
    public ActivationMode activationMode = ActivationMode.Once;

    [Tooltip("활성화 시 체력을 최대로 회복할지 여부")]
    public bool healOnActivate = true;

    [Header("이벤트")]
    [Tooltip("세이브 포인트 활성화 시 호출 (연출, 사운드 등 연결)")]
    public UnityEvent OnActivated;

    bool _isActivated;
    int  _occupiedCount;

    ColorSaveZone[] _zones;

    void Awake()
    {
        _zones = GetComponentsInChildren<ColorSaveZone>(true);
    }

    // ── ColorSaveZone에서 호출 ───────────────────────────────────

    public void OnZoneOccupied(ColorSaveZone zone)
    {
        _occupiedCount = CountOccupied();

        if (activationMode == ActivationMode.Once && _isActivated) return;
        if (_occupiedCount < requiredCount) return;

        Activate();
    }

    public void OnZoneVacated(ColorSaveZone zone)
    {
        _occupiedCount = CountOccupied();
    }

    // ── 내부 ─────────────────────────────────────────────────────

    void Activate()
    {
        _isActivated = true;

        // 점유된 각 존의 플레이어 리스폰 위치를 해당 존으로 설정
        for (int i = 0; i < _zones.Length; i++)
        {
            ColorSaveZone zone = _zones[i];
            if (!zone.IsOccupied) continue;

            Player p = zone.CurrentPlayer;
            if (p == null) continue;

            p.SetSpawnPoint(zone.SpawnPosition, zone.SpawnRotation, saveOrder);

            if (healOnActivate)
                p.heart = p.maxHeart;
        }

        OnActivated?.Invoke();
    }

    int CountOccupied()
    {
        int count = 0;
        for (int i = 0; i < _zones.Length; i++)
            if (_zones[i].IsOccupied) count++;
        return count;
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 강제 활성화")]
    void Debug_Activate() => Activate();

    void OnDrawGizmos()
    {
        Gizmos.color = _isActivated ? Color.green : Color.gray;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}
