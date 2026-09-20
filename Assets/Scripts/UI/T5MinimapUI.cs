using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// T.Stage5 전용 지도. `Objective_Panel`의 생존시간 **아래에 붙는 별도 패널**이다.
/// SSOT: `Assets/Docs/TStage5RunnerRedesign.md` §1.11
///
/// [표시하는 것 — 셋뿐이다]
///  · **러너** 위치 · **start / goal** 위치 · **체이서 4마리** 위치.
///  문·패드·안내자는 그리지 않는다. 점이 늘어날수록 1층에서 급히 읽어야 하는 정보가 흐려지고,
///  문 색은 이미 눈앞에 있다.
///
/// [왜 ObjectiveUI의 자식이 아닌가 — 자식으로 두면 지워진다]
///  `ObjectiveUI.BuildSlots()`는 `Start()`와 `Refresh()`마다 **자기 자식을 전부 Destroy**하고
///  `VerticalLayoutGroup`을 붙인다. `Objective_Panel` 안에 지도를 넣으면 스테이지가 시작하는
///  순간 사라진다. 그래서 같은 캔버스의 **형제 패널**로 두고, 생존시간 슬롯은 `ObjectiveUI`가
///  그대로 굴린다(그쪽 코드는 손대지 않았다).
///
/// [네트워크] **쓰기 없음 · 신규 NV/RPC 없음.**
///  · 러너 — `StageNetworkState.T5RunnerClientId`(NV)로 누구인지 알고, 위치는
///    `ClientNetworkTransform`이라 전 머신에 이미 수렴해 있다(`T5RunnerMarkerUI`와 같은 경로).
///  · 체이서 — Host가 스폰하고 서버 권한 NetworkTransform이 위치를 복제하므로 Client에도 실물이 있다.
///    4마리 1회 스폰 + 리스폰 없음(§1.7)이라 목록이 사실상 고정이고,
///    <see cref="chaserRescanInterval"/> 간격의 재탐색이면 충분하다.
///
/// [좌표] 월드 XZ → 지도. 격자는 −12~108 정사각형이다(§1.4 좌표계, 2026-09-21 이동).
///  start/goal은 **씬 콜라이더에서 읽는다** — `T5RunnerDirector.RunnerStartZone`과
///  `T5RunnerObjective.RunnerGoalZone`. 둘 중 하나가 비면 그 점만 숨긴다(좌표를 추측하지 않는다).
///
/// [T5 밖에서는 스스로 꺼진다]
///  이 패널은 공용 `UI.prefab`에 들어가므로 `BossHealBar_Panel`과 같은 규칙을 따른다 —
///  프리팹에서는 비활성, `T.Stage5` 씬 인스턴스에서만 활성. 그래도 씬에 `T5RunnerObjective`가
///  없으면 여기서 한 번 더 접는다(다른 씬에서 실수로 켜졌을 때의 안전망).
///
/// [씬 설정]
///  1. `UI.prefab` 캔버스 아래 빈 패널(`T5Map_Panel`, RectTransform)에 이 스크립트만 붙인다.
///     배경·격자선·점은 전부 코드가 만든다(`ObjectiveUI`·`T5RunnerMarkerUI`와 같은 방식).
///  2. `Objective_Panel`(상단 중앙, anchoredPosition (0,-70), 370×120) **아래**로 위치·크기 조정.
///  3. 프리팹에서는 **비활성**으로 저장하고, `T.Stage5` 씬의 UI 인스턴스에서만 활성화.
/// </summary>
public class T5MinimapUI : MonoBehaviour
{
    [Header("월드 범위 (§1.4 — 격자 −12~108 정사각형)")]
    [Tooltip("지도 좌하단에 대응하는 월드 (x, z).")]
    [SerializeField] Vector2 worldMin = new Vector2(-12f, -12f);

    [Tooltip("지도 우상단에 대응하는 월드 (x, z).")]
    [SerializeField] Vector2 worldMax = new Vector2(108f, 108f);

    [Header("여백 / 배경")]
    [Tooltip("패널 안쪽 여백(px). 점이 테두리에 물리지 않게 한다.")]
    [SerializeField] float padding = 10f;

    [Tooltip("지도 배경색.")]
    [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("격자선 (10×10 칸)")]
    [Tooltip("한 변의 칸 수. §1.4 = 10.")]
    [SerializeField] int gridCells = 10;

    [Tooltip("격자선 색.")]
    [SerializeField] Color gridColor = new Color(1f, 1f, 1f, 0.18f);

    [Tooltip("격자선 두께(px).")]
    [SerializeField] float gridThickness = 1f;

    [Header("점")]
    [Tooltip("start 점 색.")]
    [SerializeField] Color startColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    [Tooltip("goal 점 색.")]
    [SerializeField] Color goalColor = new Color(0.4f, 1f, 0.5f, 1f);

