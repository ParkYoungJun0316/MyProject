using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 다중 스파이크 레인 필드.
/// activeLaneCount개의 레인을 랜덤으로 선택해 동시에 발동.
/// 발동되지 않은 나머지 레인은 항상 비활성 상태를 유지.
/// TrapBase.activateInterval로 반복 주기를 제어.
///
/// [씬 계층 구조]
///  SpikeLaneField (이 스크립트)
///  ├─ Lane_0 (SpikeLane)
///  │   ├─ SpikeTrap 타일 ...
///  ├─ Lane_1 (SpikeLane)
///  │   └─ SpikeTrap 타일 ...
///  └─ Lane_7 (SpikeLane)
///      └─ SpikeTrap 타일 ...
///
/// [설정]
/// 1. 자식 SpikeLane들은 자동 수집 (lanes 필드를 직접 채워도 됨)
/// 2. 각 SpikeLane 아래 SpikeTrap의 startActive = false, activateInterval = 0 으로 설정
/// 3. activeLaneCount로 동시에 발동할 레인 수 설정
/// 4. activateInterval(TrapBase)로 발동 반복 주기 설정
/// </summary>
public class SpikeLaneField : TrapBase
{
    [Header("Lane Field")]
    [Tooltip("발동 대상 레인 목록. 비워두면 자식 SpikeLane을 자동 수집.")]
    [SerializeField] SpikeLane[] lanes;

    [Tooltip("한 번에 발동할 레인 수. lanes 개수를 넘을 수 없음.")]
    [SerializeField, Range(1, 8)] int activeLaneCount = 1;

    [Tooltip("true: 직전에 발동됐던 레인은 다음 발동 후보에서 제외 (연속 방지)")]
    [SerializeField] bool excludeLastLanes = false;

    [Header("경고 연출 (추후 구현)")]
    [Tooltip("경고 비주얼 프리팹 — 추후 연결")]
    [SerializeField] GameObject warningPrefab = null;

    [Tooltip("경고 표시 지속 시간(초)")]
    [SerializeField] float warningDuration = 0f;

    [Tooltip("경고 오브젝트를 생성할 부모 Transform — 추후 연결")]
    [SerializeField] Transform warningParent = null;

    [Tooltip("경고 오디오 클립 — 추후 연결")]
    [SerializeField] AudioClip warningSound = null;

    int[] _lastSelected;

    protected override void Awake()
    {
        base.Awake();
        if (lanes == null || lanes.Length == 0)
            lanes = GetComponentsInChildren<SpikeLane>(true);
    }

    protected override void OnTrapTrigger()
    {
        if (lanes == null || lanes.Length == 0) return;

        int count = Mathf.Clamp(activeLaneCount, 1, lanes.Length);
        int[] selected = PickRandomIndices(count);

        for (int i = 0; i < selected.Length; i++)
            if (lanes[selected[i]] != null) lanes[selected[i]].Trigger();

        _lastSelected = selected;
    }

    /// <summary>count개의 레인 인덱스를 중복 없이 랜덤 선택.</summary>
    int[] PickRandomIndices(int count)
    {
        List<int> pool = new List<int>(lanes.Length);

        for (int i = 0; i < lanes.Length; i++)
        {
            if (excludeLastLanes && _lastSelected != null && count < lanes.Length)
            {
                bool wasLast = false;
                for (int j = 0; j < _lastSelected.Length; j++)
                    if (_lastSelected[j] == i) { wasLast = true; break; }
                if (wasLast) continue;
            }
            pool.Add(i);
        }

        // 제외 후 풀이 부족하면 전체 풀로 폴백
        if (pool.Count < count)
        {
            pool.Clear();
            for (int i = 0; i < lanes.Length; i++) pool.Add(i);
        }

        // Fisher-Yates 셔플
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }

        int[] result = new int[count];
        for (int i = 0; i < count; i++) result[i] = pool[i];
        return result;
    }

    protected override void OnDeactivated()
    {
        if (lanes == null) return;
        for (int i = 0; i < lanes.Length; i++)
            if (lanes[i] != null) lanes[i].ForceDeactivate();
    }
}
