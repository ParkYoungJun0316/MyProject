using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 플레이 중 이탈 감지 + 호스트 결정 관리.
///
/// [동작]
/// 플레이어 이탈 감지 → Time.timeScale = 0 → UI 표시
///   [호스트 전용]
///   - 「계속 진행」: 남은 인원으로 씬 리로드 + GameSession 인원 재파악
///   - 「방 종료」  : Shutdown → 타이틀 복귀
///   [비호스트 클라이언트]
///   - 호스트 이탈 = 방 종료 → 타이틀 복귀
///
/// [배치]
/// M.Stage1 / T.Stage1 씬의 NetworkObject에 부착 (NetworkObject + DisconnectManager).
/// 또는 DontDestroyOnLoad 오브젝트에 부착 가능.
///
/// [Inspector 연결]
/// - disconnectPanel  : 이탈 UI 패널
/// - statusText       : "X번 플레이어가 이탈했습니다." 등 메시지
/// - continueButton   : 「계속 진행」(Host 전용)
/// - quitButton       : 「방 종료」(Host 전용)
/// - countdownText    : 남은 유예 시간 표시 (선택)
/// - gracePeriodSec   : 자동 계속 진행까지 대기 시간(초) (기본 60)
/// </summary>
public class DisconnectManager : NetworkBehaviour
{
    public static DisconnectManager Instance { get; private set; }

    [Header("UI")]
    [Tooltip("이탈 발생 시 표시할 패널. 비워두면 UI 없이 동작.")]
    [SerializeField] private GameObject disconnectPanel;

    [Tooltip("이탈 플레이어 정보 텍스트.")]
    [SerializeField] private TMP_Text   statusText;

    [Tooltip("「계속 진행」버튼. Host만 보임.")]
    [SerializeField] private Button     continueButton;

    [Tooltip("「방 종료」버튼. Host만 보임.")]
    [SerializeField] private Button     quitButton;

    [Tooltip("유예 시간 카운트다운 텍스트 (선택).")]
    [SerializeField] private TMP_Text   countdownText;

    [Header("설정")]
    [Tooltip("자동 계속 진행까지 대기(초). 이 시간 내 호스트 결정 없으면 자동 씬 리로드.")]
    [SerializeField] private float gracePeriodSec = 60f;

    [Tooltip("복귀할 타이틀 씬 이름.")]
    [SerializeField] private string titleSceneName = "0.Title";

    private bool      _disconnectPending;
    private Coroutine _graceCoroutine;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        NetworkManager.OnClientDisconnectCallback += OnClientLeft;

        // Host 전용 버튼 표시
        bool isHost = IsHost;
        if (continueButton != null) continueButton.gameObject.SetActive(isHost);
        if (quitButton      != null) quitButton.gameObject.SetActive(isHost);

        if (disconnectPanel != null) disconnectPanel.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
        if (Instance == this) Instance = null;
    }

    // ── 이탈 감지 ─────────────────────────────────────────────────

    void OnClientLeft(ulong clientId)
    {
        // 자신의 연결이 끊어진 경우 (킥/호스트 이탈)
        bool isSelf = clientId == NetworkManager.LocalClientId
                   || !NetworkManager.IsListening;

        if (isSelf && !IsHost)
        {
            // 클라이언트: 호스트 이탈 or 킥 → 타이틀 복귀
            Debug.Log("[DisconnectManager] 연결 종료 — 타이틀 복귀");
            ReturnToTitle();
            return;
        }

        if (!IsHost) return; // 호스트가 아니면 아래 로직 불필요

        if (_disconnectPending) return;
        _disconnectPending = true;

        // 게임 일시 정지
        Time.timeScale = 0f;

        ShowDisconnectUI($"플레이어가 이탈했습니다.\n\n호스트: 계속 진행하거나 방을 종료하세요.");
        _graceCoroutine = StartCoroutine(GraceCountdown());
    }

    // ── 호스트 버튼 콜백 ──────────────────────────────────────────

    /// <summary>「계속 진행」버튼. 남은 인원으로 씬 리로드.</summary>
    public void OnClickContinue()
    {
        if (!IsHost) return;
        StopGrace();
        ContinueWithRemainingPlayers();
    }

    /// <summary>「방 종료」버튼. Shutdown 후 타이틀 복귀.</summary>
    public void OnClickQuitRoom()
    {
        if (!IsHost) return;
        StopGrace();
        ShutdownAndReturnClientRpc();
        ReturnToTitle();
    }

    // ── 내부 처리 ─────────────────────────────────────────────────

    IEnumerator GraceCountdown()
    {
        float remaining = gracePeriodSec;
        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.text = $"자동 계속 진행: {Mathf.CeilToInt(remaining)}초";

            yield return new WaitForSecondsRealtime(1f); // timeScale=0이므로 Realtime 사용
            remaining -= 1f;
        }

        // 유예 만료: 자동 계속 진행
        if (IsHost)
            ContinueWithRemainingPlayers();
    }

    void ContinueWithRemainingPlayers()
    {
        HideDisconnectUI();
        Time.timeScale = 1f;
        _disconnectPending = false;

        // 인원 변경 → 씬 리로드 + 새 시드
        int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        NetworkSessionData.Seed = newSeed;
        BroadcastReloadClientRpc(newSeed);

        string sceneName = SceneManager.GetActiveScene().name;
        NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    void StopGrace()
    {
        if (_graceCoroutine != null)
        {
            StopCoroutine(_graceCoroutine);
            _graceCoroutine = null;
        }
    }

    void ShowDisconnectUI(string message)
    {
        if (disconnectPanel != null) disconnectPanel.SetActive(true);
        if (statusText      != null) statusText.text = message;
    }

    void HideDisconnectUI()
    {
        if (disconnectPanel != null) disconnectPanel.SetActive(false);
    }

    void ReturnToTitle()
    {
        Time.timeScale = 1f;
        NetworkManagerSetup.Instance?.Shutdown();
        GameSession.Instance?.ResetSession();
        LobbyContext.Mode = LobbyMode.Offline;
        SceneManager.LoadScene(titleSceneName);
    }

    // ── ClientRpc ─────────────────────────────────────────────────

    /// <summary>호스트 「방 종료」 시 모든 클라이언트에 타이틀 복귀 알림.</summary>
    [ClientRpc]
    void ShutdownAndReturnClientRpc()
    {
        if (IsHost) return; // 호스트는 직접 처리
        ReturnToTitle();
    }

    /// <summary>계속 진행 시 클라이언트에 새 시드 배포.</summary>
    [ClientRpc]
    void BroadcastReloadClientRpc(int newSeed)
    {
        if (IsHost) return;
        NetworkSessionData.Seed = newSeed;
        Time.timeScale = 1f;
        HideDisconnectUI();
        _disconnectPending = false;
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 이탈 시뮬레이션")]
    void Debug_SimDisconnect() => OnClientLeft(999);

    [ContextMenu("테스트: UI 숨기기")]
    void Debug_HideUI() => HideDisconnectUI();
#endif
}
