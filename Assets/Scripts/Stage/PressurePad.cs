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
/// [색상 규칙 — SessionColorSlotMap이 SSOT]
///  - designColor   : Inspector에서 설정하는 4인 기준 원래 설계 색 (직렬화 유지)
///  - EffectiveColor: 런타임 실제 적용 색. **SessionColorSlotMap에서 직접 당겨온다.**
///    이번 판에 그 색이 없으면 Common으로 떨어져 누구나 밟을 수 있게 된다.
///  - Common이면 모든 플레이어 허용
///
///  예전엔 StagePressurePadSetup이 씬 전역을 훑어 SetEffectiveColor()로 밀어넣었는데,
///  그 방식은 호출 순서가 곧 정합성이고 1회 적용 후 래치라 실패해도 복구되지 않았다.
///  지금은 이 패드가 스스로 당겨오므로 매핑이 언제 확정되든 다음 프레임에 수렴한다.
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
             "런타임 실제 적용은 SessionColorSlotMap이 정한다 — 이번 판에 그 색이 없으면 Common이 된다.")]
    [FormerlySerializedAs("ownerColor")]
    public PlayerColorType designColor = PlayerColorType.Common;

    [Header("필요 인원")]
    [Tooltip("발판이 충족되려면 올라가 있어야 하는 최소 인원 (StagePressurePadSetup이 인원에 맞게 조정함)")]
    public int requiredCount = 1;

    [Header("사운드 (3D)")]
    [Tooltip("고유색(또는 Common)으로 정확히 인식돼 인원이 늘 때 재생. None이면 무음. 발을 뗄 때는 재생하지 않음.")]
    [SerializeField] SFXId pressSfxId = SFXId.Pad_Press;
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] float pressMinDistance = 5f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] float pressMaxDistance = 20f;
    [SerializeField] AudioRolloffMode pressRolloffMode = AudioRolloffMode.Logarithmic;

    [Header("이벤트")]
    [Tooltip("조건이 충족됐을 때 발동 (requiredCount명 이상 올라감)")]
    public UnityEvent OnFulfilled;

    [Tooltip("조건이 해제됐을 때 발동 (인원 부족)")]
    public UnityEvent OnUnfulfilled;

    [Tooltip("발판 위 인원이 바뀔 때마다 발동 (currentCount, requiredCount). UI 전용 — Host/Client 모두 발동.")]
    public UnityEvent<int, int> OnCountChanged;

    public bool IsFulfilled  => _isFulfilled;
    public int  CurrentCount => _players.Count;

    /// <summary>런타임 실제 적용 색. SessionColorSlotMap에서 당겨온 값이다.</summary>
    public PlayerColorType EffectiveColor => _effectiveColor;

    PlayerColorType       _effectiveColor;
    int                   _appliedMapVersion = -1;
    bool                  _isFulfilled;
    int                   _lastNotifiedCount = -1;
    readonly List<Player> _players = new List<Player>();

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        PullEffectiveColor();
    }

    /// <summary>
    /// 매핑이 바뀌었을 때만 실제 색을 다시 당겨오고 비주얼을 맞춘다.
    /// 색이 바뀌면 이미 올라가 있던 사람의 자격도 바뀌므로 점유를 재평가한다 —
    /// 안 하면 "이제 못 밟는 색인데 여전히 밟은 것으로 세어지는" 상태가 남는다.
    /// </summary>
    void PullEffectiveColor()
    {
        if (_appliedMapVersion == SessionColorSlotMap.Version) return;
        _appliedMapVersion = SessionColorSlotMap.Version;

        PlayerColorType next = SessionColorSlotMap.Resolve(designColor);
        if (next == _effectiveColor && _players.Count == 0)
        {
            GetComponent<ColoredPadVisual>()?.Apply(_effectiveColor);
            return;
        }

        _effectiveColor = next;
        GetComponent<ColoredPadVisual>()?.Apply(_effectiveColor);

        for (int i = _players.Count - 1; i >= 0; i--)
        {
            Player p = _players[i];
            if (p == null || !p.CountsForOccupancy || !IsAllowed(p)) _players.RemoveAt(i);
        }
        Evaluate();
    }

    void OnTriggerEnter(Collider other)
    {
        Player p = other.GetComponent<Player>();
        if (p == null || !p.CountsForOccupancy) return;
        if (!IsAllowed(p)) return;
        if (_players.Contains(p)) return;

        _players.Add(p);
        PlayPressSfx();
        Evaluate();
    }

    void OnTriggerExit(Collider other)
    {
        Player p = other.GetComponent<Player>();
        if (p == null) return;

        if (_players.Remove(p))
            Evaluate();
    }

    // 발판 위에 서 있는 동안 색이 바뀌는 경우(예: 검정 발판 위에서 흰→검 전환) 즉시 재판정.
    // ColorTile.TryAddOccupant(OnTriggerStay 경유)와 동일 원칙 — Enter/Exit만으로는 색 전환을
    // 못 잡아서 나갔다 들어와야만 인식되던 문제를 해결.
    void OnTriggerStay(Collider other)
    {
        Player p = other.GetComponent<Player>();
        if (p == null || !p.CountsForOccupancy) return;

        bool allowedNow  = IsAllowed(p);
        bool alreadyOn   = _players.Contains(p);

        if (allowedNow && !alreadyOn)
        {
            _players.Add(p);
            PlayPressSfx();
            Evaluate();
        }
        else if (!allowedNow && alreadyOn)
        {
            _players.Remove(p);
            Evaluate();
        }
    }

    // 죽은 플레이어를 매 프레임 정리
    void Update()
    {
        PullEffectiveColor();

        bool changed = false;
        for (int i = _players.Count - 1; i >= 0; i--)
        {
            if (_players[i] == null || !_players[i].CountsForOccupancy)
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

    // 로컬 3D 재생 — 발판 트리거는 각 머신이 CNT 위치로 이미 점유를 안다(RPC 불필요).
    // 2D였을 때는 원격 플레이어가 누른 발판도 내 귀 옆에서 나는 것처럼 풀볼륨으로 들렸음
    // (2026-09-01 수정) — 발판 위치 기준 3D로 전환.
    // OnFulfilled(Host-only)에 걸면 Client가 안 들림. 인원 감소(Exit)에서는 호출하지 말 것.
    void PlayPressSfx()
    {
        if (pressSfxId == SFXId.None) return;
        SFXManager.Instance?.PlayAtPoint(pressSfxId, transform.position, pressMinDistance, pressMaxDistance, pressRolloffMode);
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
        // Host가 OnFulfilled → DoorController.CheckPadState() → StageNetworkState._doorOpenStates NV
        // (StagePressurePadSetup.SetupDoorNetworkSync 배선) → 전원 door.Open()/Close() 연출로 전파됨.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        if (_isFulfilled) OnFulfilled?.Invoke();
        else              OnUnfulfilled?.Invoke();
    }

}
