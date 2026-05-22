using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// 5×5 링 16칸 순서 협동 미니게임 (1인: 키 1~4 / Space 시뮬).
/// 서버 권한 구조를 위해 판정·시퀀스 생성은 이 매니저 한 곳에서 처리합니다.
/// </summary>
public class SequenceRingMinigame : MonoBehaviour
{
    public const int RingTileCount = 16;

    public enum MinigameState { Idle, Playing, Success, Failed }

    /// <summary>Lookahead=Phase1(+N), PlusOne=현재+다음, CurrentOnly=현재만, NextOnly=다음만(현재 숨김)</summary>
    public enum PreviewMode
    {
        Lookahead,
        PlusOne,
        CurrentOnly,
        NextOnly,
    }

    public enum StepKind { Normal, Common, Danger }

    [Serializable]
    public struct StepData
    {
        public StepKind kind;
        public PlayerColorType color;
    }

    [Serializable]
    public class SimPlayerBinding
    {
        public PlayerColorType colorType = PlayerColorType.Blue;
        public Key submitKey = Key.Digit1;
    }

    [Serializable]
    public class ColorDisplayEntry
    {
        public PlayerColorType colorType = PlayerColorType.Blue;
        public Color displayColor = Color.blue;
    }

    [Header("타일 (자식 SequenceRingTile, ringIndex 0~15)")]
    [SerializeField] SequenceRingTile[] ringTiles = new SequenceRingTile[0];

    [Header("목표·시간")]
    [Tooltip("클리어에 필요한 성공 스텝 수 (예: 30)")]
    [SerializeField] int targetStepCount = 0;

    [Tooltip("제한 시간(초). 0 이하면 무제한")]
    [SerializeField] float timeLimit = 0f;

    [Tooltip("틀렸을 때 줄어드는 시간(초)")]
    [SerializeField] float timePenaltyOnWrong = 0f;

    [Header("미리보기 난이도")]
    [SerializeField] PreviewMode previewMode = PreviewMode.Lookahead;

    [Tooltip("Lookahead 모드: 현재 스텝부터 앞으로 몇 칸까지 색 유지 (Phase1 기본 12)")]
    [SerializeField] int previewLookahead = 12;

    [Tooltip("NextOnly: 시작(cur=0) 시 0·1번 칸 색 표시 (이후부터 다음 칸만)")]
    [FormerlySerializedAs("nextOnlyBootstrapFirstStep")]
    [SerializeField] bool nextOnlyBootstrapStartSteps = true;

    [Header("스텝 생성")]
    [Tooltip("Common(흰) 스텝이 나올 확률 0~1")]
    [SerializeField] float commonSpawnChance = 0f;

    [Tooltip("Danger(검) 스텝이 나올 확률 0~1")]
    [SerializeField] float dangerSpawnChance = 0f;

    [Tooltip("Danger 칸 유지 시간(초). 무입력 시 자동 통과")]
    [SerializeField] float dangerStepDuration = 1f;

    [Header("색상")]
    [Tooltip("미리보기 밖·숨김 타일 Base Color")]
    [SerializeField] Color defaultTileColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [SerializeField] Color commonDisplayColor = Color.white;
    [SerializeField] Color dangerDisplayColor = Color.black;

    [SerializeField] ColorDisplayEntry[] uniqueColorDisplays = new ColorDisplayEntry[0];

    [Header("1인 테스트 — 플레이어 시뮬")]
    [SerializeField] SimPlayerBinding[] simPlayers = new SimPlayerBinding[0];

    [Tooltip("Space = Common/Danger any-key, Normal 스텝에서는 오입력(패널티)")]
    [SerializeField] bool spaceActsAsAnyKey = true;

    [Header("시작")]
    [SerializeField] bool startOnAwake = false;

    [Header("이벤트")]
    public UnityEvent OnMinigameStarted;
    public UnityEvent OnMinigameSuccess;
    public UnityEvent OnMinigameFailed;
    public UnityEvent OnWrongInput;
    [FormerlySerializedAs("OnTimerTick")]
    public FloatEvent OnTimeRemainingChanged;

    MinigameState _state = MinigameState.Idle;
    StepData[] _steps = Array.Empty<StepData>();
    SequenceRingTile[] _sortedTiles = Array.Empty<SequenceRingTile>();

    int _currentStepIndex;
    int _successCount;
    float _timeRemaining;
    float _dangerStepTimer;
    System.Random _rng;

