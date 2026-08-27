using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T키로 이모트 메뉴를 열 수 있다는 안내 힌트를 HUD에 상시 표시.
///
/// 채팅/치어네임 패널이 열려있거나 이모트 메뉴 자신이 열려있을 때, 로컬 플레이어가
/// 죽었을 때는 숨긴다 — 어차피 그 상태에선 T가 작동하지 않는다
/// (PlayerEmoteMenuUI.Update()의 게이팅 조건과 동일).
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

        bool shouldShow = !InGameChatUI.IsChatOpen
            && !TutorialCheerNameUI.IsOpen
            && !PlayerEmoteMenuUI.IsOpen
            && (_player == null || !_player.IsDead);

        if (hintLabel.activeSelf != shouldShow) hintLabel.SetActive(shouldShow);
    }

    /// <summary>오프라인: isOwnerControlled=true, 온라인: NetworkObject.IsOwner 기준으로 탐색.</summary>
    static Player FindLocalOwnerPlayer()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner) return p;
            if (p.isOwnerControlled) return p;
        }
        return null;
    }
}
