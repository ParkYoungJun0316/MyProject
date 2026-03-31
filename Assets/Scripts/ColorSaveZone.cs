using UnityEngine;

/// <summary>세이브 존 전용 색상 (Red / Yellow / Green / Blue 4종)</summary>
public enum SaveZoneColor { Red, Yellow, Green, Blue }

/// <summary>
/// 색상 세이브 포인트의 개별 존 (원 하나).
/// 자신의 colorType과 일치하는 플레이어가 진입하면 ColorSavePoint에 알림.
///
/// [씬 설정]
///  - ColorSavePoint 오브젝트의 자식으로 배치
///  - Collider(Is Trigger = true) 필수
///  - spawnPoint: 해당 색 플레이어의 리스폰 위치 (없으면 이 Transform 위치 사용)
/// </summary>
[RequireComponent(typeof(Collider))]
public class ColorSaveZone : MonoBehaviour
{
    [Header("존 색상")]
    [Tooltip("이 존에 진입해야 하는 플레이어 색 (Red / Yellow / Green / Blue)")]
    public SaveZoneColor colorType = SaveZoneColor.Blue;

    [Header("리스폰 위치")]
    [Tooltip("해당 색 플레이어의 리스폰 Transform. 비우면 이 오브젝트 위치 사용")]
    public Transform spawnPoint;

    [Header("색상 피드백")]
    [Tooltip("플레이어가 없을 때 색")]
    public Color vacantColor  = new Color(0.25f, 0.25f, 0.25f);
    [Tooltip("플레이어가 들어왔을 때 색")]
    public Color occupiedColor = Color.cyan;

    bool _isOccupied;

    public bool      IsOccupied    => _isOccupied;
    public Player    CurrentPlayer => _currentPlayer;
    public Vector3   SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    public Quaternion SpawnRotation => spawnPoint != null ? spawnPoint.rotation : transform.rotation;

    Player          _currentPlayer;
    ColorSavePoint  _savePoint;
    Material[]      _mats;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        // Collider를 Trigger로 강제 설정
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // 머티리얼 인스턴스 생성 (SRP Batcher 대응)
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        _mats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                _mats[i] = renderers[i].material;

        ApplyColor(vacantColor);
    }

    void Start()
    {
        _savePoint = GetComponentInParent<ColorSavePoint>();
    }

    void OnDestroy()
    {
        for (int i = 0; i < _mats.Length; i++)
            if (_mats[i] != null) Destroy(_mats[i]);
    }

    // ── 충돌 ─────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;
        if (!MatchesPlayer(player)) return;

        _currentPlayer = player;
        _isOccupied    = true;
        ApplyColor(occupiedColor);
        _savePoint?.OnZoneOccupied(this);
    }

    void OnTriggerExit(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player != _currentPlayer) return;

        _currentPlayer = null;
        _isOccupied    = false;
        ApplyColor(vacantColor);
        _savePoint?.OnZoneVacated(this);
    }

    // ── 내부 ─────────────────────────────────────────────────────

    bool MatchesPlayer(Player player)
    {
        return colorType switch
        {
            SaveZoneColor.Red    => player.playerColorType == PlayerColorType.Red,
            SaveZoneColor.Yellow => player.playerColorType == PlayerColorType.Yellow,
            SaveZoneColor.Green  => player.playerColorType == PlayerColorType.Green,
            SaveZoneColor.Blue   => player.playerColorType == PlayerColorType.Blue,
            _                    => false
        };
    }

    void ApplyColor(Color color)
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
        {
            if (_mats[i] == null) continue;
            if (_mats[i].HasProperty(BaseColorId))      _mats[i].SetColor(BaseColorId, color);
            else if (_mats[i].HasProperty(ColorId))     _mats[i].SetColor(ColorId,     color);
        }
    }

    // ── 에디터 Gizmo ─────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (spawnPoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(spawnPoint.position, 0.3f);
        Gizmos.DrawLine(transform.position, spawnPoint.position);
    }
}
