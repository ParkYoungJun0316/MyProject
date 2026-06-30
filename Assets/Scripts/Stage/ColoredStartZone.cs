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
    [Tooltip("플레이어가 없을 때 존 색상")]
    [SerializeField] Color vacantColor    = new Color(0.2f, 0.2f, 0.2f);
    [Tooltip("플레이어가 들어왔을 때 존 색상")]
    [SerializeField] Color occupiedColor  = Color.green;
    [Tooltip("카운트다운 중 존 색상")]
    [SerializeField] Color countdownColor = Color.yellow;

    public PlayerColorType ColorType       => colorType;
    public bool            IsOccupied      => _isOccupied;
    public Player          OccupyingPlayer => _currentPlayer;
    public Vector3         SpawnPosition   => spawnPoint != null ? spawnPoint.position : transform.position;
    public Quaternion      SpawnRotation   => spawnPoint != null ? spawnPoint.rotation : transform.rotation;

    public event System.Action OnOccupied;
    public event System.Action OnVacated;

    bool     _isOccupied;
    Player   _currentPlayer;
    Material[] _mats;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        MeshRenderer[] rends = GetComponentsInChildren<MeshRenderer>(true);
        _mats = new Material[rends.Length];
        for (int i = 0; i < rends.Length; i++)
            if (rends[i] != null) _mats[i] = rends[i].material;

        ApplyColor(vacantColor);
    }

    void Start()
    {
        // GameSession이 있으면 활성 색 기준으로 자신을 켜고 끔
        if (GameSession.Instance != null && !GameSession.Instance.IsColorActive(colorType))
            gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_mats == null) return;
        foreach (Material m in _mats)
            if (m != null) Destroy(m);
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

        // 리스폰 위치 갱신 — 이 존이 해당 플레이어의 리스폰 위치가 됨
        p.ForceSetSpawnPoint(SpawnPosition, SpawnRotation);

        ApplyColor(occupiedColor);
        OnOccupied?.Invoke();
    }

    void OnTriggerExit(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null || p != _currentPlayer) return;

        _currentPlayer = null;
        _isOccupied    = false;
        ApplyColor(vacantColor);
        OnVacated?.Invoke();
    }

    /// <summary>StageStartGate가 카운트다운 상태를 시각적으로 표시할 때 호출.</summary>
    public void SetCountdownVisual(bool counting)
    {
        if (!_isOccupied) return;
        ApplyColor(counting ? countdownColor : occupiedColor);
    }

    void ApplyColor(Color c)
    {
        if (_mats == null) return;
        foreach (Material m in _mats)
        {
            if (m == null) continue;
            if (m.HasProperty(BaseColorId))      m.SetColor(BaseColorId, c);
            else if (m.HasProperty(ColorId))     m.SetColor(ColorId,     c);
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
