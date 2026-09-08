using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 접촉 시 HP는 건드리지 않고 순수 넉백 + PunchHit 연출을 주는 범용 컴포넌트.
/// 튕기는 벽·범퍼 등 "닿으면 밖으로 밀려남" 상황에 사용.
/// 데미지 함정은 ContactDamage를 쓴다. 같은 오브젝트에 둘 다 붙이지 말 것.
///
/// [흐름]
///  Host가 충돌을 감지 → NetworkDamageUtil.ApplyKnockback (Owner AddForce)
///  → NetworkPlayerSetup.NotifyPunchHitFromServer (doPunchHit + PunchHit 3D SFX)
///
/// [방향 모드 — launchMode]
///  HorizontalFromCenter : 이 오브젝트 중심 → 플레이어 (수평 전용, y 제거). 기존 벽·범퍼용 기본값.
///  VerticalUp           : transform.up 고정 발사 (튕김 발판 / T.Boss P3 천장 ColorWall 맞추기용).
///                         Player.Move()에 접지 검사가 없어 넉백 억제(0.25초) 이후 공중에서도
///                         이동 입력이 그대로 먹히므로, 수직 발사 + 공중 조작으로 방향을 잡는다.
///                         Player mass = 1이라 force가 그대로 초기 속도(m/s) — 중력 -9.81 기준
///                         정점 높이 ≈ force² / 19.62 (force 10 → 약 5.1m).
///
/// [설정 방법]
///  1. 튕길 오브젝트에 이 스크립트 추가
///  2. Collider 추가
///     - Is Trigger = false : 물리적으로 막으면서 튕김 (벽 등)
///     - Is Trigger = true  : 통과하면서 튕김만 (구역·발판 등)
///  3. launchMode, 힘 min/max, knockbackInterval Inspector에서 설정
///     (선택 시 Gizmo로 실제 발사 방향 확인 가능)
/// </summary>
[RequireComponent(typeof(Collider))]
public class ContactKnockback : MonoBehaviour
{
    /// <summary>넉백 방향 계산 방식. 기본값(0) = 기존 수평 동작이라 기존 씬은 영향 없음.</summary>
    public enum LaunchMode
    {
        /// <summary>이 오브젝트 중심 → 플레이어 방향, y 제거 (벽·범퍼).</summary>
        HorizontalFromCenter = 0,
        /// <summary>transform.up 고정 (튕김 발판).</summary>
        VerticalUp = 1
    }

    [Header("방향 모드")]
    [Tooltip("HorizontalFromCenter: 중심 → 플레이어 수평 넉백 (벽·범퍼)\n" +
             "VerticalUp: transform.up으로 고정 발사 (튕김 발판).\n" +
             "수직 발사는 서 있던 위치와 무관하게 항상 같은 각도로 올라간다 —\n" +
             "중심 기준 방향을 쓰면 플레이어 피벗이 발밑이라 발판에서 조금만 벗어나도\n" +
             "수평 성분이 압도해 거의 옆으로 날아간다.")]
    [SerializeField] LaunchMode launchMode = LaunchMode.HorizontalFromCenter;

    [Header("넉백 (세기만 랜덤)")]
    [Tooltip("넉백 힘 최소값")]
    [SerializeField] float knockbackForceMin = 5f;

    [Tooltip("넉백 힘 최대값")]
    [SerializeField] float knockbackForceMax = 10f;

    [Tooltip("닿아있는 동안 이 간격마다 다시 튕김(초).\n" +
             "너무 작으면 PunchHit SFX가 연타로 나감 (권장: 0.2 이상).\n" +
             "AddForce는 물리 스텝에 걸쳐 분리되므로 겹쳐있는 동안의 반복 적용(힘 누적)과,\n" +
             "Player 태그 콜라이더가 여러 개(루트 캡슐 + PunchHitBox)인 데서 오는\n" +
             "같은 프레임 중복 감지도 이 값으로 걸러진다.")]
    [SerializeField] float knockbackInterval = 0.25f;

    [Header("설정")]
    [Tooltip("true: 이 컴포넌트가 활성화된 동안만 넉백 적용\nDeactivate()로 비활성화 가능")]
    [SerializeField] bool isActive = true;

    // 플레이어별로 다음 튕김 시각. 한 명이 타이머를 잡아먹으면 다른 명이 안 튕기는 것을 막는다.
    readonly Dictionary<int, float> _nextKnockbackTime = new Dictionary<int, float>();

    // ── 외부 호출 ────────────────────────────────────────────────

    public void Activate()   => isActive = true;
    public void Deactivate() => isActive = false;

    // ── 충돌 감지 ────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        TryKnockback(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryKnockback(other);
    }

    // Trigger가 아닌 일반 Collider로 사용할 때도 감지
    void OnCollisionEnter(Collision collision)
    {
        TryKnockback(collision.collider);
    }

    void OnCollisionStay(Collision collision)
    {
        TryKnockback(collision.collider);
    }

    // ── 내부 ────────────────────────────────────────────────────

    void TryKnockback(Collider other)
    {
        if (!isActive) return;
        if (!other.CompareTag("Player")) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        Player p = other.GetComponent<Player>()
                   ?? other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;

        int id = p.GetInstanceID();
        if (_nextKnockbackTime.TryGetValue(id, out float next) && Time.time < next)
            return;

        Vector3 dir = LaunchDirection(p.transform.position);

        float force = Random.Range(knockbackForceMin, knockbackForceMax);
        NetworkDamageUtil.ApplyKnockback(p, dir, force);
        p.GetComponent<NetworkPlayerSetup>()?.NotifyPunchHitFromServer();

        _nextKnockbackTime[id] = Time.time + Mathf.Max(knockbackInterval, 0.05f);
    }

    /// <summary>launchMode에 따른 넉백 방향(정규화). VerticalUp은 플레이어 위치를 쓰지 않는다.</summary>
    Vector3 LaunchDirection(Vector3 playerPosition)
    {
        if (launchMode == LaunchMode.VerticalUp)
            return transform.up.normalized;

        Vector3 dir = playerPosition - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        return dir.normalized;
    }

    // ── 에디터 ──────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        // 수직 모드는 방향이 고정이라 그대로 표시. 수평 모드는 플레이어 위치에 따라 달라지므로
        // 대표로 transform.forward 기준 방향만 표시한다.
        Vector3 dir = launchMode == LaunchMode.VerticalUp
            ? transform.up.normalized
            : LaunchDirection(transform.position + transform.forward);

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Vector3 from = transform.position;
        Vector3 to   = from + dir * 3f;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(to, 0.2f);
    }
}
