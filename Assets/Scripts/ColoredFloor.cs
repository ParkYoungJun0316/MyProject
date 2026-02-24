using System.Collections;
using UnityEngine;

/// <summary>
/// 색상 바닥 타일.
/// floorColor에 해당하는 플레이어는 정상적으로 밟을 수 있음.
/// 다른 색 플레이어가 접촉하면 충돌을 즉각 해제하고 타일을 비활성화해 추락시킴.
/// </summary>
public class ColoredFloor : MonoBehaviour
{
    [Header("바닥 색상")]
    [Tooltip("이 바닥의 고유색. 해당 색 플레이어만 밟을 수 있음")]
    public PlayerColorType floorColor = PlayerColorType.Blue;

    [Header("비활성화 설정")]
    [Tooltip("잘못된 색 플레이어 접촉 후 비활성화까지 지연 시간(초). 0 = 즉시")]
    public float disableDelay = 0f;

    [Tooltip("비활성화 후 자동 복구 시간(초). 0 = 복구 없음 (영구 비활성)")]
    public float respawnDelay = 0f;

    [Header("추락 보조")]
    [Tooltip(
        "감지 즉시 플레이어 Rigidbody에 가할 하방 임펄스.\n" +
        "0 = 자연낙하만 / 권장: 5~10 (즉각 추락 보장)")]
    public float downwardImpulse = 0f;

    [Header("시각 피드백")]
    [Tooltip("Inspector에서 설정한 색으로 바닥 색상을 적용. 투명(Alpha=0)이면 적용 안 함")]
    public Color tileColor = Color.clear;

    Collider  _col;
    Renderer  _rend;
    MaterialPropertyBlock _mpb;
    bool _isDisabling;

    // 복구 시 Physics.IgnoreCollision 원상복구를 위해 보관
    Player   _lastPlayer;
    Collider[] _lastPlayerCols;

    void Awake()
    {
        _col  = GetComponent<Collider>();
        _rend = GetComponent<Renderer>();
        _mpb  = new MaterialPropertyBlock();

        if (tileColor.a > 0f)
            ApplyColor(tileColor);
    }

    void OnCollisionEnter(Collision col)
    {
        if (_isDisabling) return;

        Player player = col.transform.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        // 올바른 색 플레이어 → 바닥 유지
        if (player.playerColorType == floorColor) return;

        StartCoroutine(DisableRoutine(player));
    }

    IEnumerator DisableRoutine(Player player)
    {
        _isDisabling = true;

        if (disableDelay > 0f)
            yield return new WaitForSeconds(disableDelay);

        // ① Physics.IgnoreCollision: 현재 프레임에서 즉각 충돌 해제
        //    (enabled=false 단독 사용 시 다음 Physics 프레임까지 충돌이 유지되는 문제 해결)
        if (player != null && _col != null)
        {
            _lastPlayer     = player;
            _lastPlayerCols = player.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < _lastPlayerCols.Length; i++)
                Physics.IgnoreCollision(_lastPlayerCols[i], _col, true);
        }

        // ② 콜라이더·렌더러 비활성화
        if (_col  != null) _col.enabled  = false;
        if (_rend != null) _rend.enabled = false;

        // ③ 하방 임펄스: 인접 타일에 걸린 CapsuleCollider를 즉각 낙하시킴
        if (downwardImpulse > 0f && player != null && !player.IsDead)
        {
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
                rb.AddForce(Vector3.down * downwardImpulse, ForceMode.Impulse);
        }

        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
            Restore();
        }
    }

    void Restore()
    {
        if (_col  != null) _col.enabled  = true;
        if (_rend != null) _rend.enabled = true;

        // Physics.IgnoreCollision 해제: 복구된 타일과 다시 충돌 가능하게
        if (_lastPlayerCols != null && _col != null)
            for (int i = 0; i < _lastPlayerCols.Length; i++)
                Physics.IgnoreCollision(_lastPlayerCols[i], _col, false);

        _lastPlayer     = null;
        _lastPlayerCols = null;
        _isDisabling    = false;

        if (tileColor.a > 0f)
            ApplyColor(tileColor);
    }

    void ApplyColor(Color color)
    {
        if (_rend == null) return;
        _rend.GetPropertyBlock(_mpb);
        _mpb.SetColor("_BaseColor", color); // URP
        _mpb.SetColor("_Color",     color); // Built-in Standard
        _rend.SetPropertyBlock(_mpb);
    }

    void OnDrawGizmos()
    {
        Color gizmoCol = tileColor.a > 0f ? tileColor : Color.cyan;
        gizmoCol.a = 0.4f;
        Gizmos.color = gizmoCol;
        Gizmos.DrawCube(transform.position, transform.lossyScale * 0.99f);

        gizmoCol.a = 1f;
        Gizmos.color = gizmoCol;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.01f);
    }
}