    public MinigameState State => _state;
    public int CurrentStepIndex => _currentStepIndex;
    public int SuccessCount => _successCount;
    public float TimeRemaining => _timeRemaining;
    public int TargetStepCount => targetStepCount;

    void Awake()
    {
        _rng = new System.Random();
        CollectAndSortTiles();
        ApplyDefaultColors();
    }

    void Start()
    {
        if (startOnAwake)
            StartMinigame();
    }

    void Update()
    {
        if (_state != MinigameState.Playing) return;

        TickTimer(Time.deltaTime);
        TickDangerStep(Time.deltaTime);
        PollSimInput();
    }

    // ── 공개 API ─────────────────────────────────────────────────

    public void StartMinigame()
    {
        if (_state == MinigameState.Playing) return;

        CollectAndSortTiles();
        if (_sortedTiles.Length == 0)
        {
            Debug.LogWarning("[SequenceRingMinigame] SequenceRingTile이 없습니다.", this);
            return;
        }

        if (targetStepCount <= 0)
        {
            Debug.LogWarning("[SequenceRingMinigame] targetStepCount는 1 이상이어야 합니다.", this);
            return;
        }

        GenerateSteps();
        _currentStepIndex = 0;
        _successCount     = 0;
        _timeRemaining    = timeLimit > 0f ? timeLimit : float.MaxValue;
        _dangerStepTimer  = 0f;
        _state            = MinigameState.Playing;

        OnEnterStep(_currentStepIndex);
        RefreshTileColors();
        OnMinigameStarted?.Invoke();
        BroadcastTime();
    }

    public void StopMinigame()
    {
        _state = MinigameState.Idle;
        ApplyDefaultColors();
    }

    public void ResetMinigame()
    {
        StopMinigame();
        _steps            = Array.Empty<StepData>();
        _currentStepIndex = 0;
        _successCount     = 0;
    }

    /// <summary>네트워크 연동용: 플레이어 색으로 입력 제출.</summary>
    public void TrySubmit(PlayerColorType color)
    {
        if (_state != MinigameState.Playing) return;
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return;

        StepData step = _steps[_currentStepIndex];

        switch (step.kind)
        {
            case StepKind.Common:
                AdvanceStep();
                break;

            case StepKind.Danger:
                ApplyWrongPenalty();
                break;

            case StepKind.Normal:
                if (step.color == color)
                    AdvanceStep();
                else
                    ApplyWrongPenalty();
                break;
        }
    }

    /// <summary>Common/Danger용 아무 키 입력.</summary>
    public void TrySubmitAnyKey()
    {
        if (_state != MinigameState.Playing) return;
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return;

        StepData step = _steps[_currentStepIndex];

        switch (step.kind)
        {
            case StepKind.Common:
                AdvanceStep();
                break;
            case StepKind.Danger:
                ApplyWrongPenalty();
                break;
            default:
                ApplyWrongPenalty();
                break;
        }
    }

    // ── 입력 (1인 시뮬) ──────────────────────────────────────────

    void PollSimInput()
    {
        if (Keyboard.current == null) return;

        if (spaceActsAsAnyKey && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TrySubmitAnyKey();
            return;
        }

        if (simPlayers == null) return;

        for (int i = 0; i < simPlayers.Length; i++)
        {
            Key key = simPlayers[i].submitKey;
            if (!WasKeyPressed(key)) continue;

            TrySubmit(simPlayers[i].colorType);
        }
    }

    static bool WasKeyPressed(Key key)
    {
        if (Keyboard.current == null) return false;
        var control = Keyboard.current[key];
        return control != null && control.wasPressedThisFrame;
    }

    // ── 타이머·Danger ────────────────────────────────────────────

    void TickTimer(float dt)
    {
        if (timeLimit <= 0f) return;

        _timeRemaining -= dt;
        BroadcastTime();

        if (_timeRemaining <= 0f)
            FailMinigame();
    }

    void TickDangerStep(float dt)
    {
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return;
        if (_steps[_currentStepIndex].kind != StepKind.Danger) return;

        _dangerStepTimer -= dt;
        if (_dangerStepTimer <= 0f)
            AdvanceStep();
    }

