using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 타일 챌린지 매니저 (4인용).
///
/// [축 SSOT: NetworkDesign.md §11B — 챌린지 축(C 패턴), OXQuizManager와 동일 골격 복제]
/// Trigger(스케줄, Host만) → RoundStart(Host가 시드 NV 배포) → Generate(전 머신 로컬 재생성)
/// → Judge(Host 레인만) → Resolve(OnSuccess/OnFail → §11A ③Progress로 반환).
/// StageNetworkState의 챌린지 공유 슬롯을 그대로 재사용한다 — 새 NetworkBehaviour/새 NV를
/// 만들지 않음(architecture.mdc: "Prefer extending existing systems").
///
/// [동작]
///  스케줄(activateAtSeconds)에 따라 Host만 자동 발동:
///   1. Host가 라운드 시드를 생성해 StageNetworkState로 배포
///   2. 전 머신이 동일 시드로 스폰 포인트를 셔플해 각 플레이어 고유색에 맞는 타일을 동일하게 생성
///   3. timeLimit 안에 4명 모두 자기 타일 위에 서 있는지는 Host만 판정
///   4. 결과(성공/실패)를 ClientRpc로 전파해 전 머신이 동일한 정리·이벤트를 재생
///
/// [스케줄 설정]
///  activateAtSeconds = [10, 30, 60] → 10초, 30초, 60초에 발동
///  loopSchedule = true + schedulePeriod = 90 → 90초 주기로 반복
///
/// [씬 설정]
///  1. 빈 오브젝트들을 타일이 등장할 바닥 위치에 배치 → spawnPoints 등록 (4개 이상)
///  2. tilePrefabs: Blue/Red/Green/Yellow 각 색상별 프리팹 4개 등록
/// </summary>
public class ColorTileChallenge : MonoBehaviour
{
    /// <summary>색상별 프리팹 매핑 항목.</summary>
    [System.Serializable]
    public class ColorTilePrefabEntry
    {
        [Tooltip("이 프리팹이 대응하는 플레이어 고유색")]
        public PlayerColorType colorType = PlayerColorType.Blue;

        [Tooltip("해당 색상의 ColorTile 프리팹 (머티리얼·시각 포함)")]
        public GameObject prefab;
    }

    [Tooltip("true: 씬/스테이지 시작 즉시 스케줄 자동 시작.\n" +
             "false: StageStartGate 등 외부에서 StartSchedule() 또는 Activate()를 직접 호출해야 함.")]
    [SerializeField] bool autoStart = true;

    [Header("타일 프리팹 (색상별 4개 등록)")]
    [Tooltip("Blue / Red / Green / Yellow 각각의 ColorTile 프리팹을 등록.\n" +
             "런타임에 플레이어 고유색을 감지해 해당 프리팹을 사용.")]
    [SerializeField] ColorTilePrefabEntry[] tilePrefabs = new ColorTilePrefabEntry[0];

    [Header("타일 생성")]
    [Tooltip("타일이 등장할 수 있는 위치 풀 (빈 오브젝트 Transform).\n4개 이상 준비 권장 (랜덤성)")]
    [SerializeField] Transform[] spawnPoints = new Transform[0];

    [Header("타이머")]
    [Tooltip("모든 플레이어가 타일에 올라서야 하는 제한 시간(초)")]
    [SerializeField] float timeLimit = 10f;

    [Header("발동 스케줄 (초 단위)")]
    [Tooltip("씬 시작 기준으로 이 초에 챌린지 발동. 예: [10, 30, 60]\n" +
             "비워두면 Activate()를 외부에서 직접 호출해야 함")]
    [SerializeField] float[] activateAtSeconds = new float[0];

    [Tooltip("스케줄 반복 여부")]
    [SerializeField] bool loopSchedule = false;

    [Tooltip("반복 시 한 사이클 길이(초). loopSchedule=true일 때만 사용")]
    [SerializeField] float schedulePeriod = 90f;

    [Header("실패 패널티 — 벽 영구 접근")]
    [Tooltip("챌린지 실패 시 영구적으로 접근할 AdvancingWall 목록.\n" +
             "North / South / West / East 벽 4개 등록 권장.")]
    [SerializeField] AdvancingWall[] penaltyWalls = new AdvancingWall[0];

