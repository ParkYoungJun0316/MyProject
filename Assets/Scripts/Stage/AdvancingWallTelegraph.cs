using System.Collections;
using UnityEngine;

/// <summary>
/// AdvancingWall 출발 전 경고 연출 전담 컴포넌트.
///
/// [역할 분리]
///  AdvancingWall  → 언제·얼마나 이동하는지
///  이 컴포넌트    → 출발 전 흔들림·사운드 연출
///
/// [사용법]
///  1. 라인 루트(AdvancingWall 이 붙은 오브젝트)에 함께 부착.
///  2. AdvancingWall 인스펙터의 Telegraph 필드에 이 컴포넌트를 연결.
///  3. 각 항목을 Inspector에서 설정.
///
/// [흔들림]
///  Visual Root 에 지정한 자식 Transform만 로컬 좌표로 흔들림.
///  Rigidbody / 물리 위치는 변경하지 않음.
/// </summary>
public class AdvancingWallTelegraph : MonoBehaviour
{
    [Header("경고 시간")]
    [Tooltip("전진 시작 전 경고 구간(초). 0이면 연출 없이 즉시 출발")]
    [SerializeField] float telegraphDuration = 0f;

    [Header("흔들림 (비주얼 전용)")]
    [Tooltip("흔들릴 비주얼 자식 Transform. 비워두면 흔들림 없음")]
    [SerializeField] Transform visualRoot;
    [Tooltip("흔들림 최대 진폭(m)")]
    [SerializeField] float shakeAmplitude = 0f;
    [Tooltip("흔들림 주파수(Hz). 예) 10~15 = 빠르게 잔떨림")]
    [SerializeField] float shakeFrequency = 0f;

    [Header("사운드")]
    [Tooltip("AudioSource. 비워두면 같은 오브젝트에서 자동 탐색")]
    [SerializeField] AudioSource audioSource;
    [Tooltip("흔들림 구간(텔레그래프) 시작 시 재생. 비워두면 재생 없음")]
    [SerializeField] AudioClip telegraphClip;
    [Tooltip("실제 전진 시작 순간 재생. 비워두면 재생 없음")]
    [SerializeField] AudioClip moveClip;

    /// <summary>외부(AdvancingWall)에서 대기 시간 계산용으로 읽는 경고 구간(초).</summary>
    public float Duration => telegraphDuration;

    Coroutine _routine;
    Vector3   _visualOrigin;

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        _visualOrigin = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>
    /// 경고 연출 시작 (흔들림 + 경고음).
    /// 완료 대기는 Duration 만큼 WaitForSeconds 후 Cancel() 호출로 마무리.
    /// </summary>
    public void Play()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(TelegraphRoutine());
    }

    /// <summary>전진 시작 순간 이동 사운드 재생.</summary>
    public void PlayMoveSound()
    {
        if (moveClip != null && audioSource != null)
            audioSource.PlayOneShot(moveClip);
    }

    /// <summary>연출 중단 + 비주얼 원상복귀. 색상 일치 중단 / Deactivate 시 호출.</summary>
    public void Cancel()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        ResetVisual();
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator TelegraphRoutine()
    {
        if (telegraphClip != null && audioSource != null)
            audioSource.PlayOneShot(telegraphClip);

        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            if (visualRoot != null && shakeAmplitude > 0f && shakeFrequency > 0f)
            {
                float ox = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * shakeAmplitude;
                float oy = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f * 0.73f + 1.1f) * shakeAmplitude * 0.4f;
                visualRoot.localPosition = _visualOrigin + new Vector3(ox, oy, 0f);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetVisual();
        _routine = null;
    }

    void ResetVisual()
    {
        if (visualRoot != null)
            visualRoot.localPosition = _visualOrigin;
    }
}
