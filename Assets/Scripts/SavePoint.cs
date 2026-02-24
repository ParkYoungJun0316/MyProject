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

    [Tooltip(
        "세이브 포인트 진행 순서. 높은 번호를 밟은 뒤에는 낮은 번호로 덮어쓰지 않음.\n" +
        "예) 1번=0, 2번=1, 3번=2 순으로 배치")]
    public int saveOrder = 0;

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

    [Tooltip("쉐이더 색상 프로퍼티명. 비우면 _BaseColor, _Color, _MainColor 순으로 시도")]
    public string colorPropertyName = "";

    bool _isActivated;
    MeshRenderer[] _renderers;

    // SRP Batcher는 MaterialPropertyBlock을 무시하므로 renderer.material로 인스턴스 생성
    Material[] _matInstances;

    static readonly int BaseColorId  = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId      = Shader.PropertyToID("_Color");
    static readonly int MainColorId  = Shader.PropertyToID("_MainColor");

    void Awake()
    {
        _renderers    = GetComponentsInChildren<MeshRenderer>(true);
        _matInstances = new Material[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null)
                _matInstances[i] = _renderers[i].material; // 인스턴스 자동 생성

        ApplyColor(inactiveColor);
    }

    void OnDestroy()
    {
        for (int i = 0; i < _matInstances.Length; i++)
            if (_matInstances[i] != null)
                Destroy(_matInstances[i]);
    }

    void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        // 이미 더 높은 순서의 세이브 포인트를 밟았으면 무시
        if (saveOrder < player.CurrentSaveOrder) return;

        if (activationMode == ActivationMode.Once && _isActivated) return;

        Activate(player);
    }

    void Activate(Player player)
    {
        Vector3 respawnPos = transform.position + transform.TransformDirection(spawnOffset);

        // 리스폰 위치 갱신에 실패하면(더 낮은 순서) 활성화하지 않음
        if (!player.SetSpawnPoint(respawnPos, player.transform.rotation, saveOrder)) return;

        _isActivated = true;

        if (healOnActivate)
            player.heart = player.maxHeart;

        // Unity Object는 fake-null이어서 ?. 연산자가 null 체크를 못 함
        if (activateParticle != null) activateParticle.Play();
        if (activateSound    != null) activateSound.Play();

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
        if (_matInstances == null) return;

        for (int i = 0; i < _matInstances.Length; i++)
        {
            Material mat = _matInstances[i];
            if (mat == null) continue;

            if (!string.IsNullOrEmpty(colorPropertyName))
            {
                mat.SetColor(colorPropertyName, color);
            }
            else if (mat.HasProperty(BaseColorId))
                mat.SetColor(BaseColorId, color);
            else if (mat.HasProperty(ColorId))
                mat.SetColor(ColorId, color);
            else if (mat.HasProperty(MainColorId))
                mat.SetColor(MainColorId, color);
        }
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
