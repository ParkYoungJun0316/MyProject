using UnityEngine;

/// <summary>
/// CapacityTile의 정적 감지 기둥에 붙는 트리거 중계기. 런타임에 CapacityTile이 생성하며
/// 씬/프리팹에 저장되지 않는다 — 인스펙터에서 직접 붙일 일은 없다.
///
/// [왜 별도 오브젝트인가]
/// 감지 기둥이 타일의 자식이면 타일과 함께 내려간다. 그러면
/// "타일 침강 → 탑승자가 트리거 밖으로 → 점유 0 → 복귀 → 다시 점유 → 침강"의 피드백 루프가
/// 생기고, 진동하는 계는 미세한 타이밍 차가 증폭돼 머신마다 결과가 갈린다.
/// 점유 판정을 '플레이어 위치만의 함수'로 유지해야 로컬 계산의 수렴이 보장되므로
/// (TStage4TrapRandomization.md §4.1 — 2026-09-18 로컬 계산 확정),
/// 감지 기둥은 타일과 분리된 채 휴지 위치에 고정된다. 그래서 콜백을 넘겨줄 중계기가 필요하다.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class CapacityTileTriggerRelay : MonoBehaviour
{
    CapacityTile _owner;

    public void Bind(CapacityTile owner) => _owner = owner;

    void OnTriggerEnter(Collider other)
    {
        if (_owner != null) _owner.HandleTriggerEnter(other);
    }

    void OnTriggerStay(Collider other)
    {
        if (_owner != null) _owner.HandleTriggerStay(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (_owner != null) _owner.HandleTriggerExit(other);
    }
}
