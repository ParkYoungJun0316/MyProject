using System.Collections;
using UnityEngine;

/// <summary>
/// DropTrap 경고 바닥 마커.
/// - 경고 단계: 얇은 외곽 원만 표시 (progress=0), warnDuration 동안 유지.
/// - 낙하 단계: 낙하 시작 시점부터 fallDuration에 걸쳐 progress 0→1 (바깥→안 채움).
///
/// progress는 낙하체의 실제 위치/충돌을 전혀 참조하지 않는다. DropTrap이 미리
/// (spawnHeight / speed)로 계산한 낙하 소요 시간을 그대로 타이머로 채우는 방식이라
/// 트리거 충돌 감지가 필요 없고, 스테이지마다 바닥 높이가 달라도 안전하다.
///
/// [셰이더 연동]
/// targetRenderer 머티리얼에 fillProperty(기본 "_Fill", 0~1 float) 프로퍼티가 있으면
/// MaterialPropertyBlock으로 갱신. 프로퍼티가 없는 셰이더는 조용히 무시된다.
/// </summary>
public class DropWarnMarker : MonoBehaviour
{
    [Header("채움 표시")]
    [Tooltip("채움 진행도를 전달할 Renderer. 비워두면 자식에서 자동 탐색")]
    [SerializeField] private Renderer targetRenderer = null;

    [Tooltip("Renderer 머티리얼의 채움 셰이더 프로퍼티 이름 (0~1 float)")]
    [SerializeField] private string fillProperty = "_Fill";

    MaterialPropertyBlock _block;
    int _fillId;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        _fillId = Shader.PropertyToID(fillProperty);
        _block  = new MaterialPropertyBlock();
        SetProgress(0f);
    }

    public void SetProgress(float t)
    {
        if (targetRenderer == null) return;

        targetRenderer.GetPropertyBlock(_block);
        _block.SetFloat(_fillId, Mathf.Clamp01(t));
        targetRenderer.SetPropertyBlock(_block);
    }

    /// <summary>
    /// duration에 걸쳐 progress를 0→1로 채운다. duration&lt;=0이면 즉시 1로 스냅.
    /// DropTrap이 낙하 시작 시점에 호출.
    /// </summary>
    public IEnumerator FillOverTime(float duration)
    {
        if (duration <= 0f)
        {
            SetProgress(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetProgress(elapsed / duration);
            yield return null;
        }
        SetProgress(1f);
    }
}
