using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Tutorial 사전 게이트 구간 — 색 구분 없는 단일 트리거 존 (NetworkDesign.md §6B.3).
///
/// [역할]
/// 순수 로컬 트리거 감지만 담당 — "지금 존 안에 몇 명이 있는가"만 센다.
/// 네트워크 판정(헤드카운트 비교·카운트다운·씬 전환)은 하지 않는다 — 그건 Host 레인에서
/// TutorialNetworkManager가 이 클래스의 OccupantCount를 읽어서 처리한다
/// (ColoredStartZone이 판정 없이 점유 상태만 보고하고, StageStartGate가 판정하는 것과 동일 원칙).
///
/// [설정 방법]
/// 1. 빈 GameObject에 이 스크립트 + Collider(Is Trigger) 추가
/// 2. Tutorial 씬에 1개만 배치 (색별 4구역 아님)
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialGatherZone : MonoBehaviour
{
    public static TutorialGatherZone Instance { get; private set; }

    // clientId 기준으로 저장 — 물리 OnTriggerExit이 Destroy 시 항상 발동하는 건 아니므로
    // TutorialNetworkManager.OnClientLeft에서 RemoveOccupant(clientId)로도 강제 정리 가능해야 함.
    readonly Dictionary<ulong, Player> _occupants = new();

    public int OccupantCount => _occupants.Count;

    void Awake()
    {
        Instance = this;
        GetComponent<Collider>().isTrigger = true;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnTriggerEnter(Collider other) => TryAdd(other);

    /// <summary>
    /// NetworkTransform이 transform.position을 직접 설정하거나 플레이어가 스폰 시
    /// 존 안에 이미 있을 경우 OnTriggerEnter가 발동하지 않을 수 있다 — OnTriggerStay로 보완.
    /// </summary>
    void OnTriggerStay(Collider other) => TryAdd(other);

    void OnTriggerExit(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null) return;

        NetworkObject netObj = p.GetComponent<NetworkObject>();
        if (netObj == null) return;

        _occupants.Remove(netObj.OwnerClientId);
    }

    void TryAdd(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;

        NetworkObject netObj = p.GetComponent<NetworkObject>();
        if (netObj == null) return;

        _occupants[netObj.OwnerClientId] = p;
    }

    /// <summary>
    /// Host 전용: 클라이언트 이탈 시 존 점유 목록에서 강제 제거.
    /// GameObject가 Despawn/Destroy될 때 물리 OnTriggerExit이 항상 보장되진 않으므로,
    /// TutorialNetworkManager.OnClientLeft에서 명시적으로 호출해 헤드카운트가 stale해지는 것을 막는다.
    /// </summary>
    public void RemoveOccupant(ulong clientId) => _occupants.Remove(clientId);
}