    void OnEnterStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= _steps.Length) return;

        if (_steps[stepIndex].kind == StepKind.Danger)
            _dangerStepTimer = dangerStepDuration > 0f ? dangerStepDuration : 0f;
        else
            _dangerStepTimer = 0f;
    }

    // ── 진행·판정 ────────────────────────────────────────────────

    void AdvanceStep()
    {
        _successCount++;
        _currentStepIndex++;

        if (_successCount >= targetStepCount)
        {
            SucceedMinigame();
            return;
        }

        if (_currentStepIndex >= _steps.Length)
        {
            Debug.LogWarning("[SequenceRingMinigame] 스텝 배열이 targetStepCount보다 짧습니다.", this);
            SucceedMinigame();
            return;
        }

        OnEnterStep(_currentStepIndex);
        RefreshTileColors();
    }

    void ApplyWrongPenalty()
    {
        if (timePenaltyOnWrong > 0f && timeLimit > 0f)
        {
            _timeRemaining -= timePenaltyOnWrong;
            BroadcastTime();
            if (_timeRemaining <= 0f)
            {
                FailMinigame();
                return;
            }
        }

        OnWrongInput?.Invoke();
    }

    void SucceedMinigame()
    {
        _state = MinigameState.Success;
        ApplyDefaultColors();
        OnMinigameSuccess?.Invoke();
    }

    void FailMinigame()
    {
        _state = MinigameState.Failed;
        ApplyDefaultColors();
        OnMinigameFailed?.Invoke();
    }

    void BroadcastTime()
    {
        float t = timeLimit > 0f ? Mathf.Max(0f, _timeRemaining) : _timeRemaining;
        OnTimeRemainingChanged?.Invoke(t);
    }

    // ── 시퀀스 생성 ──────────────────────────────────────────────

    void GenerateSteps()
    {
        _steps = new StepData[targetStepCount];
        PlayerColorType[] pool = GetUniqueColorPool();

        for (int i = 0; i < targetStepCount; i++)
        {
            float roll = (float)_rng.NextDouble();
            float dChance = Mathf.Clamp01(dangerSpawnChance);
            float cChance = Mathf.Clamp01(commonSpawnChance);

            if (roll < dChance)
            {
                _steps[i] = new StepData { kind = StepKind.Danger, color = PlayerColorType.Danger };
            }
            else if (roll < dChance + cChance)
            {
                _steps[i] = new StepData { kind = StepKind.Common, color = PlayerColorType.Common };
            }
            else
            {
                PlayerColorType c = pool.Length > 0
                    ? pool[_rng.Next(pool.Length)]
                    : PlayerColorType.Blue;
                _steps[i] = new StepData { kind = StepKind.Normal, color = c };
            }
        }
    }

    PlayerColorType[] GetUniqueColorPool()
    {
        if (simPlayers != null && simPlayers.Length > 0)
        {
            var list = new List<PlayerColorType>();
            for (int i = 0; i < simPlayers.Length; i++)
            {
                PlayerColorType t = simPlayers[i].colorType;
                if (t == PlayerColorType.Common || t == PlayerColorType.Danger) continue;
                if (!list.Contains(t)) list.Add(t);
            }
            if (list.Count > 0) return list.ToArray();
        }

        return new[]
        {
            PlayerColorType.Blue,
            PlayerColorType.Green,
            PlayerColorType.Yellow,
            PlayerColorType.Purple,
        };
    }

    // ── 미리보기 가시 스텝 ───────────────────────────────────────

    HashSet<int> BuildVisibleStepSet()
    {
        var set = new HashSet<int>();
        int total = _steps.Length;
        if (total == 0) return set;

        int cur = _currentStepIndex;

        switch (previewMode)
        {
            case PreviewMode.Lookahead:
            {
                int end = Mathf.Min(cur + Mathf.Max(0, previewLookahead), total - 1);
                for (int s = cur; s <= end; s++)
                    set.Add(s);
                break;
            }

            case PreviewMode.PlusOne:
            {
                set.Add(cur);
                if (cur + 1 < total) set.Add(cur + 1);
                break;
            }

            case PreviewMode.CurrentOnly:
                set.Add(cur);
                break;

            case PreviewMode.NextOnly:
                if (cur + 1 < total)
                    set.Add(cur + 1);
                if (cur == 0 && nextOnlyBootstrapStartSteps)
                {
                    set.Add(0);
                    if (total > 1)
                        set.Add(1);
                }
                break;
        }

        return set;
    }

    // ── 타일 색 갱신 ─────────────────────────────────────────────

    void RefreshTileColors()
    {
        if (_sortedTiles.Length == 0) return;

        HashSet<int> visible = BuildVisibleStepSet();
        int total = _steps.Length;
        int cur = _currentStepIndex;

        for (int r = 0; r < _sortedTiles.Length; r++)
        {
            SequenceRingTile tile = _sortedTiles[r];
            if (tile == null) continue;

            int ring = tile.RingIndex;
            int displayStep = ResolveDisplayStepForRing(ring, visible, cur, total);
            Color color = displayStep >= 0
                ? GetDisplayColor(_steps[displayStep])
                : defaultTileColor;

            tile.ApplyColor(color);
        }
    }

    /// <summary>링 칸에 표시할 스텝 인덱스. 없으면 -1(기본색).</summary>
    int ResolveDisplayStepForRing(int ring, HashSet<int> visible, int cur, int total)
    {
        if (total == 0 || cur < 0) return -1;

        if (previewMode == PreviewMode.NextOnly)
        {
            bool hideCurrent = !(cur == 0 && nextOnlyBootstrapStartSteps);
            return FindBestStepOnRing(ring, visible, cur, total, hideCurrentStep: hideCurrent);
        }

        int curRing = cur < total ? cur % RingTileCount : -1;
        if (curRing == ring && cur < total)
            return cur;

        return FindBestStepOnRing(ring, visible, cur, total, hideCurrentStep: false);
    }

    static int FindBestStepOnRing(int ring, HashSet<int> visible, int cur, int total, bool hideCurrentStep)
    {
        int bestStep = -1;
        for (int s = 0; s < total; s++)
        {
            if (s % RingTileCount != ring) continue;
            if (!visible.Contains(s)) continue;
            if (hideCurrentStep && s == cur) continue;

            if (bestStep < 0 || s < bestStep)
                bestStep = s;
        }

        return bestStep;
    }

    Color GetDisplayColor(StepData step)
    {
        switch (step.kind)
        {
            case StepKind.Common: return commonDisplayColor;
            case StepKind.Danger: return dangerDisplayColor;
            case StepKind.Normal: return GetUniqueDisplayColor(step.color);
            default: return defaultTileColor;
        }
    }

    Color GetUniqueDisplayColor(PlayerColorType colorType)
    {
        if (uniqueColorDisplays != null)
        {
            for (int i = 0; i < uniqueColorDisplays.Length; i++)
                if (uniqueColorDisplays[i].colorType == colorType)
                    return uniqueColorDisplays[i].displayColor;
        }

        return ColoredMemoryPath.GetDefaultColorFor(colorType);
    }

    void ApplyDefaultColors()
    {
        if (_sortedTiles == null) return;
        for (int i = 0; i < _sortedTiles.Length; i++)
            if (_sortedTiles[i] != null)
                _sortedTiles[i].ApplyColor(defaultTileColor);
    }

    void CollectAndSortTiles()
    {
        if (ringTiles == null || ringTiles.Length == 0)
            ringTiles = GetComponentsInChildren<SequenceRingTile>(true);

        var list = new List<SequenceRingTile>();
        for (int i = 0; i < ringTiles.Length; i++)
            if (ringTiles[i] != null)
                list.Add(ringTiles[i]);

        list.Sort((a, b) => a.RingIndex.CompareTo(b.RingIndex));
        _sortedTiles = list.ToArray();
    }

    // ── 에디터 ───────────────────────────────────────────────────

    [ContextMenu("미니게임 시작")]
    void Debug_Start() => StartMinigame();

    [ContextMenu("미니게임 정지")]
    void Debug_Stop() => StopMinigame();

    void Reset()
    {
        simPlayers = new[]
        {
            new SimPlayerBinding { colorType = PlayerColorType.Blue,   submitKey = Key.Digit1 },
            new SimPlayerBinding { colorType = PlayerColorType.Green,  submitKey = Key.Digit2 },
            new SimPlayerBinding { colorType = PlayerColorType.Yellow, submitKey = Key.Digit3 },
            new SimPlayerBinding { colorType = PlayerColorType.Purple, submitKey = Key.Digit4 },
        };

        uniqueColorDisplays = new[]
        {
            new ColorDisplayEntry { colorType = PlayerColorType.Blue,   displayColor = Color.blue },
            new ColorDisplayEntry { colorType = PlayerColorType.Green,  displayColor = Color.green },
            new ColorDisplayEntry { colorType = PlayerColorType.Yellow, displayColor = Color.yellow },
            new ColorDisplayEntry { colorType = PlayerColorType.Purple, displayColor = new Color(0.55f, 0.2f, 0.95f) },
        };
    }

    void OnValidate()
    {
        previewLookahead = Mathf.Max(0, previewLookahead);
        targetStepCount  = Mathf.Max(0, targetStepCount);
    }
}
