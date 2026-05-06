using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 압력 발판(Pressure Pad).
///
/// [동작]
///  - 지정한 색상의 플레이어가 requiredCount명 이상 올라가면 IsFulfilled = true → OnFulfilled 발생
///  - 인원이 부족해지면 IsFulfilled = false → OnUnfulfilled 발생
///
/// [색상 규칙]
///  - ownerColor = Common : 색상 무관, 모든 플레이어 허용
///  - ownerColor = Blue/Red 등 : isUniqueColor 상태이고 playerColorType이 일치하는 플레이어만 허용
///
/// [씬 설정]
///  1. 빈 GameObject에 Collider(Is Trigger = true) + 이 스크립트 추가
///  2. ownerColor, requiredCount 설정
///  3. DoorController.requiredPads[]에 등록
/// </summary>
[RequireComponent(typeof(Collider))]
public class PressurePad : MonoBehaviour
{
    [Header("색상 소유권")]
    [Tooltip("Common: 모든 플레이어 허용 / 나머지: 해당 색 고유색 모드 플레이어만 허용")]
    public PlayerColorType ownerColor = PlayerColorType.Common;

    [Header("필요 인원")]
    [Tooltip("발판이 충족되려면 올라가 있어야 하는 최소 인원")]
    public int requiredCount = 1;

    [Header("이벤트")]
    [Tooltip("조건이 충족됐을 때 발동 (requiredCount명 이상 올라감)")]
    public UnityEvent OnFulfilled;

    [Tooltip("조건이 해제됐을 때 발동 (인원 부족)")]
    public UnityEvent OnUnfulfilled;

    public bool IsFulfilled => _isFulfilled;
    public int  CurrentCount => _players.Count;

    bool _isFulfilled;
    readonly List<Player> _players = new List<Player>();

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        Player p = other.GetComponent<Player>();
        if (p == null || p.IsDead) return;
        if (!IsAllowed(p)) return;
        if (_players.Contains(p)) return;

        _players.Add(p);
        Evaluate();
    }

    void OnTriggerExit(Collider other)
    {
        Player p = other.GetComponent<Player>();
        if (p == null) return;

        if (_players.Remove(p))
            Evaluate();
    }

    // 죽은 플레이어를 매 프레임 정리
    void Update()
    {
        bool changed = false;
        for (int i = _players.Count - 1; i >= 0; i--)
        {
            if (_players[i] == null || _players[i].IsDead)
            {
                _players.RemoveAt(i);
                changed = true;
            }
        }
        if (changed) Evaluate();
    }

    // ── 내부 ────────────────────────────────────────────────

    bool IsAllowed(Player p)
    {
        if (ownerColor == PlayerColorType.Common) return true;
        return p.isUniqueColor && p.playerColorType == ownerColor;
    }

    void Evaluate()
    {
        bool nowFulfilled = _players.Count >= requiredCount;
        if (nowFulfilled == _isFulfilled) return;

        _isFulfilled = nowFulfilled;
        if (_isFulfilled) OnFulfilled?.Invoke();
        else              OnUnfulfilled?.Invoke();
    }

}
