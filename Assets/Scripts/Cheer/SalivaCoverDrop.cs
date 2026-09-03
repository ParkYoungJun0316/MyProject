using UnityEngine;

/// <summary>
/// 침 Cover 연출용 로컬 낙하 오브젝트. 데미지·NGO 없음.
/// SalivaHazard가 Cover 때 스폰하고 Recover 때 정리한다.
/// Rigidbody가 있으면 중력/비키네마틱을 끄고 이 스크립트가 Y만 내린다.
/// </summary>
public class SalivaCoverDrop : MonoBehaviour
{
    [SerializeField] float fallSpeed = 10f;
    [SerializeField] float maxLifetime = 4f;

    Rigidbody _rb;
    float _floorY;
    float _elapsed;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        FreezePhysics();
    }

    public void Init(float floorY, float speed = -1f)
    {
        FreezePhysics();
        _floorY = floorY;
        if (speed > 0f)
            fallSpeed = speed;
        float dist = Mathf.Max(0.01f, transform.position.y - floorY);
        maxLifetime = dist / Mathf.Max(0.01f, fallSpeed) + 0.5f;
    }

    void FreezePhysics()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody>();
        if (_rb == null) return;
        _rb.useGravity = false;
        _rb.isKinematic = true;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        transform.position += Vector3.down * (fallSpeed * dt);
        _elapsed += dt;

        if (_elapsed >= maxLifetime || transform.position.y <= _floorY)
            Destroy(gameObject);
    }
}
