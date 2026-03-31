using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 페이즈 전환 연출 컨트롤러 (라운드 기반).
///
/// [동작 흐름]
///  ColorTileChallenge 완료 (성공/실패) → PlayTransition()
///    → 플레이어 이동 정지
///    → trapsToActivate 경고 깜빡임
///    → trapsToDeactivate 비활성화 (이전 라운드 함정 제거)
///    → trapsToActivate 활성화 (이번 라운드 함정 생성)
///    → 플레이어 이동 재개
///
/// [씬 초기 상태]
///  - 기존 ground는 항상 고정
///  - 모든 ContactDamage: isActive = false
///
/// [사용법]
///  1. rounds 배열에 라운드 수만큼 항목 추가
///  2. 각 라운드에 제거할 함정 / 추가할 함정 등록
///  3. ColorTileChallenge.OnSuccess + OnFail → PlayTransition() 연결
/// </summary>
public class StageTransition : MonoBehaviour
{
    // ── 라운드 데이터 ────────────────────────────────────────────

    [System.Serializable]
    public class TrapRound
    {
        [Tooltip("Inspector 표시용 라운드 이름")]
        public string roundName = "Round";

        [Tooltip("이번 라운드 전환 시 비활성화할 함정.\n" +
                 "이전 라운드에서 켜진 함정을 꺼서 길을 되돌릴 때 사용.\n" +
                 "필요 없으면 비워두면 됨.")]
        public ContactDamage[] trapsToDeactivate = new ContactDamage[0];

        [Tooltip("이번 라운드 전환 시 새로 활성화할 함정.\n" +
                 "경고 깜빡임 후 Activate() 호출됨.")]
        public ContactDamage[] trapsToActivate = new ContactDamage[0];
    }

    [Header("라운드 설정")]
    [Tooltip("순서대로 진행할 라운드 목록.\n" +
             "ColorTileChallenge 1회 완료마다 다음 라운드로 넘어감.")]
    [SerializeField] TrapRound[] rounds = new TrapRound[0];

    [Header("연출 타이밍")]
    [Tooltip("경고 깜빡임 총 시간(초). 예: 2")]
    [SerializeField] float warningDuration = 0f;

    [Tooltip("깜빡임 ON/OFF 간격(초). 작을수록 빠르게 깜빡임. 예: 0.2")]
    [SerializeField] float blinkInterval = 0f;

    [Tooltip("함정 변경 후 플레이어 이동 재개까지 추가 대기(초). 예: 0.5")]
    [SerializeField] float resumeDelay = 0f;

    [Header("이벤트")]
    [Tooltip("전환 시작 시 호출 (UI 연출, 사운드 등)")]
    public UnityEvent OnTransitionStart;

    [Tooltip("함정 변경 직후 호출")]
    public UnityEvent OnTrapsChanged;

    [Tooltip("전환 완료 및 플레이어 이동 재개 후 호출")]
    public UnityEvent OnTransitionComplete;

    [Tooltip("모든 라운드가 끝났을 때 호출 (스테이지 클리어 연결 등)")]
    public UnityEvent OnAllRoundsComplete;

    int  _currentRound = 0;
    bool _isPlaying    = false;

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>
    /// 현재 라운드 전환 연출 시작.
    /// ColorTileChallenge.OnSuccess / OnFail 모두 이 메서드에 연결.
    /// 이미 재생 중이면 무시. 모든 라운드 완료 후에는 OnAllRoundsComplete만 호출.
    /// </summary>
    public void PlayTransition()
    {
        Debug.Log($"[StageTransition] PlayTransition 호출됨. _isPlaying={_isPlaying}, _currentRound={_currentRound}/{rounds.Length}");

        if (_isPlaying)
        {
            Debug.LogWarning("[StageTransition] 이미 전환 중 — 무시됨");
            return;
        }

        if (_currentRound >= rounds.Length)
        {
            Debug.Log("[StageTransition] 모든 라운드 완료");
            OnAllRoundsComplete?.Invoke();
            return;
        }


        StartCoroutine(TransitionRoutine(rounds[_currentRound]));
        _currentRound++;
    }

