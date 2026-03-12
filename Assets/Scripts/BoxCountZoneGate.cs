using UnityEngine;

/// <summary>
/// 여러 BoxCountZone이 모두 충족되면 문을 열고, 하나라도 미달이 되면 문을 닫습니다.
/// Map02 등 "모든 구역에 박스 배치" 클리어 조건에 사용.
/// </summary>
public class BoxCountZoneGate : MonoBehaviour
{
    [Tooltip("모두 충족되어야 문이 열림")]
    public BoxCountZone[] requiredZones;

    [Tooltip("열릴 문")]
    public DoorController door;

    void OnEnable()
    {
        for (int i = 0; i < requiredZones.Length; i++)
        {
            if (requiredZones[i] == null) continue;
            requiredZones[i].OnFulfilled.AddListener(CheckAllFulfilled);
            requiredZones[i].OnUnfulfilled.AddListener(OnZoneUnfulfilled);
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < requiredZones.Length; i++)
        {
            if (requiredZones[i] == null) continue;
            requiredZones[i].OnFulfilled.RemoveListener(CheckAllFulfilled);
            requiredZones[i].OnUnfulfilled.RemoveListener(OnZoneUnfulfilled);
        }
    }

    void CheckAllFulfilled()
    {
        if (door == null || requiredZones == null) return;

        for (int i = 0; i < requiredZones.Length; i++)
            if (requiredZones[i] == null || !requiredZones[i].IsFulfilled)
                return;

        door.Open();
    }

    void OnZoneUnfulfilled()
    {
        if (door != null)
            door.Close();
    }
}
