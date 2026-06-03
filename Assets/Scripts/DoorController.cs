using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 문 컨트롤러.
///
/// [이동 방식]
///  Rigidbody.MovePosition(Kinematic) 사용.
///  transform.position 직접 변경 방식 대비 물리 충돌이 올바르게 처리됨.
///
/// [즉사 판정]
///  문이 닫히는 도중 플레이어와 충돌하면 Player.KillInstantly() 호출.
///  OnCollisionEnter + OnCollisionStay 양쪽에서 감지하므로 이미 닿아 있는 경우도 처리됨.
///
/// [압력 발판 연동]
///  requiredPads[] 에 PressurePad를 등록하면, 전부 충족될 때 문이 열린다.
///  latchOnOpen = false : 발판에서 내려오면 즉시 닫힘
///  latchOnOpen = true  : 한 번 열리면 발판을 벗어나도 열린 상태 유지
///
/// [Inspector 필수 설정]
///  - Rigidbody: Is Kinematic = true, Use Gravity = false (스크립트가 자동 추가/설정)
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
    public float openAmount = 0f;

    [Tooltip("열리고 닫히는 데 걸리는 시간(초)")]
    public float duration = 0f;

    [Header("압력 발판 연동")]
    [Tooltip("등록된 발판이 전부 충족돼야 문이 열림. 비어 있으면 Open()/Close() 직접 호출 방식으로만 동작")]
    public PressurePad[] requiredPads;

    [Tooltip(
        "false: 발판에서 내려오면 즉시 문이 닫힘\n" +
        "true : 한 번 열리면 발판을 벗어나도 열린 상태 유지")]
    public bool latchOnOpen = false;

    [Header("이벤트")]
    [Tooltip("Open() 호출 직후 (열림 애니메이션 시작 시점)")]
    public UnityEvent OnOpened;

    [Tooltip("Close() 호출 직후 (닫힘 애니메이션 시작 시점). 기존 연결 유지용.")]
    public UnityEvent OnClosed;

    [Tooltip("닫힘 애니메이션이 끝나 문이 완전히 닫힌 뒤 1회 호출. Stage1 비활성화 등에 연결.")]
    public UnityEvent OnFullyClosed;

    public bool IsOpen    => _isOpen;
    public bool IsLatched => _isLatched;

    bool       _isOpen;
    bool       _isLatched;
    bool       _isClosing;

    Vector3    _closedLocalPos;
    Quaternion _closedLocalRot;

    Rigidbody  _rb;

    // ── 초기화 ────────────────────────────────────────────────

    void Awake()
    {
        _closedLocalPos = transform.localPosition;
        _closedLocalRot = transform.localRotation;

        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity  = false;
    }

    void OnEnable()
    {
        for (int i = 0; i < requiredPads.Length; i++)
        {
            if (requiredPads[i] == null) continue;
            requiredPads[i].OnFulfilled.AddListener(CheckPadState);
            requiredPads[i].OnUnfulfilled.AddListener(CheckPadState);
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < requiredPads.Length; i++)
        {
            if (requiredPads[i] == null) continue;
            requiredPads[i].OnFulfilled.RemoveListener(CheckPadState);
            requiredPads[i].OnUnfulfilled.RemoveListener(CheckPadState);
        }
    }

    // ── 발판 상태 재검사 ──────────────────────────────────────

    void CheckPadState()
    {
        if (requiredPads == null || requiredPads.Length == 0) return;
        if (latchOnOpen && _isLatched) return;

        bool allFulfilled = true;
        for (int i = 0; i < requiredPads.Length; i++)
        {
            if (requiredPads[i] == null || !requiredPads[i].IsFulfilled)
            {
                allFulfilled = false;
                break;
            }
        }

        if (allFulfilled)
        {
            if (latchOnOpen) _isLatched = true;
            Open();
        }
        else
        {
            Close();
        }
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

    /// <summary>래치 초기화 후 즉시 닫힌 위치로 텔레포트.</summary>
    public void Reset()
    {
        StopAllCoroutines();
        _isOpen    = false;
        _isLatched = false;
        _isClosing = false;
        _rb.position = LocalToWorld(_closedLocalPos);
        _rb.rotation = LocalToWorldRot(_closedLocalRot);
    }

    // ── 충돌 즉사 판정 ────────────────────────────────────────

    void OnCollisionEnter(Collision col)
    {
        if (!_isClosing) return;
        Player p = col.collider.GetComponent<Player>();
        p?.KillInstantly();
    }

    void OnCollisionStay(Collision col)
    {
        if (!_isClosing) return;
        Player p = col.collider.GetComponent<Player>();
        p?.KillInstantly();
    }

    // ── 내부 애니메이션 ───────────────────────────────────────

    IEnumerator AnimateDoor(bool opening)
    {
        _isClosing = !opening;

        Vector3    startWorldPos = _rb.position;
        Quaternion startWorldRot = _rb.rotation;

        // 목표 로컬 위치/회전 계산
        Vector3    targetLocalPos = _closedLocalPos;
        Quaternion targetLocalRot = _closedLocalRot;

        if (opening)
        {
            switch (openMode)
            {
                case OpenMode.SlideUp:
                    targetLocalPos = _closedLocalPos + Vector3.up * openAmount;
                    break;
                case OpenMode.SlideDown:
                    targetLocalPos = _closedLocalPos + Vector3.down * openAmount;
                    break;
                case OpenMode.SlideRight:
                    targetLocalPos = _closedLocalPos + transform.right * openAmount;
                    break;
                case OpenMode.SlideLeft:
                    targetLocalPos = _closedLocalPos - transform.right * openAmount;
                    break;
                case OpenMode.RotateY:
                    targetLocalRot = _closedLocalRot * Quaternion.Euler(0f, openAmount, 0f);
                    break;
            }
        }

        Vector3    targetWorldPos = LocalToWorld(targetLocalPos);
        Quaternion targetWorldRot = LocalToWorldRot(targetLocalRot);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _rb.MovePosition(Vector3.Lerp(startWorldPos, targetWorldPos, t));
            _rb.MoveRotation(Quaternion.Lerp(startWorldRot, targetWorldRot, t));
            yield return new WaitForFixedUpdate();
        }

        _rb.MovePosition(targetWorldPos);
        _rb.MoveRotation(targetWorldRot);
        _isClosing = false;

        if (!opening)
            OnFullyClosed?.Invoke();
    }

    // ── 좌표 변환 헬퍼 ───────────────────────────────────────

    Vector3 LocalToWorld(Vector3 localPos)
    {
        return transform.parent != null
            ? transform.parent.TransformPoint(localPos)
            : localPos;
    }

    Quaternion LocalToWorldRot(Quaternion localRot)
    {
        return transform.parent != null
            ? transform.parent.rotation * localRot
            : localRot;
    }
}
