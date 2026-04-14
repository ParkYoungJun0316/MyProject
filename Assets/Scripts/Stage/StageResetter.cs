using UnityEngine;

/// <summary>
/// Stage 루트 오브젝트에 붙이는 완전 재시작 컴포넌트.
///
/// [동작]
/// Awake 시점에 모든 하위 오브젝트의 초기 활성 상태를 기록.
/// RestoreChildStates() 호출 시:
///   1. 씬에 날아다니는 TrapProjectile 전부 즉시 파괴
///   2. 게임 중 SetActive(false)된 하위 오브젝트(부서진 바닥 등) 원상 복구
///
/// PhaseManager.RestartCurrentPhase()가 이 메서드를 호출한 뒤
/// Stage 오브젝트를 SetActive(false → true)로 사이클 → 모든 컴포넌트 OnDisable/OnEnable
///
/// [사용법]
/// 각 Stage 루트 GameObject에 이 컴포넌트 추가. 설정 없음.
/// </summary>
public class StageResetter : MonoBehaviour
{
    struct ChildState
    {
        public GameObject obj;
        public bool       activeSelf;
    }

    ChildState[] _initialStates;

    void Awake()
    {
        RecordInitialStates();
    }

    void RecordInitialStates()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        _initialStates = new ChildState[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            _initialStates[i] = new ChildState
            {
                obj        = all[i].gameObject,
                activeSelf = all[i].gameObject.activeSelf
            };
        }
    }

    /// <summary>
    /// 씬 투사체 제거 + 하위 오브젝트 초기 상태 복원.
    /// PhaseManager.RestartCurrentPhase()에서 호출됨.
    /// 호출 후 이 GameObject의 SetActive(false → true) 사이클이 이어짐.
    /// </summary>
    public void RestoreChildStates()
    {
        DestroyAllTrapProjectiles();
        RestoreStates();
    }

    void DestroyAllTrapProjectiles()
    {
        TrapProjectile[] projectiles = FindObjectsByType<TrapProjectile>(FindObjectsSortMode.None);
        foreach (TrapProjectile p in projectiles)
            if (p != null) Destroy(p.gameObject);
    }

    void RestoreStates()
    {
        if (_initialStates == null) return;
        foreach (ChildState state in _initialStates)
            if (state.obj != null)
                state.obj.SetActive(state.activeSelf);
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 초기 상태 복원")]
    void Debug_Restore() => RestoreChildStates();

    [ContextMenu("초기 상태 다시 기록")]
    void Debug_Record() => RecordInitialStates();
#endif
}