    [Tooltip("실패 1회당 각 벽이 영구 접근하는 거리(m).\n" +
             "스케줄 이동과 별개로 적용됨.\n" +
             "ex) 스케줄 10 전진 + 패널티 5 → 총 15 전진")]
    [SerializeField] float penaltyAdvanceDistance = 5f;

    [Header("이벤트")]
    [Tooltip("챌린지 시작 시 호출")]
    public UnityEvent OnChallengeStarted;

    [Tooltip("시간 안에 모두 성공 시 호출")]
    public UnityEvent OnSuccess;

    [Tooltip("시간 초과 시 호출 (추가 연출 등 연결 가능)")]
    public UnityEvent OnFail;

    bool _isRunning;
    int  _failCount;

    readonly List<ColorTile> _activeTiles = new List<ColorTile>();
    Coroutine _scheduleCoroutine;
    Coroutine _judgeCoroutine;
    float     _scheduleStartTime;

    StageNetworkState _netState;

    /// <summary>
    /// activateAtSeconds에 등록된 총 스케줄 개수 = 총 라운드 수.
    /// ColorTileRoundObjective에서 읽어 UI 표시에 사용.
    /// </summary>
    public int ScheduledRoundCount => activateAtSeconds != null ? activateAtSeconds.Length : 0;

    /// <summary>실패 누적 횟수.</summary>
    public int FailCount => _failCount;

    // ── 생명주기 ─────────────────────────────────────────────────

    void Start()
    {
        // StageNetworkState.Awake()가 이 컴포넌트의 Start()보다 먼저 실행되는 것을
        // Unity 전역 Awake→Start 순서로 보장받음 (OXQuizManager와 동일 전제).
        _netState = StageNetworkState.Instance;
        if (_netState != null)
        {
            _netState.OnChallengeStepChanged += HandleChallengeStepChanged;
            _netState.OnChallengeOutcome     += HandleChallengeOutcome;
        }

        if (autoStart && !IsClientOnly() && activateAtSeconds != null && activateAtSeconds.Length > 0)
            StartSchedule();
    }

    void OnDestroy()
    {
        if (_netState != null)
        {
            _netState.OnChallengeStepChanged -= HandleChallengeStepChanged;
            _netState.OnChallengeOutcome     -= HandleChallengeOutcome;
        }
    }

    void OnDisable()
    {
        // 오브젝트 비활성화 시 남아있는 타일 강제 정리 (로컬 시각 정리 — 네트워크 불필요)
        ClearTiles();
    }

    /// <summary>Client/Host 공통. Host 레인 여부만 다르게 취급 (OXQuizManager와 동일).</summary>
    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    // ── 외부 호출 (Host 전용 — §11B ①Trigger) ─────────────────────

    /// <summary>
    /// 챌린지 즉시 시작. Host 레인만 실제로 진행 — Client의 직접 호출은 무시된다.
    /// 라운드 시드를 생성해 StageNetworkState로 배포하면, 전 머신이 HandleChallengeStepChanged에서
    /// 동일한 시드로 타일을 재생성한다 (§11B ②RoundStart).
    /// </summary>
    public void Activate()
    {
        if (IsClientOnly()) return;
        if (_isRunning) return;
        if (_netState == null) return;

        int seed = Random.Range(int.MinValue, int.MaxValue);
        _netState.ChallengeStart(seed);
        _netState.ChallengeStepBegin(0);
    }

    /// <summary>
    /// 스케줄 수동 시작 (Start에서 자동 호출됨).
    /// Host 레인만 스케줄 타이머를 돌린다 — Client는 이 코루틴을 아예 실행하지 않고
    /// NV/RPC로 전파되는 결과만 관찰한다 (스케줄=시간 기반 Trigger이므로 Host Update가 단일 소스).
    /// </summary>
    public void StartSchedule()
    {
        if (IsClientOnly()) return;
        if (_scheduleCoroutine != null) StopCoroutine(_scheduleCoroutine);
        _scheduleStartTime = Time.time;
        _scheduleCoroutine = StartCoroutine(ScheduleRoutine());
    }

