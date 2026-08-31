using System.Collections.Generic;
using Dissonance;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 패널 "팀 보이스" 탭 — CheerName(guma/danho/sook)이 아니라
/// 팀원의 Steam 표시 이름(GameSession 세션 이름)을 슬롯에 표시.
/// 수신 볼륨 슬라이더는 GameSession 세션 VoiceId(Dissonance LocalPlayerName self-report,
/// SoundAndSettingsDesign.md §6-8)로 DissonanceComms.FindPlayer(voiceId)를 조회해
/// VoicePlayerState.Volume에 실제 반영한다.
///
/// 구 로비(1.Lobby, LobbyNetworkManager) 슬롯 폴백은 로비 씬 삭제로 제거됨(NetworkDesign.md §6B.7 P8,
/// 2026-08-20) — 이제 GameSession 세션 데이터 하나만 본다. DisplayName/VoiceId 세션 확정은
/// TutorialNetworkManager.CompleteGate()(게이트 통과 시점)에만 이뤄지므로(§6B.7 P3/P8, PlayerDisplayNameSync),
/// 그 전(Tutorial 사전 게이트 통과 전)에는 팀원 목록이 비어 보일 수 있음 — 의도된 상태, 버그 아님.
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

    DissonanceComms _subscribedComms;

    void OnEnable()
    {
        SubscribeVoiceSession();
        Refresh();
    }

    void OnDisable() => UnsubscribeVoiceSession();

    void SubscribeVoiceSession()
    {
        UnsubscribeVoiceSession();
        DissonanceComms comms = DissonanceComms.GetSingleton();
        if (comms == null) return;
        comms.OnPlayerJoinedSession += OnVoiceSessionChanged;
        comms.OnPlayerLeftSession += OnVoiceSessionChanged;
        _subscribedComms = comms;
    }

    void UnsubscribeVoiceSession()
    {
        if (_subscribedComms == null) return;
        _subscribedComms.OnPlayerJoinedSession -= OnVoiceSessionChanged;
        _subscribedComms.OnPlayerLeftSession -= OnVoiceSessionChanged;
        _subscribedComms = null;
    }

    void OnVoiceSessionChanged(VoicePlayerState _) => Refresh();

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

        bool canControl = player != null && !player.IsLocalPlayer && player.IsConnected;
        slider.interactable = canControl;
        slider.SetValueWithoutNotify(canControl ? player.Volume : 1f);
        slider.GetComponent<SliderValuePercentLabel>()?.RefreshNow();

        if (!canControl) return;

        slider.onValueChanged.AddListener(value =>
        {
            VoicePlayerState live = DissonanceComms.GetSingleton()?.FindPlayer(voiceId);
            if (live != null && live.IsConnected) live.Volume = value;
        });
    }

    static List<Teammate> CollectTeammates()
    {
        var teammates = new List<Teammate>(3);
        TryCollectFromGameSession(teammates);
        return teammates;
    }

    static void TryCollectFromGameSession(List<Teammate> teammates)
    {
        GameSession session = GameSession.Instance;
        if (session == null || session.ActivePlayerCount <= 0) return;

        int localColor = ResolveLocalColorIndex();
        IReadOnlyList<PlayerColorType> colors = session.GetActiveColors();
        if (colors == null || colors.Count == 0) return;

        for (int i = 0; i < colors.Count; i++)
        {
            int ci = PlayerColorUtil.ColorTypeToIndex(colors[i]);
            if (ci < 0) continue;
            if (localColor >= 0 && ci == localColor) continue;

            string name = session.GetSessionDisplayName(ci);
            if (string.IsNullOrEmpty(name) || name == "Player") continue;
            teammates.Add(new Teammate { Name = name, VoiceId = session.GetSessionVoiceId(ci) });
            if (teammates.Count >= 3) break;
        }
    }

    static int ResolveLocalColorIndex()
    {
        if (NetworkManager.Singleton == null) return -1;
        if (!PlayerSpawnCoordinator.TryGetColor(NetworkManager.Singleton.LocalClientId, out PlayerColorType color))
            return -1;
        return PlayerColorUtil.ColorTypeToIndex(color);
    }
}
