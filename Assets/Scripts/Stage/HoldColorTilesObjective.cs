using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 목표 4: 인원 모두가 각자 맞는 색 타일에서 X초 동안 버티기.
/// 각 PlayerZonePair 에 플레이어와 그 플레이어가 서 있어야 할 존 콜라이더를 연결.
/// 전원이 동시에 자기 존 안에 있어야 타이머 진행. 한 명이라도 이탈 시 리셋.
///
/// [설정 예시]
///   pairs[0]: player=BluePlayer,  zone=BlueZoneCollider
///   pairs[1]: player=RedPlayer,   zone=RedZoneCollider
///   holdDuration = 10
/// </summary>
public class HoldColorTilesObjective : StageObjective
{
    [Serializable]
    public class PlayerZonePair
    {
        [Tooltip("플레이어 오브젝트")]
        public Player player;

        [Tooltip("이 플레이어가 서 있어야 할 존 콜라이더")]
        public Collider zone;

        [Tooltip("존에 머무는 동안 보여줄 색")]
        public Color holdingColor = Color.cyan;

        [Tooltip("이탈 상태 색")]
        public Color waitingColor = new Color(0.5f, 0.5f, 0.5f);

        // 런타임 내부 상태
        [HideInInspector] public bool      isInside;
        [HideInInspector] public Material[] mats;
    }

    [Header("플레이어-존 쌍")]
    public PlayerZonePair[] pairs;

    [Header("버티기 설정")]
    [Tooltip("전원이 동시에 자기 존에 있어야 하는 시간(초)")]
    public float holdDuration = 10f;

    [Header("Runtime (확인용)")]
    [SerializeField] float _elapsed;
    [SerializeField] bool  _isHolding;

    public float Elapsed   => _elapsed;
    public float Remaining => Mathf.Max(0f, holdDuration - _elapsed);

    public UnityEvent<float> OnHoldTimeChanged;
    public UnityEvent        OnHoldBroken;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");
    float _nextUITick;

    public override void Begin()
    {
        _elapsed   = 0f;
        _isHolding = false;

        for (int i = 0; i < pairs.Length; i++)
        {
            var pair = pairs[i];
            if (pair == null || pair.zone == null) continue;

            pair.isInside = false;

            // 각 존 오브젝트의 머티리얼 인스턴스 캐시
            var renderers = pair.zone.GetComponentsInChildren<MeshRenderer>(true);
            pair.mats = new Material[renderers.Length];
            for (int r = 0; r < renderers.Length; r++)
                if (renderers[r] != null)
                    pair.mats[r] = renderers[r].material;

            ApplyColor(pair, pair.waitingColor);
        }
    }

    public override void Tick()
    {
        if (IsCompleted || IsFailed) return;

        bool allInside = AllPairsInside();

        if (allInside)
        {
            if (!_isHolding)
            {
                _isHolding = true;
                foreach (var pair in pairs)
                    if (pair != null) ApplyColor(pair, pair.holdingColor);
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
                OnHoldBroken?.Invoke();
                OnHoldTimeChanged?.Invoke(holdDuration);
            }

            // 개별 색 피드백: 각 존을 현재 상태에 맞게 업데이트
            for (int i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i];
                if (pair == null || pair.zone == null || pair.player == null) continue;

                bool inside = !pair.player.IsDead && IsInsideCollider(pair.player.transform.position, pair.zone);
                if (inside != pair.isInside)
                {
                    pair.isInside = inside;
                    ApplyColor(pair, inside ? pair.holdingColor : pair.waitingColor);
                }
            }
        }
    }

    bool AllPairsInside()
    {
        if (pairs == null || pairs.Length == 0) return false;

        for (int i = 0; i < pairs.Length; i++)
        {
            var pair = pairs[i];
            if (pair == null || pair.player == null || pair.zone == null) return false;
            if (pair.player.IsDead) return false;
            if (!IsInsideCollider(pair.player.transform.position, pair.zone)) return false;
        }
        return true;
    }

    bool IsInsideCollider(Vector3 worldPos, Collider col)
    {
        Vector3 closest = col.ClosestPoint(worldPos);
        return Vector3.SqrMagnitude(closest - worldPos) < 0.001f;
    }

    void ApplyColor(PlayerZonePair pair, Color color)
    {
        if (pair.mats == null) return;
        for (int i = 0; i < pair.mats.Length; i++)
        {
            var mat = pair.mats[i];
            if (mat == null) continue;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            else if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < pairs.Length; i++)
        {
            if (pairs[i]?.mats == null) continue;
            for (int r = 0; r < pairs[i].mats.Length; r++)
                if (pairs[i].mats[r] != null) Destroy(pairs[i].mats[r]);
        }
    }

    void OnDrawGizmos()
    {
        if (pairs == null) return;
        for (int i = 0; i < pairs.Length; i++)
        {
            var pair = pairs[i];
            if (pair?.zone == null) continue;

            Gizmos.color = pair.isInside ? pair.holdingColor : pair.waitingColor;
            Gizmos.DrawWireCube(pair.zone.bounds.center, pair.zone.bounds.size);

#if UNITY_EDITOR
            string label = pair.player != null ? pair.player.name : "?";
            UnityEditor.Handles.Label(
                pair.zone.bounds.center + Vector3.up * (pair.zone.bounds.extents.y + 0.3f),
                label);
#endif
        }
#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position,
            $"{_elapsed:F1} / {holdDuration}s");
#endif
    }
}
