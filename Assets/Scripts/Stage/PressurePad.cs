using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// 압력 발판(Pressure Pad).
///
/// [동작]
///  - effectiveColor에 해당하는 플레이어가 requiredCount명 이상 올라가면 IsFulfilled = true → OnFulfilled 발생
///  - 인원이 부족해지면 IsFulfilled = false → OnUnfulfilled 발생
///
/// [색상 규칙]
///  - designColor  : Inspector에서 설정하는 4인 기준 원래 의도 색 (직렬화 유지)
///  - effectiveColor : 런타임 실제 적용 색. StagePressurePadSetup이 SetEffectiveColor()로 덮어씀
///  - Common이면 모든 플레이어 허용
///
/// [씬 설정]
///  1. 빈 GameObject에 Collider(Is Trigger = true) + 이 스크립트 추가
///  2. designColor, requiredCount 설정
///  3. DoorController.requiredPads[]에 등록
/// </summary>
[RequireComponent(typeof(Collider))]
public class PressurePad : MonoBehaviour
{
    [Header("색상 소유권")]
    [Tooltip("4인 기준 원래 설계 색. Common: 모든 플레이어 허용 / 나머지: 해당 색 고유색 플레이어만 허용.\n" +
             "런타임 실제 적용은 effectiveColor 기준 (StagePressurePadSetup이 덮어씀).")]
    [FormerlySerializedAs("ownerColor")]
    public PlayerColorType designColor = PlayerColorType.Common;

    [Header("필요 인원")]
    [Tooltip("발판이 충족되려면 올라가 있어야 하는 최소 인원 (StagePressurePadSetup이 인원에 맞게 조정함)")]
    public int requiredCount = 1;

    [Header("이벤트")]
    [Tooltip("조건이 충족됐을 때 발동 (requiredCount명 이상 올라감)")]
    public UnityEvent OnFulfilled;

    [Tooltip("조건이 해제됐을 때 발동 (인원 부족)")]
    public UnityEvent OnUnfulfilled;

    [Tooltip("발판 위 인원이 바뀔 때마다 발동 (currentCount, requiredCount). UI 전용 — Host/Client 모두 발동.")]
    public UnityEvent<int, int> OnCountChanged;

    public bool IsFulfilled  => _isFulfilled;
    public int  CurrentCount => _players.Count;

    /// <summary>런타임 실제 적용 색. Awake에서 designColor로 초기화되며 SetEffectiveColor()로 변경 가능.</summary>
    public PlayerColorType EffectiveColor => _effectiveColor;

    PlayerColorType       _effectiveColor;
    bool                  _isFulfilled;
    int                   _lastNotifiedCount = -1;
    readonly List<Player> _players = new List<Player>();

    void Awake()
    {
        _effectiveColor = designColor;

        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    /// <summary>
    /// 런타임 적용 색을 변경한다. StagePressurePadSetup에서 인원·색 배정 후 호출.
    /// 현재 올라가 있는 플레이어 목록은 초기화되지 않으므로 Setup 완료 전에 호출할 것.
    /// </summary>
    public void SetEffectiveColor(PlayerColorType color)
    {
        _effectiveColor = color;
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
        if (_effectiveColor == PlayerColorType.Common) return true;
        return p.isUniqueColor && p.playerColorType == _effectiveColor;
    }

    void Evaluate()
    {
        int  currentCount = _players.Count;
        bool nowFulfilled = currentCount >= requiredCount;

        // UI 갱신 이벤트 — 인원 변화 시마다 발동 (Host/Client 모두, 로컬 UI 전용)
        if (currentCount != _lastNotifiedCount)
        {
            _lastNotifiedCount = currentCount;
            OnCountChanged?.Invoke(currentCount, requiredCount);
        }

        if (nowFulfilled == _isFulfilled) return;

        _isFulfilled = nowFulfilled;

        // Client는 문 개폐 이벤트를 발동하지 않음.
        // Host가 OnFulfilled → DoorController.CheckPadState() → DoorNetworkSync._isOpen NV
        // → 전원 door.Open()/Close() 연출로 전파됨.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        if (_isFulfilled) OnFulfilled?.Invoke();
        else              OnUnfulfilled?.Invoke();
    }

}
