using System.Collections;
using UnityEngine;

/// <summary>
/// DropTrap 경고 바닥 마커.
/// - 경고 단계: 얇은 외곽 원만 표시 (progress=0), warnDuration 동안 유지.
/// - 낙하 단계: 월드 Y가 startY→groundY(기본 0)로 내려가는 동안 progress 0→1.
///   Y=groundY에 도달하는 순간 Fill=1. (등속 낙하 가정: y = startY - speed*t)
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
    /// 등속 낙하(속도 speed, 시작 높이 startY)가 groundY에 닿을 때까지 Fill 0→1.
    /// progress = 1 - (y - groundY) / (startY - groundY). y&lt;=groundY이면 1로 종료.
    /// </summary>
    public IEnumerator FillUntilWorldY(float startY, float speed, float groundY = 0f)
    {
        float span = startY - groundY;
        if (span <= 0f || speed <= 0f)
        {
            SetProgress(1f);
            yield break;
        }

        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float y = startY - speed * elapsed;
            float progress = 1f - (y - groundY) / span;
            SetProgress(progress);
            if (y <= groundY)
                break;
            yield return null;
        }
        SetProgress(1f);
    }
}
