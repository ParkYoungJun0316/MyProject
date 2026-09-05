using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T키로 이모트 메뉴를 열 수 있다는 안내 힌트를 HUD에 상시 표시.
///
/// 마우스를 쓰는 UI(채팅·치어네임·ESC 메뉴·이모트 휠 자신)가 하나라도 떠 있거나 로컬
/// 플레이어가 죽었을 때는 숨긴다 — 어차피 그 상태에선 T가 작동하지 않는다
/// (PlayerEmoteMenuUI.Update()의 열기 게이트와 같은 기준을 본다).
///
/// [배치]
/// UI.prefab(로컬 HUD)의 항상 활성 상태인 빈 GameObject에 부착 — hintLabel 자체를
/// 이 컴포넌트가 붙은 GameObject로 쓰면 숨겨진 뒤 다시 보여줄 조건을 검사할 Update()가
/// 멈추므로, hintLabel은 반드시 별도의 자식 GameObject로 연결할 것.
/// hintLabel: "T: 이모트" 텍스트/아이콘 GameObject 연결.
/// </summary>
public class EmoteHintUI : MonoBehaviour
{
    [Header("힌트")]
    [Tooltip("항상 표시할 안내 힌트 GameObject(텍스트/아이콘). " +
             "이 컴포넌트가 붙은 GameObject와는 별도의 자식으로 연결할 것(자기 자신 X).")]
    [SerializeField] GameObject hintLabel;

    Player _player;

    void Start()
    {
        _player = FindLocalOwnerPlayer();
        if (_player != null) return;

        PlayerSpawnCoordinator.OnPlayersReady += FindAndInit;
        if (PlayerSpawnCoordinator.IsReady) FindAndInit();
    }

    void FindAndInit()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= FindAndInit;
        _player = FindLocalOwnerPlayer();
    }

    void OnDestroy() => PlayerSpawnCoordinator.OnPlayersReady -= FindAndInit;

    void Update()
    {
        if (hintLabel == null) return;

        // 커서 해제 요청 목록이 "지금 마우스를 쓰는 UI가 있나"의 SSOT — 채팅·치어네임·ESC 메뉴·
        // 이모트 휠이 모두 여기 등록되므로 UI별 플래그를 하나씩 볼 필요가 없다.
        bool shouldShow = !CursorUnlockRequestUtil.IsRequested
            && (_player == null || !_player.IsDead);

        if (hintLabel.activeSelf != shouldShow) hintLabel.SetActive(shouldShow);
    }

    /// <summary>로컬 오너 플레이어 — NetworkObject.IsOwner 기준. 솔로도 Host 1인이라 같은 경로다.</summary>
    static Player FindLocalOwnerPlayer()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner) return p;
        }
        return null;
    }
}
