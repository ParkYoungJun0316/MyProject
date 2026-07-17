using UnityEngine;

/// <summary>
/// SceneFlowManager 중계 컴포넌트 — 클리어 → 다음 씬의 **확정 배선** (NetworkDesign §11.1).
/// SceneFlowManager는 DontDestroyOnLoad라 다른 씬에서 Inspector로 직접 연결 불가.
/// 반드시 이 Relay를 각 씬에 배치하고 UnityEvent → LoadNextScene으로 연결한다.
///
/// [배치 방법]
/// 1. 각 씬에 빈 GameObject 생성 → SceneFlowRelay 컴포넌트 추가
/// 2. StageManager.OnStageClear → SceneFlowRelay.LoadNextScene 연결
///    (PhaseManager 씬은 onAllPhasesComplete → SceneFlowRelay.LoadNextScene)
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

#if UNITY_EDITOR
    [ContextMenu("테스트: 다음 씬으로")]
    void Debug_Next() => LoadNextScene();
#endif
}
