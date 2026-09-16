using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 흑/백 토글 패드. 누구든(색 무관) 올라설 때마다 BlackWhiteDoorToggle 상태를 뒤집는다.
/// 올라선 채로는 재토글 없음 — 내려갔다가 다시 밟아야 한다.
///
/// [판정] Host 전용 (ContactKnockback과 동일 — Host가 원격 플레이어 CNT 위치로 트리거를 받는다).
///        루트 캡슐(Player 컴포넌트가 붙은 콜라이더)만 인정 — 자식 PunchHitBox는 무시.
/// [연출] 밟는 소리는 각 머신 로컬 3D 재생 (PressurePad와 동일).
///
/// [씬 설정] Collider(Is Trigger) + 이 스크립트. controller를 비우면 씬에서 자동 탐색.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BlackWhiteTogglePad : MonoBehaviour
{
    [SerializeField] BlackWhiteDoorToggle controller;

    [Header("사운드 (3D)")]
    [SerializeField] SFXId pressSfxId = SFXId.Pad_Press;
    [SerializeField] float pressMinDistance = 5f;
    [SerializeField] float pressMaxDistance = 20f;
    [SerializeField] AudioRolloffMode pressRolloffMode = AudioRolloffMode.Logarithmic;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (controller == null)
            controller = FindFirstObjectByType<BlackWhiteDoorToggle>(FindObjectsInactive.Include);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Player p = other.GetComponent<Player>();
        if (p == null || p.IsDead || p.IsDowned) return;

        if (pressSfxId != SFXId.None)
            SFXManager.Instance?.PlayAtPoint(pressSfxId, transform.position, pressMinDistance, pressMaxDistance, pressRolloffMode);

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;
        if (controller != null) controller.RequestToggle();
    }
}