    /// <summary>라운드를 처음으로 리셋.</summary>
    public void ResetRounds() => _currentRound = 0;

    /// <summary>현재 라운드 인덱스 (0부터 시작)</summary>
    public int CurrentRound => _currentRound;

    /// <summary>진행 중인 연출을 즉시 중단하고 플레이어 이동 재개.</summary>
    public void ForceStop()
    {
        StopAllCoroutines();

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        SetPlayerFreeze(players, false);

        _isPlaying = false;
    }

    // ── 루틴 ────────────────────────────────────────────────────

    IEnumerator TransitionRoutine(TrapRound round)
    {
        _isPlaying = true;
        OnTransitionStart?.Invoke();

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        // 1. 플레이어 이동 정지
        SetPlayerFreeze(players, true);

        // 2. 새로 생길 함정: GameObject 먼저 켜되 렌더러는 끔 (깜빡임 준비)
        for (int i = 0; i < round.trapsToActivate.Length; i++)
        {
            if (round.trapsToActivate[i] == null) continue;
            round.trapsToActivate[i].gameObject.SetActive(true);
            SetRenderers(round.trapsToActivate[i], false);
        }

        // 3. 경고 깜빡임 (GameObject 켜진 상태에서 렌더러만 toggle)
        if (warningDuration > 0f && blinkInterval > 0f && round.trapsToActivate.Length > 0)
            yield return BlinkRoutine(round.trapsToActivate, warningDuration, blinkInterval);

        // 4. 이전 라운드 함정 끄기 + GameObject 비활성화
        for (int i = 0; i < round.trapsToDeactivate.Length; i++)
        {
            if (round.trapsToDeactivate[i] == null) continue;
            round.trapsToDeactivate[i].Deactivate();
            round.trapsToDeactivate[i].gameObject.SetActive(false);
        }

        // 5. 이번 라운드 함정 렌더러 표시 + Activate
        for (int i = 0; i < round.trapsToActivate.Length; i++)
        {
            if (round.trapsToActivate[i] == null) continue;
            SetRenderers(round.trapsToActivate[i], true);
            round.trapsToActivate[i].Activate();
        }

        OnTrapsChanged?.Invoke();

        // 6. 짧은 대기 후 이동 재개
        if (resumeDelay > 0f)
            yield return new WaitForSeconds(resumeDelay);

        SetPlayerFreeze(players, false);
        OnTransitionComplete?.Invoke();

        _isPlaying = false;
    }

    IEnumerator BlinkRoutine(ContactDamage[] traps, float totalDuration, float interval)
    {
        float elapsed = 0f;
        bool  visible = false;

        while (elapsed < totalDuration)
        {
            visible = !visible;
            for (int i = 0; i < traps.Length; i++)
                if (traps[i] != null)
                    SetRenderers(traps[i], visible);

            float wait = Mathf.Min(interval, totalDuration - elapsed);
            yield return new WaitForSeconds(wait);
            elapsed += interval;
        }

        // 깜빡임 끝 → 렌더러 끔 (5번 단계에서 다시 켜짐)
        for (int i = 0; i < traps.Length; i++)
            if (traps[i] != null)
                SetRenderers(traps[i], false);
    }

    void SetRenderers(ContactDamage trap, bool visible)
    {
        Renderer[] renderers = trap.GetComponentsInChildren<Renderer>(true);
        for (int j = 0; j < renderers.Length; j++)
            renderers[j].enabled = visible;
    }

    void SetPlayerFreeze(Player[] players, bool freeze)
    {
        for (int i = 0; i < players.Length; i++)
            if (players[i] != null)
                players[i].moveSpeedMultiplier = freeze ? 0f : 1f;
    }

    // ── 에디터 ──────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 전환 연출 재생 (다음 라운드)")]
    void Debug_Play() => PlayTransition();

    [ContextMenu("테스트: 강제 중단")]
    void Debug_Stop() => ForceStop();

    [ContextMenu("테스트: 라운드 리셋")]
    void Debug_Reset() => ResetRounds();
#endif
}
