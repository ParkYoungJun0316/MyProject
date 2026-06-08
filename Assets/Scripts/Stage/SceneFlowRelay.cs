using UnityEngine;

/// <summary>
/// SceneFlowManager 중계 컴포넌트.
/// SceneFlowManager는 DontDestroyOnLoad라 다른 씬에서 Inspector로 직접 연결 불가.
/// 이 컴포넌트를 각 씬에 배치하고 UnityEvent에 연결하면 SceneFlowManager를 대신 호출.
///
/// [배치 방법]
/// 1. 각 씬에 빈 GameObject 생성 → SceneFlowRelay 컴포넌트 추가
/// 2. PhaseManager.onAllPhasesComplete → SceneFlowRelay.LoadNextScene 연결
///    또는 StageManager.OnStageClear → SceneFlowRelay.LoadNextScene 연결
/// </summary>
public class SceneFlowRelay : MonoBehaviour
{
    /// <summary>
    /// 다음 씬으로 전환.
    /// PhaseManager.onAllPhasesComplete 또는 StageManager.OnStageClear 에 연결.
    /// </summary>
    public void LoadNextScene()
    {
        if (SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.LoadNextScene();
        else
            Debug.LogWarning("[SceneFlowRelay] SceneFlowManager 인스턴스가 없습니다. M.Stage1에 SceneFlowManager가 배치됐는지 확인하세요.");
    }

    /// <summary>현재 씬을 처음부터 리셋. 필요 시 이벤트에 연결 가능.</summary>
    public void ReloadCurrentScene()
    {
        if (SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.ReloadCurrentScene();
        else
            Debug.LogWarning("[SceneFlowRelay] SceneFlowManager 인스턴스가 없습니다.");
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 다음 씬으로")]
    void Debug_Next() => LoadNextScene();
#endif
}
