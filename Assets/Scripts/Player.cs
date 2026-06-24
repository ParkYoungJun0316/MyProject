using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour, IDamageReceiver, IPlayerContext
{
    [Header("Identity")]
    [SerializeField] private int playerId = 0;
    [Header("Move")]
    public float speed = 0f;
    public float runMultiplier = 0f;

    public Camera followCamera;

    [Header("Turn")]
    [Tooltip("이동 방향으로 캐릭터가 회전하는 속도 (0 = 즉시). 폴가이즈 느낌: 10~15")]
    public float turnSpeed = 0f;

    [Header("Stat")]
    public int heart;

    public int maxHeart = 0;

    [Header("Black/White Switch")]
    public bool isBlack;
    public float bwCooldown = 0f;
    [Tooltip("isBlack = true일 때 적용되는 색")]
    public Color blackColor = Color.black;
    [Tooltip("isBlack = false일 때 적용되는 색")]
    public Color whiteColor = Color.white;

    [Header("Respawn")]
    public float respawnDelay = 0f;

    [Header("즉사 판정")]
    [Tooltip("이 수치 이상의 데미지를 받으면 Jammed 애니메이션 재생. 0이면 비활성")]
    public int instantKillThreshold = 0;

    [Header("피격 무적")]
    [Tooltip("피격 후 이 시간(초) 동안 모든 출처의 데미지 무시. 0이면 0.5초(기존 첫 무적 구간)")]
    [SerializeField] float damageInvulnerabilityDuration = 0f;

    [Header("고유색 (캐릭터 선택 시 결정)")]
    [Tooltip("이 플레이어의 고유색. Inspector에서 원하는 색으로 설정.")]
    public Color uniqueColor = Color.blue;
    public bool isUniqueColor;
    [Tooltip("오브젝트 색상 소유권(상자·바닥 등) 판별에 사용되는 고유색 타입")]
    public PlayerColorType playerColorType = PlayerColorType.Blue;

    [Header("추락 사망")]
    [Tooltip("체크 시 Y 좌표가 fallDeathY 이하로 내려가면 즉시 사망")]
    public bool enableFallDeath = false;
    [Tooltip("사망 기준 Y 좌표. enableFallDeath가 켜진 경우에만 적용")]
    public float fallDeathY = 0f;
    [Tooltip("Fall 애니메이션 시작 Y 좌표. fallDeathY보다 높게 설정 (예: fallDeathY=-10 이면 -5 정도). enableFallDeath가 켜진 경우에만 적용")]
    public float fallAnimY = 0f;

    [HideInInspector] public float moveSpeedMultiplier = 1f;

    public bool IsDead   { get; private set; }
    public int PlayerId => playerId;

    [HideInInspector] public Vector2 moveInput;
    Vector3 moveVec;

    bool isDamage;
    bool isKnockback;

    bool bwDown, altDown;

    // 낙사 Fall 애니메이션이 이미 재생됐는지 추적 (매 프레임 중복 트리거 방지)
    bool fallAnimTriggered = false;
    // 즉사 판정 시 Die()에서 doJammed 재생 여부 결정
    bool isInstantKill = false;

    float nextBWTime = 0f;

    Rigidbody rigid;
    Animator anim;

    Vector3 spawnPos;
    Quaternion spawnRot;
    int _currentSaveOrder = int.MinValue; // 현재 활성화된 세이브 포인트의 순서

    int normalLayer;
    int deadLayer;
    Collider[] cols;
    float fixedY;

    PlayerEvents events;
    PlayerStealth playerStealth;
    PlayerBuffSystem playerBuffSystem;


    public void OnMove(InputValue value)
    {
        if (IsDead) return;
        moveInput = value.Get<Vector2>();
    }

    void Awake()
    {
        rigid = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

        spawnPos = transform.position;
        spawnRot = transform.rotation;

        normalLayer = LayerMask.NameToLayer("Player");
        deadLayer = LayerMask.NameToLayer("PlayerDead");

        cols = GetComponentsInChildren<Collider>(true);

        events = GetComponent<PlayerEvents>();
        if (events == null) events = gameObject.AddComponent<PlayerEvents>();

        playerStealth = GetComponent<PlayerStealth>();

        playerBuffSystem = GetComponent<PlayerBuffSystem>();
        if (playerBuffSystem == null) playerBuffSystem = gameObject.AddComponent<PlayerBuffSystem>();

        events.RaiseBlackWhiteChanged(isBlack);
    }

    void Update()
    {
        if (!IsDead && enableFallDeath)
        {
            float y = transform.position.y;
            // fallAnimY 통과 시 Fall 애니메이션 1회 재생 (fallDeathY보다 높은 지점에서 미리 트리거)
            if (!fallAnimTriggered && y < fallAnimY)
            {
                fallAnimTriggered = true;
                anim?.SetTrigger("doFall");
                events?.RaiseFallDeath();
            }
            if (y < fallDeathY)
                Die();
        }

        if (IsDead)
        {
            Vector3 p = transform.position;
            p.y = fixedY;
            transform.position = p;
            return;
        }

        GetInput();

        if (bwDown && Time.time >= nextBWTime)
        {
            nextBWTime = Time.time + bwCooldown;
            if (isUniqueColor)
            {
                isUniqueColor = false;
                events?.RaiseUniqueColorChanged(-1);
            }
            isBlack = !isBlack;
            events?.RaiseBlackWhiteChanged(isBlack);
            anim?.SetTrigger("doChangeColor");
        }

        if (altDown)
        {
            isUniqueColor = !isUniqueColor;
            events?.RaiseUniqueColorChanged(isUniqueColor ? 0 : -1);
            anim?.SetTrigger("doChangeColor");
        }
    }

    void FixedUpdate()
    {
        if (IsDead)
        {
            rigid.linearVelocity = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
            return;
        }

        Move();
        Turn();
        FreezeRotation();
    }

    void GetInput()
    {
        bwDown  = Keyboard.current.leftCtrlKey.wasPressedThisFrame;
        altDown = Keyboard.current.leftAltKey.wasPressedThisFrame;
    }

    void Move()
    {
        if (isKnockback) return;

        // 카메라의 수평 forward/right 기준으로 이동 방향 계산 (폴가이즈 스타일)
        Vector3 camForward = (followCamera != null)
            ? Vector3.ProjectOnPlane(followCamera.transform.forward, Vector3.up).normalized
            : Vector3.forward;
        Vector3 camRight = (followCamera != null)
            ? Vector3.ProjectOnPlane(followCamera.transform.right, Vector3.up).normalized
            : Vector3.right;

        moveVec = (camForward * moveInput.y + camRight * moveInput.x).normalized;

        bool hasMove = moveVec.sqrMagnitude > 0.0001f;

        float speedBonus = playerBuffSystem != null
            ? playerBuffSystem.GetValue(PlayerBuffSystem.BuffType.SpeedUp)
            : 0f;
        float finalSpeed = (speed * runMultiplier + speedBonus) * moveSpeedMultiplier;

        Vector3 v = rigid.linearVelocity;
        v.x = moveVec.x * finalSpeed;
        v.z = moveVec.z * finalSpeed;
        rigid.linearVelocity = v;

        if (anim != null)
            anim.SetBool("isRun", hasMove);
    }

    void Turn()
    {
        // 이동 입력이 없으면 마지막 방향 유지 (폴가이즈 스타일)
        if (moveVec.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(moveVec);
        if (turnSpeed > 0f)
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
        else
            transform.forward = moveVec;
    }

    void FreezeRotation()
    {
        rigid.angularVelocity = Vector3.zero;
    }

    /// <summary>버프·무적 무시 즉사. Die()에서 doJammed 연동.</summary>
    public void KillInstantly()
    {
        if (IsDead) return;
        isInstantKill = true;
        Die();
    }

    /// <summary>고유색 모드면 uniqueColor, 아니면 blackColor/whiteColor.</summary>
    public Color GetCurrentBaseColor()
    {
        if (isUniqueColor) return uniqueColor;
        return isBlack ? blackColor : whiteColor;
    }

    /// <summary>색 전환 쿨다운 남은 시간(초). 0이면 사용 가능.</summary>
    public float GetBWCooldownRemaining() => Mathf.Max(0f, nextBWTime - Time.time);

    /// <summary>무적·피격 쿨 중이면 false, 실제 피격 시 true.</summary>
    public bool TryTakeDamage(int amount, bool knockback = false)
    {
        if (IsDead) return false;
        if (isDamage) return false;

        if (playerBuffSystem != null && playerBuffSystem.IsActive(PlayerBuffSystem.BuffType.Invincibility))
            return false;

        TakeDamage(amount, knockback);
        return true;
    }

    public void TakeDamage(int amount, bool knockback = false)
    {
        if (IsDead) return;
        if (isDamage) return;

        if (playerBuffSystem != null && playerBuffSystem.IsActive(PlayerBuffSystem.BuffType.Invincibility))
            return;

        isInstantKill = instantKillThreshold > 0 && amount >= instantKillThreshold;

        heart -= amount;
        events?.RaiseDamaged(knockback);

        playerStealth?.RevealTemporarily();

        if (heart <= 0) { Die(); return; }
        anim?.SetTrigger("doHit");
        StartCoroutine(OnDamage(knockback));
    }

    public void ReceiveDamage(int amount, object source)
    {
        TakeDamage(amount, false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsDead) return;

        if (other.CompareTag("EnemyBullet"))
        {
            Bullet enemyBullet = other.GetComponent<Bullet>();
            if (enemyBullet != null)
                TakeDamage(enemyBullet.damage, false);

            if (other.GetComponent<Rigidbody>() != null)
                Destroy(other.gameObject);
        }
    }

    // ── 사망 / 리스폰 ─────────────────────────────────────────

    IEnumerator OnDamage(bool isBossAtk)
    {
        isDamage = true;

        if (isBossAtk)
        {
            isKnockback = true;
            rigid.AddForce(transform.forward * -25, ForceMode.Impulse);
        }

        float invuln = damageInvulnerabilityDuration > 0f ? damageInvulnerabilityDuration : 0.5f;
        yield return new WaitForSeconds(invuln);
        isDamage = false;

        if (isBossAtk)
        {
            rigid.linearVelocity = Vector3.zero;
            isKnockback = false;
        }
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        CancelInvoke();

        isKnockback = false; isDamage = false;
        moveSpeedMultiplier  = 1f;

        if (playerStealth != null)
            playerStealth.ForceLayer(deadLayer);
        else
            SetLayerRecursively(gameObject, deadLayer);

        if (cols != null)
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) cols[i].enabled = false;

        moveInput = Vector2.zero;
        rigid.linearVelocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;
        fixedY = transform.position.y;
        rigid.isKinematic = true;

        if (anim != null)
        {
            anim.SetBool("isRun", false);
            anim.ResetTrigger("doDie");
            anim.ResetTrigger("doJammed");
            anim.ResetTrigger("doFall");
            anim.SetTrigger(isInstantKill ? "doJammed" : "doDie");
        }
        if (isInstantKill) events?.RaiseInstantKilled();
        isInstantKill = false;
        events?.RaiseDied();
        StartCoroutine(RespawnAfter(respawnDelay));
    }

    IEnumerator RespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Respawn();
    }

    public int CurrentSaveOrder => _currentSaveOrder;

    /// <summary>order가 CurrentSaveOrder보다 작으면 무시.</summary>
    public bool SetSpawnPoint(Vector3 pos, Quaternion rot, int order)
    {
        if (order < _currentSaveOrder) return false;
        _currentSaveOrder = order;
        spawnPos = pos;
        spawnRot = rot;
        return true;
    }

    public void ForceSetSpawnPoint(Vector3 pos, Quaternion rot)
    {
        _currentSaveOrder = int.MinValue;
        spawnPos = pos;
        spawnRot = rot;
    }

    public void ForceRespawn(Vector3 pos, Quaternion rot)
    {
        ForceSetSpawnPoint(pos, rot);
        if (!IsDead) Respawn();
    }

    public void Respawn()
    {
        CancelInvoke();
        transform.SetPositionAndRotation(spawnPos, spawnRot);
        heart = maxHeart;

        rigid.isKinematic = false;
        rigid.linearVelocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;

        IsDead = false;
        isDamage = false; isKnockback = false;
        moveSpeedMultiplier  = 1f;
        fallAnimTriggered    = false;
        isInstantKill        = false;

        if (playerStealth == null)
            SetLayerRecursively(gameObject, normalLayer);

        if (cols != null)
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) cols[i].enabled = true;

        if (anim != null)
        {
            anim.ResetTrigger("doDie");
            anim.ResetTrigger("doFall");
            anim.ResetTrigger("doJammed");
            anim.SetBool("isRun", false);
            anim.Play("Idle", 0, 0f);
            anim.Update(0f);
        }

        isUniqueColor = false;
        events?.RaiseUniqueColorChanged(-1);
        events?.RaiseRespawned();
        events?.RaiseBlackWhiteChanged(isBlack);
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        for (int i = 0; i < obj.transform.childCount; i++)
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
    }
}
