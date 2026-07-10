using UnityEngine;

/// <summary>
/// 색상 매칭 게임 시작 구역.
///
/// [동작]
/// - colorType과 일치하는 플레이어만 진입 허용 (노란 존 = 노란 플레이어)
/// - 진입 시 플레이어 리스폰 위치 자동 갱신 (리스폰 위치 = 이 존)
/// - 점유/이탈 상태를 StageStartGate에 이벤트로 보고
///
/// [설정 방법]
/// 1. 빈 GameObject에 이 스크립트 + Collider(Is Trigger) 추가
/// 2. colorType: 담당 플레이어 색 선택 (Blue / Red / Green / Yellow)
/// 3. spawnPoint: 리스폰 위치 Transform (비우면 이 오브젝트 위치 사용)
/// 4. StageStartGate.zones[] 에 등록
/// </summary>
[RequireComponent(typeof(Collider))]
public class ColoredStartZone : MonoBehaviour
{
    [Header("색상")]
    [Tooltip("이 존에 들어올 수 있는 플레이어 색상")]
    [SerializeField] PlayerColorType colorType = PlayerColorType.Blue;

    [Header("리스폰 위치")]
    [Tooltip("해당 플레이어의 리스폰 Transform. 비우면 이 오브젝트 위치/회전 사용")]
    [SerializeField] Transform spawnPoint = null;

    [Header("색상 피드백")]
    [Tooltip("플레이어가 들어왔을 때 존 색상 (_BaseColor 덮어씀). 이탈 시 머터리얼 원본 색으로 복원됨")]
    [SerializeField] Color occupiedColor = Color.white;

    [Header("스폰 높이")]
    [Tooltip("스폰·리스폰 시 Y 오프셋 (바닥 관통 방지). 프리팹 루트가 y=0 기준일 때 0.5 권장")]
    [SerializeField] float spawnHeightOffset = 0.5f;

    public PlayerColorType ColorType       => colorType;
    public bool            IsOccupied      => _isOccupied;
    public Player          OccupyingPlayer => _currentPlayer;
    public Vector3         SpawnPosition
    {
        get
        {
            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            pos.y += spawnHeightOffset;
            return pos;
        }
    }
    public Quaternion      SpawnRotation   => spawnPoint != null ? spawnPoint.rotation : transform.rotation;

    public event System.Action OnOccupied;
    public event System.Action OnVacated;

    bool       _isOccupied;
    Player     _currentPlayer;
    Material[] _mats;
    Color[]    _originalColors;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        MeshRenderer[] rends = GetComponentsInChildren<MeshRenderer>(true);
        _mats           = new Material[rends.Length];
        _originalColors = new Color[rends.Length];

        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            _mats[i] = rends[i].material;
            if      (_mats[i].HasProperty(BaseColorId)) _originalColors[i] = _mats[i].GetColor(BaseColorId);
            else if (_mats[i].HasProperty(ColorId))     _originalColors[i] = _mats[i].GetColor(ColorId);
        }

        // OnPlayersReady는 스폰이 완전히 끝난 뒤 발행되므로, 이 시점에 확정 재보정한다.
        // GameObject가 비활성화돼도 static event 구독은 끊기지 않으므로(코루틴이 아닌
        // 일반 델리게이트 호출) Start()에서 SetActive(false)가 걸려도 이후 정상 수신됨.
        PlayerSpawnCoordinator.OnPlayersReady += RefreshActiveStateConfirmed;
    }

    void Start()
    {
        // GameSession 기준 최선 추정치로 즉시 반영 (온라인은 RPC 도달 전이라 틀릴 수 있음 —
        // OnPlayersReady 발행 시 RefreshActiveStateConfirmed()가 확정 재보정한다)
        if (GameSession.Instance != null && !GameSession.Instance.IsColorActive(colorType))
            gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= RefreshActiveStateConfirmed;

        if (_mats == null) return;
        foreach (Material m in _mats)
            if (m != null) Destroy(m);
    }

    /// <summary>
    /// 플레이어 스폰이 완전히 끝난 뒤(OnPlayersReady) 호출되는 확정 재보정.
    /// 온라인 모드에서는 PlayerSpawnCoordinator의 네트워크 동기화된 색 목록(레이스 없음)을
    /// 사용해, Start() 시점에 GameSession 동기화가 아직 안 끝나 존이 잘못 꺼졌던 경우를 되살린다.
    /// </summary>
    void RefreshActiveStateConfirmed()
    {
        bool active = LobbyContext.IsOnline
            ? PlayerSpawnCoordinator.IsColorInSession(colorType)
            : (GameSession.Instance == null || GameSession.Instance.IsColorActive(colorType));

        gameObject.SetActive(active);
    }

    void OnTriggerEnter(Collider other) => TryOccupy(other);

    /// <summary>
    /// NetworkTransform이 transform.position을 직접 설정하거나 플레이어가 스폰 시
    /// 트리거 내부에 이미 있을 경우 OnTriggerEnter가 발동하지 않을 수 있다.
    /// OnTriggerStay로 매 프레임 보완 감지한다.
    /// </summary>
    void OnTriggerStay(Collider other) => TryOccupy(other);

    void TryOccupy(Collider other)
    {
        if (_isOccupied) return;

        Player p = other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;
        if (p.playerColorType != colorType) return;

        _currentPlayer = p;
        _isOccupied    = true;

        // [B+C 방식] 리스폰 위치는 PlayerSpawnManager 고정 좌표가 담당.
        // Zone 점유 시 ForceSetSpawnPoint 호출 불필요.

        ApplyColor(occupiedColor);
        OnOccupied?.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null || p != _currentPlayer) return;

        _currentPlayer = null;
        _isOccupied    = false;
        RestoreOriginalColor();
        OnVacated?.Invoke();
    }

    /// <summary>StageStartGate가 카운트다운 상태를 시각적으로 표시할 때 호출. 색은 occupiedColor 유지.</summary>
    public void SetCountdownVisual(bool counting)
    {
        if (!_isOccupied) return;
        ApplyColor(occupiedColor);
    }

    void ApplyColor(Color c)
    {
        if (_mats == null) return;
        foreach (Material m in _mats)
        {
            if (m == null) continue;
            if      (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            else if (m.HasProperty(ColorId))     m.SetColor(ColorId,     c);
        }
    }

    void RestoreOriginalColor()
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
        {
            Material m = _mats[i];
            if (m == null) continue;
            if      (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, _originalColors[i]);
            else if (m.HasProperty(ColorId))     m.SetColor(ColorId,     _originalColors[i]);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pos, 0.4f);
        if (spawnPoint != null)
            Gizmos.DrawLine(transform.position, spawnPoint.position);
    }
#endif
}