    /// <summary>챌린지 강제 종료 + 타일 정리. Host 전용 (현재 외부 미연결 — 디버그용).</summary>
    public void Cancel()
    {
        if (IsClientOnly()) return;
        if (_judgeCoroutine != null) { StopCoroutine(_judgeCoroutine); _judgeCoroutine = null; }
        _isRunning = false;
        ClearTiles();
    }

    // ── 스케줄 (Host 전용) ───────────────────────────────────────

    IEnumerator ScheduleRoutine()
    {
        if (activateAtSeconds == null || activateAtSeconds.Length == 0) yield break;

        float cycleOffset = 0f;

        do
        {
            foreach (float t in activateAtSeconds)
            {
                float targetTime = _scheduleStartTime + cycleOffset + t;
                float waitTime   = targetTime - Time.time;

                if (waitTime > 0f)
                    yield return new WaitForSeconds(waitTime);

                Activate();

                // 챌린지가 끝날 때까지 대기 (겹치기 방지)
                while (_isRunning)
                    yield return null;
            }

            cycleOffset += schedulePeriod;

        } while (loopSchedule);
    }

    // ── 라운드 생성 (전 머신 공통 — StageNetworkState NV 구독, §11B ③Generate) ──

    /// <summary>
    /// StageNetworkState.OnChallengeStepChanged 구독 핸들러. Host/Client 동일 코드로 타일을 생성한다.
    /// 위치 배정은 ChallengeSeed 기반 Fisher-Yates라 전 머신이 항상 같은 결과를 낸다.
    /// 판정(JudgeRoutine)은 이 메서드 끝에서 Host만 이어서 시작한다 (§11B ④Judge).
    /// </summary>
    void HandleChallengeStepChanged(int stepIndex)
    {
        if (stepIndex < 0) return; // ChallengeStart()의 초기화 신호 — 무시

        // 1. 플레이어 수집 — playerColorType 순으로 결정적(GameSession.Apply() 정렬 보장, 전 머신 동일)
        var players = new List<Player>();
        if (GameSession.Instance != null)
        {
            foreach (Player p in GameSession.Instance.GetActivePlayers())
                if (p != null && !p.IsDead) players.Add(p);
        }
        else
        {
            foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
                if (!p.IsDead) players.Add(p);
        }

        if (players.Count == 0 || spawnPoints.Length == 0 || tilePrefabs.Length == 0)
            return;

        _isRunning = true;

        // 2. 스폰 포인트 셔플 — ChallengeSeed 기반 System.Random
        //    (UnityEngine.Random 전역 상태 오염 방지 — OXQuizManager.RegenerateQuestionOrder와 동일 원칙)
        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        var rng  = new System.Random(seed);

        int tileCount = Mathf.Min(players.Count, spawnPoints.Length);
        List<Transform> shuffled = new List<Transform>(spawnPoints);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // 3. 색상별 프리팹으로 타일 동시 생성 (전 머신 로컬 — 결과 자체는 네트워크로 안 보냄)
        ClearTiles();
        for (int i = 0; i < tileCount; i++)
        {
            PlayerColorType colorType = players[i].playerColorType;
            GameObject prefab = GetPrefabForColor(colorType);

            if (prefab == null)
            {
                Debug.LogWarning($"[ColorTileChallenge] {colorType} 색상 프리팹이 등록되지 않았습니다.");
                continue;
            }

            GameObject tileObj = Instantiate(prefab, shuffled[i].position, Quaternion.identity);
            ColorTile tile = tileObj.GetComponent<ColorTile>();
            if (tile == null) tile = tileObj.AddComponent<ColorTile>();
            tile.Setup(colorType);
            _activeTiles.Add(tile);
        }

        // 타일이 하나도 생성되지 않으면 챌린지 진행 불가
        if (_activeTiles.Count == 0)
        {
            Debug.LogWarning("[ColorTileChallenge] 타일이 생성되지 않았습니다. tilePrefabs 등록을 확인하세요.");
            _isRunning = false;
            return;
        }

        OnChallengeStarted?.Invoke();

        // 4. 판정은 Host 레인에서만 (§11B ④Judge) — Client는 결과를 ClientRpc로만 관찰
        if (IsClientOnly()) return;

        if (_judgeCoroutine != null) StopCoroutine(_judgeCoroutine);
        _judgeCoroutine = StartCoroutine(JudgeRoutine());
    }

