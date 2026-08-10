using System.Collections.Generic;
using Dissonance;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 패널 "팀 보이스" 탭 — CheerName(guma/danho/sook)이 아니라
/// 팀원의 Steam 표시 이름(LobbyPlayerState.DisplayName / GameSession 세션 이름)을 슬롯에 표시.
/// 수신 볼륨 슬라이더는 LobbyPlayerState.VoiceId(Dissonance LocalPlayerName self-report,
/// SoundAndSettingsDesign.md §6-8)로 DissonanceComms.FindPlayer(voiceId)를 조회해
/// VoicePlayerState.Volume에 실제 반영한다.
/// </summary>
public class OptionsTeamVoicePanel : MonoBehaviour
{
    struct Teammate
    {
        public string Name;
        public string VoiceId; // 빈 문자열/null = Dissonance 매칭 불가(볼륨 슬라이더 비활성)
    }

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
        List<Teammate> teammates = CollectTeammates();

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i].root == null) continue;
            bool show = i < teammates.Count;
            rows[i].root.SetActive(show);
            if (!show) continue;

            Teammate mate = teammates[i];
            if (rows[i].nameLabel != null)
                rows[i].nameLabel.text = mate.Name;

            BindVolumeSlider(rows[i].volumeSlider, mate.VoiceId);
        }

        if (emptyState != null)
            emptyState.SetActive(teammates.Count == 0);
    }

    /// <summary>
    /// 슬라이더를 해당 팀원의 Dissonance VoicePlayerState.Volume에 연결.
    /// VoiceId 매칭 실패(아직 미보고/미접속)면 비활성화하고 100%로 표시.
    /// </summary>
    static void BindVolumeSlider(Slider slider, string voiceId)
    {
        if (slider == null) return;

        slider.onValueChanged.RemoveAllListeners();

        VoicePlayerState player = string.IsNullOrEmpty(voiceId)
            ? null
            : DissonanceComms.GetSingleton()?.FindPlayer(voiceId);

        slider.interactable = player != null;
        slider.SetValueWithoutNotify(player?.Volume ?? 1f);

        if (player == null) return;

        slider.onValueChanged.AddListener(value =>
        {
            VoicePlayerState live = DissonanceComms.GetSingleton()?.FindPlayer(voiceId);
            if (live != null) live.Volume = value;
        });
    }

    static List<Teammate> CollectTeammates()
    {
        var teammates = new List<Teammate>(3);

        // 1) 인게임: GameSession 세션 데이터 (로비에서 확정된 Steam 표시 이름 + VoiceId)
        if (TryCollectFromGameSession(teammates))
            return teammates;

        // 2) 로비: LobbyNetworkManager 슬롯
        if (TryCollectFromLobby(teammates))
            return teammates;

        return teammates;
    }

    static bool TryCollectFromGameSession(List<Teammate> teammates)
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
            teammates.Add(new Teammate { Name = name, VoiceId = session.GetSessionVoiceId(ci) });
            if (teammates.Count >= 3) break;
        }

        // ActiveColors는 있는데 이름이 전부 비어 있어도 "세션 경로로 처리됨"으로 간주
        // (솔로 1인이면 팀원 0명 → emptyState 표시가 맞음)
        return true;
    }

    static bool TryCollectFromLobby(List<Teammate> teammates)
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
            teammates.Add(new Teammate { Name = name, VoiceId = slot.VoiceId.ToString() });
            if (teammates.Count >= 3) break;
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
