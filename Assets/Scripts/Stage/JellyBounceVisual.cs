using UnityEngine;

/// <summary>
/// 튕김 젤리(T.Stage3 Jumper) 연출. 누가 윗면에 닿으면 방울이 납작하게 눌렸다가 튀어 오르며 펴진다.
///
/// [동작]
///  - 판정·튕김은 같은 오브젝트의 콜라이더와 ContactKnockback 그대로다. 이 컴포넌트는 자식 비주얼만 움직인다
///    (콜라이더가 붙은 트랜스폼을 줄이면 튕김 판정 모양까지 흔들린다).
///  - 원격 플레이어는 ClientNetworkTransform으로 움직여 이 머신에서 충돌 이벤트가 안 불릴 수 있다.
///    그래서 활성 플레이어의 발(몸 콜라이더 맨 아래)이 윗면 근처에 들어왔는지 거리로 감지한다.
///  - 각 머신 로컬 연출 — RPC 없음.
///
/// [씬 설정]
///  1. 튕김 오브젝트(콜라이더 + ContactKnockback)에 추가, 자기 MeshRenderer는 끈다
///  2. 같은 메시·재질의 자식을 만들어 visual 에 연결
/// </summary>
public class JellyBounceVisual : MonoBehaviour
{
    [Tooltip("눌렸다 펴질 자식 비주얼")]
    [SerializeField] Transform visual;

    [Tooltip("눌림 한 번의 길이(초)")]
    [SerializeField] float squashDuration = 0.35f;

    [Tooltip("가장 눌렸을 때 높이가 줄어드는 비율 (0.3 = 30%)")]
    [SerializeField] float squashAmount = 0.3f;

    [Tooltip("발이 윗면보다 이 높이(m) 안에 있으면 닿은 것으로 본다")]
    [SerializeField] float touchHeight = 0.6f;

    [Tooltip("다시 눌릴 수 있기까지 대기(초). ContactKnockback 간격(0.25초)과 비슷하게.")]
    [SerializeField] float cooldown = 0.3f;

    Collider _col;
    Vector3  _baseScale;
    float    _elapsed = -1f;
    float    _nextAllowed;

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (visual != null) _baseScale = visual.localScale;
    }

    void Update()
    {
        if (Time.time >= _nextAllowed && IsTouched())
        {
            _elapsed     = 0f;
            _nextAllowed = Time.time + cooldown;
        }

        if (_elapsed < 0f || visual == null) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / squashDuration);

        // 처음엔 눌리고(1 아래), 뒤에선 살짝 솟았다가(1 위) 1로 돌아온다.
        float y  = 1f - squashAmount * Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
        float xz = 1f + (1f - y) * 0.5f;
        visual.localScale = new Vector3(_baseScale.x * xz, _baseScale.y * y, _baseScale.z * xz);

        if (t >= 1f)
        {
            visual.localScale = _baseScale;
            _elapsed = -1f;
        }
    }

    bool IsTouched()
    {
        GameSession session = GameSession.Instance;
        if (session == null || _col == null) return false;

        Bounds b = _col.bounds;
        float r  = Mathf.Min(b.extents.x, b.extents.z);

        foreach (Player p in session.GetActivePlayers())
        {
            if (p == null) continue;
            Collider body = p.GetComponent<Collider>();
            if (body == null) continue;

            Bounds pb = body.bounds;
            float dx = pb.center.x - b.center.x, dz = pb.center.z - b.center.z;
            if (dx * dx + dz * dz > r * r) continue;

            float dy = pb.min.y - b.max.y;
            if (dy >= -0.5f && dy <= touchHeight) return true;
        }
        return false;
    }
}