    // ── 판정 (Host 전용, §11B ④Judge) ─────────────────────────────

    IEnumerator JudgeRoutine()
    {
        float remaining = timeLimit;

        while (remaining > 0f)
        {
            // 모든 타일이 완료됐는지 확인 (타일이 있을 때만 성공 가능)
            bool allDone = _activeTiles.Count > 0;
            foreach (ColorTile t in _activeTiles)
            {
                if (t == null || !t.IsCompleted) { allDone = false; break; }
            }

            if (allDone)
            {
                ResolveRound(true);
                yield break;
            }

            remaining -= Time.deltaTime;
            yield return null;
        }

        ResolveRound(false);
    }

    /// <summary>Host: 판정 확정 → 로컬 반영 + Client에 결과 전파 (§11B ⑤Resolve).</summary>
    void ResolveRound(bool success)
    {
        _judgeCoroutine = null;
        HandleChallengeOutcome(success);
        _netState?.NotifyChallengeOutcomeClientRpc(success);
    }

    // ── 결과 반영 (전 머신 공통 — Host는 직접 호출, Client는 ClientRpc로 수신) ──

    /// <summary>
    /// 라운드 결과 반영. Host는 ResolveRound에서 직접 호출하고, Client는
    /// StageNetworkState.OnChallengeOutcome 구독으로 동일 메서드를 수신한다 (OXQuizManager와 동일 패턴 —
    /// NotifyChallengeOutcomeClientRpc는 IsServer면 내부에서 스킵되므로 Host에서 이중 발동되지 않음).
    /// </summary>
    void HandleChallengeOutcome(bool success)
    {
        ClearTiles();
        _isRunning = false;

        if (success)
        {
            OnSuccess?.Invoke();
        }
        else
        {
            _failCount++;
            ApplyPenalty();
            OnFail?.Invoke();
        }
    }

    /// <summary>
    /// 실패 패널티: penaltyWalls 에 등록된 모든 벽을 penaltyAdvanceDistance 만큼 영구 전진.
    /// AdvancingWall은 네트워크 동기화가 없는 로컬 컴포넌트이므로, 전 머신이 HandleChallengeOutcome을
    /// 통해 동일한 고정값(penaltyAdvanceDistance, Inspector 직렬화값)으로 동시에 호출해야 위치가 일치한다.
    /// </summary>
    void ApplyPenalty()
    {
        if (penaltyWalls == null || penaltyWalls.Length == 0) return;
        if (penaltyAdvanceDistance <= 0f) return;

        foreach (AdvancingWall wall in penaltyWalls)
        {
            if (wall != null)
                wall.PermanentAdvance(penaltyAdvanceDistance);
        }

        Debug.Log($"[ColorTileChallenge] 실패 #{_failCount} — 패널티 적용: {penaltyWalls.Length}개 벽 {penaltyAdvanceDistance}m 영구 전진");
    }

    void ClearTiles()
    {
        foreach (ColorTile t in _activeTiles)
        {
            if (t == null) continue;
            t.gameObject.SetActive(false); // 즉시 시각적으로 숨김
            Destroy(t.gameObject);         // 프레임 끝에 메모리 해제
        }
        _activeTiles.Clear();
    }

    /// <summary>colorType에 맞는 프리팹 반환. 없으면 null.</summary>
    GameObject GetPrefabForColor(PlayerColorType colorType)
    {
        foreach (ColorTilePrefabEntry entry in tilePrefabs)
        {
            if (entry.colorType == colorType)
                return entry.prefab;
        }
        return null;
    }

    // ── 에디터 ──────────────────────────────────────────────────

    [ContextMenu("테스트: 챌린지 시작")]
    void Debug_Activate() => Activate();

    [ContextMenu("테스트: 챌린지 취소")]
    void Debug_Cancel() => Cancel();

    void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);
        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawWireCube(sp.position, Vector3.one * 0.8f);
        }
    }
}
