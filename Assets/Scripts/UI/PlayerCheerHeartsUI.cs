using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 캐릭터 머리 위에 "팀 응원 창이 열려 있는 동안, 이 플레이어가 이번 창에서 아직 통과 못 했는지"를
/// 느낌표 1개로 표시. Player 프리팹에 부착.
/// [2026-09-14 3차 변경] 회색/초록 2색 구 + 타임아웃 표 리셋 방식 폐기 — 통과하면 표시가 꺼진다.
/// [2026-09-14] 메시/색/크기를 코드가 만들지 않는다. CheerExclamation 프리팹을 Instantiate하고
/// offset만 적용한다. 스케일·색은 프리팹 값을 그대로 쓴다.
///
/// [뜨는 시점]
/// CheerService.OnHazardWindowChanged(true) — 성공 스탬프(TeamCheerCleared)가 아니라 경고 창 시작 시점.
/// OnTeamVoteChanged의 voterColorIndices에 이 캐릭터 colorIndex가 있으면(=통과) 표시 소거,
/// 없으면 계속 표시. 창이 열려 있는 동안 타임아웃으로 인한 재표시는 없다 — 한 번 통과하면
/// 그 창이 끝날 때까지(성공 또는 강제 종료) 계속 꺼진 채로 유지된다.
///
/// [네트워크 불필요 · 전원에게 보임]
/// 창 열림은 각 피어 함정이 로컬로 알리고, 표 명단은 Host ClientRpc로 온다. 둘의 도착 순서가
/// 피어마다 다를 수 있으므로 마지막 표 상태(_voted)를 기억해 창이 열릴 때 그 값으로 켠다.
///
/// [씬 설정]
/// 1. Player 프리팹 루트에 PlayerCheerHeartsUI.
/// 2. exclamationPrefab : Assets/Prefab/CheerExclamation.prefab.
/// 3. offset : 머리 위 위치 (예: 0, 2.6, 0).
/// </summary>
[RequireComponent(typeof(Player))]
public class PlayerCheerHeartsUI : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("머리 위 표시 위치 오프셋 (이름표보다 위쪽 권장)")]
    [SerializeField] Vector3 offset = new Vector3(0f, 2.6f, 0f);
    [Tooltip("Assets/Prefab/CheerExclamation. 스케일·색은 프리팹 그대로.")]
    [SerializeField] GameObject exclamationPrefab;

    Player     _player;
    int        _myColorIndex = -1;
    bool       _voted;
    GameObject _markGo;
    Coroutine  _waitSubscribe;

    void Awake()
    {
        _player = GetComponent<Player>();
    }

    void Start()
    {
        BuildMark();

        PlayerSpawnCoordinator.OnPlayersReady += HandlePlayersReady;
        HandlePlayersReady();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= HandlePlayersReady;
        UnsubscribeCheerService();
    }

    // ── 준비 시점 ────────────────────────────────────────────────

    void HandlePlayersReady()
    {
        _myColorIndex = ResolveColorIndex();
        TrySubscribeCheerService();
    }

    /// <summary>ColorOrder 인덱스(0=berry …). TeamStatusUI.ResolveColorIndex와 동일 로직.</summary>
    int ResolveColorIndex()
    {
        var net = GetComponent<NetworkObject>();
        if (net != null && PlayerSpawnCoordinator.TryGetColor(net.OwnerClientId, out var sessionColor))
            return System.Array.IndexOf(PlayerColorUtil.ColorOrder, sessionColor);
        return System.Array.IndexOf(PlayerColorUtil.ColorOrder, _player.playerColorType);
    }

    // ── CheerService 구독 ─────────────────────────────────────────

    void TrySubscribeCheerService()
    {
        if (CheerService.Instance != null)
        {
            SubscribeCheerService();
            return;
        }
        if (!isActiveAndEnabled || _waitSubscribe != null) return;
        _waitSubscribe = StartCoroutine(WaitAndSubscribe());
    }

    IEnumerator WaitAndSubscribe()
    {
        while (CheerService.Instance == null)
            yield return null;
        _waitSubscribe = null;
        SubscribeCheerService();
    }

    void SubscribeCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;

        svc.OnHazardWindowChanged -= HandleHazardWindowChanged;
        svc.OnHazardWindowChanged += HandleHazardWindowChanged;
        svc.OnTeamVoteChanged -= HandleTeamVoteChanged;
        svc.OnTeamVoteChanged += HandleTeamVoteChanged;

        HandleHazardWindowChanged(svc.IsHazardWindowActive);
    }

    void UnsubscribeCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnHazardWindowChanged -= HandleHazardWindowChanged;
        svc.OnTeamVoteChanged -= HandleTeamVoteChanged;
    }

    void HandleHazardWindowChanged(bool active)
    {
        ApplyVisibility(active);
    }

    void HandleTeamVoteChanged(int current, int required, int[] voterColorIndices)
    {
        if (_myColorIndex < 0) _myColorIndex = ResolveColorIndex();
        _voted = _myColorIndex >= 0
            && voterColorIndices != null
            && System.Array.IndexOf(voterColorIndices, _myColorIndex) >= 0;

        var svc = CheerService.Instance;
        ApplyVisibility(svc != null && svc.IsHazardWindowActive);
    }

    // ── 표시 갱신 ────────────────────────────────────────────────

    /// <summary>창이 열려 있고 아직 통과 못 했을 때만 보인다 — 통과하면(1회) 그 창이 끝날 때까지 소거.</summary>
    void ApplyVisibility(bool windowActive)
    {
        if (_markGo == null) return;
        _markGo.SetActive(windowActive && !_voted);
    }

    // ── 표시 생성 ────────────────────────────────────────────────

    void BuildMark()
    {
        if (exclamationPrefab == null)
        {
            Debug.LogWarning("[PlayerCheerHeartsUI] exclamationPrefab 미연결.", this);
            return;
        }

        _markGo = Instantiate(exclamationPrefab, transform, false);
        _markGo.name = "CheerStatusMark";
        _markGo.transform.localPosition = offset;
        _markGo.SetActive(false);
    }
}
