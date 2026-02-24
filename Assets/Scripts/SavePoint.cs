using System.Collections;
using UnityEngine;

/// <summary>
/// 세이브 포인트: 플레이어가 진입하면 리스폰 지점을 갱신하고 체력을 회복합니다.
/// </summary>
public class SavePoint : MonoBehaviour
{
    public enum ActivationMode
    {
        Once,       // 최초 1회만 활성화
        Always,     // 진입할 때마다 재활성화
    }

    [Header("세이브 포인트 설정")]
    [Tooltip("활성화 모드: Once = 1회만, Always = 매번")]
    public ActivationMode activationMode = ActivationMode.Once;

    [Tooltip("활성화 시 체력을 최대로 회복할지 여부")]
    public bool healOnActivate = true;

    [Tooltip("플레이어가 리스폰될 오프셋 (세이브 포인트 기준)")]
    public Vector3 spawnOffset = Vector3.zero;

    [Header("시각 피드백")]
    [Tooltip("비활성 상태 색 (Mesh Renderer가 있는 경우)")]
    public Color inactiveColor = Color.gray;

    [Tooltip("활성화 상태 색")]
    public Color activeColor = Color.cyan;

    [Tooltip("활성화 연출 시간(초). 0이면 즉시 색 전환")]
    public float activateDuration = 0f;

    [Header("레퍼런스 (선택)")]
    [Tooltip("활성화 시 재생할 파티클 (없으면 생략)")]
    public ParticleSystem activateParticle;

    [Tooltip("활성화 시 재생할 사운드 (없으면 생략)")]
    public AudioSource activateSound;

    bool _isActivated;
    MeshRenderer _renderer;
    MaterialPropertyBlock _propBlock;

    void Awake()
    {
        _renderer  = GetComponentInChildren<MeshRenderer>();
        _propBlock = new MaterialPropertyBlock();
        ApplyColor(inactiveColor);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (activationMode == ActivationMode.Once && _isActivated) return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        Activate(player);
    }

    void Activate(Player player)
    {
        _isActivated = true;

        Vector3 respawnPos = transform.position + transform.TransformDirection(spawnOffset);
        player.SetSpawnPoint(respawnPos, player.transform.rotation);

        if (healOnActivate)
            player.heart = player.maxHeart;

        activateParticle?.Play();
        activateSound?.Play();

        if (activateDuration > 0f)
            StartCoroutine(AnimateColor(inactiveColor, activeColor, activateDuration));
        else
            ApplyColor(activeColor);
    }

    IEnumerator AnimateColor(Color from, Color to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ApplyColor(Color.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        ApplyColor(to);
    }

    void ApplyColor(Color color)
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetColor("_BaseColor", color); // URP
        _propBlock.SetColor("_Color", color);      // Built-in Standard
        _renderer.SetPropertyBlock(_propBlock);
    }

    /// <summary>에디터에서 spawnOffset 위치를 항상 표시하는 기즈모</summary>
    void OnDrawGizmos()
    {
        Gizmos.color = _isActivated ? Color.green : Color.gray;
        Vector3 pos = transform.position + transform.TransformDirection(spawnOffset);
        Gizmos.DrawWireSphere(pos, 0.3f);
        Gizmos.DrawLine(transform.position, pos);
    }
}
