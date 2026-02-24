using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 문 컨트롤러.
/// requiredTriggers[] 에 BoxColorTrigger를 등록하면, 전부 활성화될 때 문이 열리고
/// 하나라도 비활성화되면 닫힌다.
///
/// Open() / Close()를 직접 호출하거나,
/// Inspector의 OnOpened / OnClosed UnityEvent를 이용해 다른 오브젝트와 연결할 수 있다.
/// </summary>
public class DoorController : MonoBehaviour
{
    public enum OpenMode
    {
        SlideUp,    // 위로 올라감
        SlideDown,  // 아래로 내려감
        SlideRight, // transform.right 방향으로 이동
        SlideLeft,  // -transform.right 방향으로 이동
        RotateY,    // Y축 회전 (경첩형 문)
    }

    [Header("문 동작")]
    [Tooltip("문이 열리는 방식")]
    public OpenMode openMode = OpenMode.SlideUp;

    [Tooltip("슬라이드 거리(m) 또는 회전 각도(도)")]
    public float openAmount = 3f;

    [Tooltip("열리고 닫히는 데 걸리는 시간(초)")]
    public float duration = 0.5f;

    [Header("트리거 연결")]
    [Tooltip("등록된 트리거가 전부 활성화돼야 문이 열림. 비어 있으면 Open()/Close() 직접 호출 방식으로만 동작")]
    public BoxColorTrigger[] requiredTriggers;

    [Header("이벤트")]
    public UnityEvent OnOpened;
    public UnityEvent OnClosed;

    public bool IsOpen => _isOpen;

    bool       _isOpen;
    Vector3    _closedLocalPos;
    Quaternion _closedLocalRot;

    void Awake()
    {
        _closedLocalPos = transform.localPosition;
        _closedLocalRot = transform.localRotation;
    }

    void OnEnable()
    {
        for (int i = 0; i < requiredTriggers.Length; i++)
        {
            if (requiredTriggers[i] == null) continue;
            requiredTriggers[i].OnActivated.AddListener(CheckState);
            requiredTriggers[i].OnDeactivated.AddListener(CheckState);
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < requiredTriggers.Length; i++)
        {
            if (requiredTriggers[i] == null) continue;
            requiredTriggers[i].OnActivated.RemoveListener(CheckState);
            requiredTriggers[i].OnDeactivated.RemoveListener(CheckState);
        }
    }

    // ── 트리거 상태 재검사 ────────────────────────────────────

    void CheckState()
    {
        if (requiredTriggers == null || requiredTriggers.Length == 0) return;

        int activeCount = 0;
        for (int i = 0; i < requiredTriggers.Length; i++)
            if (requiredTriggers[i] != null && requiredTriggers[i].IsActive)
                activeCount++;

        if (activeCount >= requiredTriggers.Length)
            Open();
        else
            Close();
    }

    // ── 공개 메서드 ──────────────────────────────────────────

    public void Open()
    {
        if (_isOpen) return;
        _isOpen = true;
        StopAllCoroutines();
        StartCoroutine(AnimateDoor(true));
        OnOpened?.Invoke();
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        StopAllCoroutines();
        StartCoroutine(AnimateDoor(false));
        OnClosed?.Invoke();
    }

    // ── 내부 애니메이션 ───────────────────────────────────────

    IEnumerator AnimateDoor(bool opening)
    {
        Vector3    startPos = transform.localPosition;
        Quaternion startRot = transform.localRotation;

        Vector3    targetPos = _closedLocalPos;
        Quaternion targetRot = _closedLocalRot;

        if (opening)
        {
            switch (openMode)
            {
                case OpenMode.SlideUp:
                    targetPos = _closedLocalPos + Vector3.up * openAmount;
                    break;
                case OpenMode.SlideDown:
                    targetPos = _closedLocalPos + Vector3.down * openAmount;
                    break;
                case OpenMode.SlideRight:
                    targetPos = _closedLocalPos + transform.right * openAmount;
                    break;
                case OpenMode.SlideLeft:
                    targetPos = _closedLocalPos - transform.right * openAmount;
                    break;
                case OpenMode.RotateY:
                    targetRot = _closedLocalRot * Quaternion.Euler(0f, openAmount, 0f);
                    break;
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            transform.localRotation = Quaternion.Lerp(startRot, targetRot, t);
            yield return null;
        }

        transform.localPosition = targetPos;
        transform.localRotation = targetRot;
    }
}
