using System.Collections;
using UnityEngine;

/// <summary>
/// 기억 경로 발판 하나.
/// Safe  : 미리보기 때 잠깐 빛났다가 꺼짐 → 밟아도 통과
/// Trap  : 미리보기에 표시 안 됨 → 밟으면 바닥이 사라져 낙사
///
/// MemoryPath 자식으로 배치. MemoryPath.Awake()가 자동으로 참조·역할을 주입.
/// </summary>
public class MemoryPathTile : MonoBehaviour
{
    public enum TileRole { Safe, Trap }

    [Header("발판 역할")]
    [Tooltip("Safe: 미리보기에 표시되는 안전 경로 / Trap: 밟으면 사라지는 함정")]
    public TileRole role = TileRole.Trap;

    [Header("시각 피드백")]
    [Tooltip("평상시(도전 중) 색")]
    public Color normalColor  = new Color(0.45f, 0.45f, 0.45f);
    [Tooltip("미리보기 때 Safe 발판에 표시되는 색")]
    public Color previewColor = new Color(1f, 0.85f, 0f);

    [Header("Trap 전용 설정")]
    [Tooltip("플레이어가 밟은 후 발판이 사라지기까지 딜레이(초). 0 = 즉시")]
    public float trapDisableDelay = 0f;
    [Tooltip("사라진 발판이 자동 복구되기까지 대기 시간(초). 0 = 복구 안 함")]
    public float trapRespawnDelay = 0f;
    [Tooltip("발판이 사라질 때 플레이어에게 가할 하방 임펄스. 0 = 자연낙하만")]
    public float downwardImpulse = 8f;

    // MemoryPath.Awake()에서 주입
    [HideInInspector] public MemoryPath memoryPath;

    Collider   _col;
    Material[] _mats;
    bool       _isDisabled;

    // 복구 시 IgnoreCollision 원상복구용
    Player     _lastPlayer;
    Collider[] _lastPlayerCols;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        _col = GetComponent<Collider>();

        // 루트·자식 MeshRenderer 모두 인스턴스 머티리얼로 캐시 (SRP Batcher 우회)
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        _mats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                _mats[i] = renderers[i].material;

        ApplyColor(normalColor);
    }

    void OnDestroy()
    {
        for (int i = 0; i < _mats.Length; i++)
            if (_mats[i] != null) Destroy(_mats[i]);
    }

    void OnCollisionEnter(Collision col)
    {
        if (_isDisabled) return;

        Player player = col.transform.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        if (role == TileRole.Trap)
            StartCoroutine(TrapRoutine(player));
        else
            memoryPath?.OnSafeTileStepped(this);
    }

    // ── 상태 전환 (MemoryPath에서 호출) ─────────────────────────

    /// <summary>미리보기: Safe 발판만 빛나게</summary>
    public void ShowPreview() => ApplyColor(previewColor);

    /// <summary>미리보기 종료: 모든 발판 동일하게 보이게</summary>
    public void HidePreview() => ApplyColor(normalColor);

    /// <summary>리셋 시 발판 원상복구</summary>
    public void Restore()
    {
        StopAllCoroutines();

        if (_lastPlayerCols != null && _col != null)
            for (int i = 0; i < _lastPlayerCols.Length; i++)
                Physics.IgnoreCollision(_lastPlayerCols[i], _col, false);

        _lastPlayer     = null;
        _lastPlayerCols = null;
        _isDisabled     = false;

        if (_col != null) _col.enabled = true;

        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = true;

        ApplyColor(normalColor);
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator TrapRoutine(Player player)
    {
        _isDisabled = true;

        // 딜레이 동안 잠깐 빨간색으로 피드백
        ApplyColor(Color.red);
        if (trapDisableDelay > 0f)
            yield return new WaitForSeconds(trapDisableDelay);

        // 플레이어 충돌 즉시 해제 → 낙사
        if (player != null && _col != null)
        {
            _lastPlayer     = player;
            _lastPlayerCols = player.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < _lastPlayerCols.Length; i++)
                Physics.IgnoreCollision(_lastPlayerCols[i], _col, true);
        }

        if (_col != null) _col.enabled = false;

        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = false;

        // 하방 임펄스: 자연낙하 대기 없이 즉각 추락
        if (downwardImpulse > 0f && player != null && !player.IsDead)
        {
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
                rb.AddForce(Vector3.down * downwardImpulse, ForceMode.Impulse);
        }

        memoryPath?.OnTrapStepped(this);

        if (trapRespawnDelay > 0f)
        {
            yield return new WaitForSeconds(trapRespawnDelay);
            Restore();
        }
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

    void OnDrawGizmos()
    {
        Color c = role == TileRole.Safe ? previewColor : Color.red;
        c.a = 0.35f;
        Gizmos.color = c;
        Gizmos.DrawCube(transform.position, transform.lossyScale * 0.95f);

        c.a = 1f;
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.01f);
    }
}