    [Tooltip("러너 점 색. T5RunnerMarkerUI의 머리 위 화살표와 같은 노랑으로 맞춰 둔다.")]
    [SerializeField] Color runnerColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("체이서 점 색.")]
    [SerializeField] Color chaserColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("start/goal 점 크기(px).")]
    [SerializeField] float staticDotSize = 10f;

    [Tooltip("러너·체이서 점 크기(px).")]
    [SerializeField] float actorDotSize = 12f;

    [Header("갱신")]
    [Tooltip("체이서 목록을 다시 훑는 주기(초). 4마리 1회 스폰이라 촘촘할 이유가 없다.")]
    [SerializeField] float chaserRescanInterval = 0.5f;

    // ── 내부 ────────────────────────────────────────────────────

    RectTransform _area;          // 여백을 뺀 실제 지도 영역. 점들의 부모다.
    RectTransform _startDot;
    RectTransform _goalDot;
    RectTransform _runnerDot;

    readonly List<RectTransform>  _chaserDots = new List<RectTransform>();
    readonly List<Stage5ChaserAI> _chasers    = new List<Stage5ChaserAI>();

    T5RunnerObjective _objective;
    T5RunnerDirector  _director;

    // 러너 Transform 캐시 — clientId가 바뀔 때만 다시 찾는다(T5RunnerMarkerUI와 같은 이유).
    Transform _runnerTransform;
    ulong     _cachedRunnerId = ulong.MaxValue;
    bool      _cacheValid;

    float _nextChaserScan;
    bool  _active;

    void Start()
    {
        _objective = FindFirstObjectByType<T5RunnerObjective>(FindObjectsInactive.Include);
        _director  = FindFirstObjectByType<T5RunnerDirector>(FindObjectsInactive.Include);

        // T5가 아닌 씬에서 켜졌다면 아무것도 그리지 않는다.
        if (_objective == null) return;

        _active = true;
        Build();
    }

    void LateUpdate()
    {
        if (!_active) return;

        UpdateStaticDot(_startDot, _director  != null ? _director.RunnerStartZone : null);
        UpdateStaticDot(_goalDot,  _objective != null ? _objective.RunnerGoalZone : null);

        UpdateRunnerDot();
        UpdateChaserDots();
    }

    // ── 점 갱신 ─────────────────────────────────────────────────

    /// <summary>start/goal처럼 움직이지 않는 점. 콜라이더가 없으면 그 점만 숨긴다.</summary>
    void UpdateStaticDot(RectTransform dot, Collider zone)
    {
        if (dot == null) return;

        if (zone == null)
        {
            SetDotVisible(dot, false);
            return;
        }

        SetDotVisible(dot, true);
        SetNormalized(dot, WorldToNormalized(zone.bounds.center));
    }

    void UpdateRunnerDot()
    {
        Transform runner = ResolveRunner();
        if (runner == null)
        {
            SetDotVisible(_runnerDot, false);
            return;
        }

        SetDotVisible(_runnerDot, true);
        SetNormalized(_runnerDot, WorldToNormalized(runner.position));
    }

    void UpdateChaserDots()
    {
        if (Time.time >= _nextChaserScan)
        {
            _nextChaserScan = Time.time + Mathf.Max(0.1f, chaserRescanInterval);
            RescanChasers();
        }

        for (int i = 0; i < _chaserDots.Count; i++)
        {
            Stage5ChaserAI chaser = i < _chasers.Count ? _chasers[i] : null;
            if (chaser == null)
            {
                SetDotVisible(_chaserDots[i], false);
                continue;
            }

            SetDotVisible(_chaserDots[i], true);
            SetNormalized(_chaserDots[i], WorldToNormalized(chaser.transform.position));
        }
    }

    /// <summary>씬의 체이서를 다시 훑고, 점이 모자라면 그만큼 더 만든다.</summary>
    void RescanChasers()
    {
        _chasers.Clear();
        foreach (Stage5ChaserAI chaser in FindObjectsByType<Stage5ChaserAI>(FindObjectsSortMode.None))
            if (chaser != null) _chasers.Add(chaser);

        while (_chaserDots.Count < _chasers.Count)
            _chaserDots.Add(CreateDot($"Chaser_{_chaserDots.Count}", chaserColor, actorDotSize));
    }

    /// <summary>표시해야 할 러너의 Transform. 미추첨이면 null.</summary>
    Transform ResolveRunner()
    {
        var net = StageNetworkState.Instance;
        var nm  = NetworkManager.Singleton;
        if (net == null || nm == null || !nm.IsListening) return null;

        ulong runnerId = net.T5RunnerClientId;
        if (runnerId == StageNetworkState.NoRunner) return null;

        if (!_cacheValid || runnerId != _cachedRunnerId || _runnerTransform == null)
        {
            _runnerTransform = FindPlayerTransform(runnerId);
            _cachedRunnerId  = runnerId;
            _cacheValid      = true;
        }

        return _runnerTransform;
    }

    /// <summary>
    /// clientId로 Player를 찾는다. Client에서는 `ConnectedClients`가 비어 있으므로 씬의 Player를
    /// 훑어 OwnerClientId로 대조한다 — 판당 한 번만 도는 경로다(T5RunnerMarkerUI와 같은 방식).
    /// </summary>
    static Transform FindPlayerTransform(ulong clientId)
    {
        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            NetworkObject no = p.GetComponent<NetworkObject>();
            if (no != null && no.OwnerClientId == clientId) return p.transform;
        }
        return null;
    }

    // ── 좌표 ────────────────────────────────────────────────────

    /// <summary>
    /// 월드 XZ → 지도 안의 0~1. 시작 홀은 격자 밖(음수 좌표)이라 clamp로 가장자리에 붙인다 —
    /// 출발 전 러너 점이 사라졌다 나타나는 것보다 "아직 시작 쪽"으로 보이는 편이 읽기 쉽다.
    /// </summary>
    Vector2 WorldToNormalized(Vector3 world)
    {
        float w = worldMax.x - worldMin.x;
        float h = worldMax.y - worldMin.y;
        if (Mathf.Approximately(w, 0f) || Mathf.Approximately(h, 0f)) return new Vector2(0.5f, 0.5f);

        return new Vector2(
            Mathf.Clamp01((world.x - worldMin.x) / w),
            Mathf.Clamp01((world.z - worldMin.y) / h));
    }

    /// <summary>anchor로 위치를 잡는다 — 패널 크기가 바뀌어도 비율이 유지된다(ObjectiveUI와 같은 기법).</summary>
    static void SetNormalized(RectTransform rect, Vector2 normalized)
    {
        rect.anchorMin        = normalized;
        rect.anchorMax        = normalized;
        rect.anchoredPosition = Vector2.zero;
    }

    static void SetDotVisible(RectTransform dot, bool visible)
    {
        if (dot != null && dot.gameObject.activeSelf != visible)
            dot.gameObject.SetActive(visible);
    }

    // ── 생성 (배경 · 격자선 · 점) ────────────────────────────────

    void Build()
    {
        Image bg = GetComponent<Image>();
        if (bg == null) bg = gameObject.AddComponent<Image>();
        bg.color         = backgroundColor;
        bg.raycastTarget = false;

        // 여백을 뺀 지도 영역 — 점의 0~1은 전부 이 사각형 기준이다.
        var areaObj = new GameObject("Area");
        areaObj.transform.SetParent(transform, false);
        _area = areaObj.AddComponent<RectTransform>();
        _area.anchorMin = Vector2.zero;
        _area.anchorMax = Vector2.one;
        _area.offsetMin = new Vector2(padding,  padding);
        _area.offsetMax = new Vector2(-padding, -padding);

        BuildGrid();

        _startDot  = CreateDot("Start",  startColor,  staticDotSize);
        _goalDot   = CreateDot("Goal",   goalColor,   staticDotSize);
        _runnerDot = CreateDot("Runner", runnerColor, actorDotSize);

        SetDotVisible(_startDot,  false);
        SetDotVisible(_goalDot,   false);
        SetDotVisible(_runnerDot, false);
    }

    /// <summary>칸 경계선. 한 번 만들고 다시 건드리지 않는다.</summary>
    void BuildGrid()
    {
        if (gridCells <= 0 || gridThickness <= 0f) return;

        var gridObj = new GameObject("Grid");
        gridObj.transform.SetParent(_area, false);
        RectTransform gridRect = gridObj.AddComponent<RectTransform>();
        gridRect.anchorMin = Vector2.zero;
        gridRect.anchorMax = Vector2.one;
        gridRect.offsetMin = Vector2.zero;
        gridRect.offsetMax = Vector2.zero;

        for (int i = 0; i <= gridCells; i++)
        {
            float t = (float)i / gridCells;
            CreateGridLine(gridRect, $"V_{i}", vertical: true,  at: t);
            CreateGridLine(gridRect, $"H_{i}", vertical: false, at: t);
        }
    }

    void CreateGridLine(RectTransform parent, string lineName, bool vertical, float at)
    {
        var lineObj = new GameObject(lineName);
        lineObj.transform.SetParent(parent, false);

        RectTransform rect = lineObj.AddComponent<RectTransform>();
        if (vertical)
        {
            rect.anchorMin = new Vector2(at, 0f);
            rect.anchorMax = new Vector2(at, 1f);
            rect.sizeDelta = new Vector2(gridThickness, 0f);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, at);
            rect.anchorMax = new Vector2(1f, at);
            rect.sizeDelta = new Vector2(0f, gridThickness);
        }
        rect.anchoredPosition = Vector2.zero;

        Image img = lineObj.AddComponent<Image>();
        img.color         = gridColor;
        img.raycastTarget = false;
    }

    RectTransform CreateDot(string dotName, Color color, float size)
    {
        var dotObj = new GameObject(dotName);
        dotObj.transform.SetParent(_area, false);

        RectTransform rect = dotObj.AddComponent<RectTransform>();
        rect.pivot     = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        SetNormalized(rect, new Vector2(0.5f, 0.5f));

        Image img = dotObj.AddComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;

        return rect;
    }
}
