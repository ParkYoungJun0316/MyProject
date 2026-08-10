using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 패널 "팀 보이스" 탭 — CheerName(guma/danho/sook)이 아니라
/// 팀원의 Steam 표시 이름(LobbyPlayerState.DisplayName / GameSession 세션 이름)을 슬롯에 표시.
/// 수신 볼륨 슬라이더는 UI만 두고, Dissonance 연동은 후속 작업.
/// </summary>
public class OptionsTeamVoicePanel : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public GameObject root;
        public TMP_Text nameLabel;
        public Slider volumeSlider;
    }

    [SerializeField] Row[] rows;
    [SerializeField] GameObject emptyState;

    void OnEnable() => Refresh();

    public void Refresh()
    {
        List<string> names = CollectTeammateDisplayNames();

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i].root == null) continue;
            bool show = i < names.Count;
            rows[i].root.SetActive(show);
            if (show && rows[i].nameLabel != null)
                rows[i].nameLabel.text = names[i];
        }

        if (emptyState != null)
            emptyState.SetActive(names.Count == 0);
    }

    static List<string> CollectTeammateDisplayNames()
    {
        var names = new List<string>(3);

        // 1) 인게임: GameSession 세션 표시 이름 (로비에서 확정된 Steam 표시 이름)
        if (TryCollectFromGameSession(names))
            return names;

        // 2) 로비: LobbyNetworkManager 슬롯
        if (TryCollectFromLobby(names))
            return names;

        return names;
    }

    static bool TryCollectFromGameSession(List<string> names)
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.ActivePlayerCount <= 0) return false;

        int localColor = ResolveLocalColorIndex();
        IReadOnlyList<PlayerColorType> colors = session.GetActiveColors();
        if (colors == null || colors.Count == 0) return false;

        for (int i = 0; i < colors.Count; i++)
        {
            int ci = LobbyNetworkManager.ColorTypeToIndex(colors[i]);
            if (ci < 0) continue;
            if (localColor >= 0 && ci == localColor) continue;

            string name = session.GetSessionDisplayName(ci);
            if (string.IsNullOrEmpty(name) || name == "Player") continue;
            names.Add(name);
            if (names.Count >= 3) break;
        }

        // ActiveColors는 있는데 이름이 전부 비어 있어도 "세션 경로로 처리됨"으로 간주
        // (솔로 1인이면 팀원 0명 → emptyState 표시가 맞음)
        return true;
    }

    static bool TryCollectFromLobby(List<string> names)
    {
        LobbyNetworkManager lobby = LobbyNetworkManager.Instance;
        if (lobby == null || lobby.SlotCount <= 0) return false;

        ulong localId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        for (int i = 0; i < lobby.SlotCount; i++)
        {
            LobbyPlayerState slot = lobby.GetSlot(i);
            if (slot.ClientId == localId) continue;

            string name = slot.DisplayName.ToString();
            if (string.IsNullOrEmpty(name)) continue;
            names.Add(name);
            if (names.Count >= 3) break;
        }

        return true;
    }

    static int ResolveLocalColorIndex()
    {
        if (NetworkManager.Singleton == null) return -1;
        if (!PlayerSpawnCoordinator.TryGetColor(NetworkManager.Singleton.LocalClientId, out PlayerColorType color))
            return -1;
        return LobbyNetworkManager.ColorTypeToIndex(color);
    }
}
