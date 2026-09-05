using System.Collections.Generic;
using Dissonance;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 옵션 패널 "팀 보이스" 탭 — CheerName(guma/danho/sook)이 아니라
/// 팀원의 Steam 표시 이름(GameSession 세션 이름)을 슬롯에 표시.
/// 수신 볼륨 슬라이더는 GameSession 세션 VoiceId(Dissonance LocalPlayerName self-report,
/// SoundAndSettingsDesign.md §6-8)로 DissonanceComms.FindPlayer(voiceId)를 조회해
/// VoicePlayerState.Volume에 실제 반영한다.
///
/// 구 로비(1.Lobby, LobbyNetworkManager) 슬롯 폴백은 로비 씬 삭제로 제거됨(NetworkDesign.md §6B.7 P8,
/// 2026-08-20).
///
/// [게이트 전에도 동작함, 2026-09-05]
/// DisplayName/VoiceId 세션 확정은 TutorialNetworkManager.CompleteGate()에서만 이뤄지지만
/// (§6B.7 P3/P8), 그렇다고 Tutorial에서 팀 보이스 조절이 안 되는 건 사용자 입장에서 버그다.
/// 그래서 명단은 PlayerSpawnCoordinator(게이트 전에도 채워지는 SSOT)에서 뽑고, 이름/VoiceId는
/// TeamStatusUI와 같은 규칙(세션 확정값 → 실시간 NV 폴백)으로 해석한다.
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
    readonly Dictionary<Slider, UnityAction<float>> _volumeCallbacks = new();

    void OnEnable()
    {
        SubscribeVoiceSession();
        // DisplayName/VoiceId는 스폰 직후가 아니라 나중에 NV로 도착한다(VoiceId는 Dissonance
        // LocalPlayerName 확정을 최대 5초 기다림). 패널이 이미 열려 있는 동안 도착한 값을
        // 반영하려면 NV 변경 알림을 구독해야 한다 — 없으면 슬라이더가 계속 비활성으로 남는다.
        PlayerDisplayNameSync.OnAnyDisplayNameChanged += Refresh;
        PlayerDisplayNameSync.OnAnyVoiceIdChanged     += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        PlayerDisplayNameSync.OnAnyDisplayNameChanged -= Refresh;
        PlayerDisplayNameSync.OnAnyVoiceIdChanged     -= Refresh;
        UnsubscribeVoiceSession();
    }

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
        if (rows == null) return;

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
    /// RemoveAllListeners()를 쓰면 SliderValuePercentLabel의 onValueChanged 구독까지 같이
    /// 끊겨 퍼센트 표시가 값이 바뀌어도 고정되는 버그가 있어, 이 메서드가 직접 붙인 콜백만
    /// 골라 제거한다(SoundAndSettingsDesign.md 팀 보이스 % 고정 버그).
    /// </summary>
    void BindVolumeSlider(Slider slider, string voiceId)
    {
        if (slider == null) return;

        if (_volumeCallbacks.TryGetValue(slider, out UnityAction<float> previous))
            slider.onValueChanged.RemoveListener(previous);

        VoicePlayerState player = string.IsNullOrEmpty(voiceId)
            ? null
            : DissonanceComms.GetSingleton()?.FindPlayer(voiceId);

        bool canControl = player != null && !player.IsLocalPlayer && player.IsConnected;
        slider.interactable = canControl;
        slider.SetValueWithoutNotify(canControl ? player.Volume : 1f);
        slider.GetComponent<SliderValuePercentLabel>()?.RefreshNow();

        if (!canControl)
        {
            _volumeCallbacks.Remove(slider);
            return;
        }

        UnityAction<float> callback = value =>
        {
            VoicePlayerState live = DissonanceComms.GetSingleton()?.FindPlayer(voiceId);
            if (live != null && live.IsConnected) live.Volume = value;
        };
        _volumeCallbacks[slider] = callback;
        slider.onValueChanged.AddListener(callback);
    }

    /// <summary>
    /// 팀원 명단은 PlayerSpawnCoordinator(clientId→색 NetworkList) 하나만 순회한다 — Tutorial
    /// 게이트 전에도 채워져 있는 유일한 명단 SSOT.
    ///
    /// 예전엔 GameSession.ActivePlayerCount/GetActiveColors를 봤는데, GameSession은 씬 이름에
    /// "Stage"/"Boss"가 들어갈 때만 플레이어를 재수집하고(OnSceneLoaded) SetActiveColors도 게이트
    /// 통과 때만 호출되므로, Tutorial에서는 항상 0/빈 배열이라 팀 보이스 탭이 구조적으로 절대
    /// 뜰 수 없었다("Tutorial에서 team voice 조절 안 됨", 2026-09-05 수정).
    ///
    /// 이름/VoiceId 우선순위는 TeamStatusUI.GetPlayerDisplayName과 동일 —
    /// 세션 확정값 → 없으면 PlayerDisplayNameSync 실시간 NV.
    /// </summary>
    static List<Teammate> CollectTeammates()
    {
        var teammates = new List<Teammate>(3);

        // 자기 자신 제외는 색이 아니라 clientId로 — 색 매핑이 아직 없는 순간에도 정확하다.
        ulong localClientId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        foreach (var (clientId, color) in PlayerSpawnCoordinator.GetAllEntries())
        {
            if (clientId == localClientId) continue;

            int ci = PlayerColorUtil.ColorTypeToIndex(color);
            if (ci < 0) continue;

            string name = ResolveDisplayName(ci, clientId);
            if (string.IsNullOrEmpty(name)) continue;

            teammates.Add(new Teammate { Name = name, VoiceId = ResolveVoiceId(ci, clientId) });
            if (teammates.Count >= 3) break;
        }
        return teammates;
    }

    static string ResolveDisplayName(int colorIndex, ulong clientId)
    {
        string confirmed = GameSession.Instance?.GetSessionDisplayName(colorIndex);
        if (!string.IsNullOrEmpty(confirmed)) return confirmed;

        foreach (var (id, name) in PlayerDisplayNameSync.GetAllEffectiveNames())
            if (id == clientId && !string.IsNullOrEmpty(name)) return name;

        return null;
    }

    static string ResolveVoiceId(int colorIndex, ulong clientId)
    {
        string confirmed = GameSession.Instance?.GetSessionVoiceId(colorIndex);
        if (!string.IsNullOrEmpty(confirmed)) return confirmed;

        foreach (var (id, voiceId) in PlayerDisplayNameSync.GetAllEffectiveVoiceIds())
            if (id == clientId && !string.IsNullOrEmpty(voiceId)) return voiceId;

        return null;
    }
}
