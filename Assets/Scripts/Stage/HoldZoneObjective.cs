using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 목표 3: 인원 모두가 한 자리(존)에 X초 동안 버티기.
/// zoneCollider 영역 안에 players[] 전원이 동시에 있어야 타이머 진행.
/// 한 명이라도 나가면 타이머 리셋.
/// </summary>
public class HoldZoneObjective : StageObjective
{
    [Header("존 설정")]
    [Tooltip("버텨야 할 구역 콜라이더. 비우면 이 오브젝트의 Collider 사용")]
    public Collider zoneCollider;

    [Tooltip("동시에 존 안에 있어야 할 플레이어 목록. 비우면 씬 전체 자동 수집")]
    public Player[] players;

    [Tooltip("존 안에서 버텨야 하는 시간(초)")]
    public float holdDuration = 30f;

    [Header("시각 피드백")]
    [Tooltip("타이머 진행 중 존 색")]
    public Color holdingColor = Color.green;
    [Tooltip("인원 부족 시 존 색")]
    public Color waitingColor = Color.yellow;

    [Header("Runtime (확인용)")]
    [SerializeField] float _elapsed;
    [SerializeField] bool  _isHolding;

    public float Elapsed     => _elapsed;
    public float Remaining   => Mathf.Max(0f, holdDuration - _elapsed);
    public bool  IsHolding   => _isHolding;

    public UnityEvent<float> OnHoldTimeChanged; // 남은 시간 (UI용)
    public UnityEvent        OnHoldBroken;      // 인원 부족으로 타이머 리셋

    Material[] _mats;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    float _nextUITick;

    public override void Begin()
    {
        _elapsed    = 0f;
        _isHolding  = false;
        _nextUITick = 0f;

        if (zoneCollider == null) zoneCollider = GetComponent<Collider>();

        if (players == null || players.Length == 0)
            players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        // 머티리얼 인스턴스 캐시
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        _mats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                _mats[i] = renderers[i].material;

        ApplyColor(waitingColor);
    }

    public override void Tick()
    {
        if (IsCompleted || IsFailed) return;

        bool allInside = AllPlayersInside();

        if (allInside)
        {
            if (!_isHolding)
            {
                _isHolding = true;
                ApplyColor(holdingColor);
            }

            _elapsed += Time.deltaTime;

            if (Time.time >= _nextUITick)
            {
                _nextUITick = Time.time + 0.1f;
                OnHoldTimeChanged?.Invoke(Remaining);
            }

            if (_elapsed >= holdDuration)
                Complete();
        }
        else
        {
            if (_isHolding)
            {
                _isHolding = false;
                _elapsed   = 0f;
                ApplyColor(waitingColor);
                OnHoldBroken?.Invoke();
                OnHoldTimeChanged?.Invoke(holdDuration);
            }
        }
    }

    bool AllPlayersInside()
    {
        if (zoneCollider == null || players == null) return false;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || players[i].IsDead) return false;
            if (!IsInsideCollider(players[i].transform.position)) return false;
        }
        return players.Length > 0;
    }

    bool IsInsideCollider(Vector3 worldPos)
    {
        // ClosestPoint: 점이 콜라이더 안에 있으면 자기 자신을 반환
        Vector3 closest = zoneCollider.ClosestPoint(worldPos);
        return Vector3.SqrMagnitude(closest - worldPos) < 0.001f;
    }

    void ApplyColor(Color color)
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
        {
            var mat = _mats[i];
            if (mat == null) continue;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            else if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        }
    }

    void OnDestroy()
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
            if (_mats[i] != null) Destroy(_mats[i]);
    }

    void OnDrawGizmos()
    {
        if (zoneCollider == null) return;
        Gizmos.color = _isHolding ? holdingColor : waitingColor;
        Gizmos.DrawWireCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            zoneCollider.bounds.center + Vector3.up * (zoneCollider.bounds.extents.y + 0.3f),
            $"{_elapsed:F1} / {holdDuration}s");
#endif
    }
}
