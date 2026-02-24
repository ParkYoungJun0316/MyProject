using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Move")]
    public float speed = 0f;
    public float runMultiplier = 0f;

    [Header("Grenade Throw")]
    public GameObject grenadeObj;
    [Tooltip("최소 투척 힘 (살짝 눌렀을 때)")]
    public float grenadeMinForce = 0f;
    [Tooltip("최대 투척 힘 (꽉 눌렀을 때)")]
    public float grenadeMaxForce = 0f;
    [Tooltip("최대 차징 도달 시간(초). 0이면 항상 최대 힘")]
    public float grenadeMaxChargeTime = 0f;
    [Tooltip("투척 각도(도). 45 = 가장 멀리, 0 = 수평")]
    public float grenadeThrowAngle = 0f;
    public Vector3 grenadeSpawnOffset = Vector3.zero;
    public Collider[] ownerColliders;

    [Header("Grenade Explosion")]
    [Tooltip("폭발까지 걸리는 시간(초)")]
    public float grenadeFuseTime = 0f;
    [Tooltip("폭발 반경")]
    public float grenadeExplosionRadius = 0f;
    [Tooltip("폭발 데미지")]
    public int grenadeExplosionDamage = 0;
    [Tooltip("폭발 대상 레이어")]
    public LayerMask grenadeExplosionMask;
    [Tooltip("폭발 이펙트 프리팹 (선택)")]
    public GameObject grenadeExplosionEffect;

    [Header("Potion Drink")]
    [Tooltip("포션을 마시는 데 걸리는 시간(초). 0이면 즉시")]
    public float potionDrinkDuration = 0f;

    public Camera followCamera;
    public LayerMask aimMask;

    [Header("Stat")]
    public int coin;
    public int heart;

    public int maxCoin = 0;
    public int maxHeart = 0;

    [Header("Stamina")]
    public float maxStamina = 0f;
    [Tooltip("초당 달리기 스테미나 소모량")]
    public float staminaDrainRate = 0f;
    [Tooltip("초당 스테미나 충전량")]
    public float staminaRechargeRate = 0f;
    [Tooltip("달리기/닷지 후 충전 시작까지 딜레이(초)")]
    public float staminaRechargeDelay = 0f;
    [Tooltip("닷지 1회 스테미나 소모량")]
    public float dodgeStaminaCost = 0f;

    [Header("Stamina (Runtime)")]
    [Tooltip("현재 스테미나 (런타임 확인용)")]
    public float currentStamina;
    float staminaRechargeTimer;
    bool isStaminaDraining;

    [Header("Jump / Dodge")]
    public float dodgeForce = 0f;

    [Header("Black/White Switch")]
    public bool isBlack;
    public float bwCooldown = 0f;

    [Header("Action Cooldown")]
    public float actionCooldown = 0f;

    [Header("Dodge i-frame")]
    public float dodgeInvincibleDuration = 0f;
    float dodgeInvincibleUntil = 0f;

    [Header("Respawn")]
    public float respawnDelay = 0f;

    [Header("힘 (캐릭터별 설정)")]
    [Tooltip("캐릭터 힘. 높을수록 무거운 상자도 빠르게 밀고 당김. (마른이 < 보통이 < 뚱뚱이 권장)")]
    public float strength = 0f;

    [Header("고유색 (캐릭터 선택 시 결정)")]
    [Tooltip("이 플레이어의 고유색. 캐릭터 선택 화면에서 1인당 1색을 배정. (테스트: 파란색)")]
    public Color uniqueColor = Color.blue;
    public bool isUniqueColor;

    [HideInInspector] public float moveSpeedMultiplier = 1f;

    [Header("좌클릭 우선순위")]
    [Tooltip(
        "아이템 사용의 좌클릭 우선순위 (기본: 10).\n" +
        "BoxInteraction.interactionPriority(기본 0)보다 높으면 박스 우선.\n" +
        "새 상호작용 추가 시 이 값과 비교하는 interactionPriority 필드를 해당 컴포넌트에 추가.")]
    public int itemUsePriority = 10;

    public bool IsDead { get; private set; }

    public Vector2 moveInput;
    Vector3 moveVec;

    bool isGrounded;
    bool isJumping;
    bool isDodging;
    bool isDamage;
    bool isKnockback;

    bool sDown1, sDown2, sDown3, sDown4, sDown5;
    bool bwDown, dDown, altDown;

    bool  grenadeHeld       = false;
    float grenadeChargeTime = 0f;
    float potionDrinkTimer  = 0f;
    int   prevSelectedSlot  = -1;
    bool  requiresInputRelease = false;

    float nextActionTime = 0f;
    float nextBWTime = 0f;

    Rigidbody rigid;
    Animator anim;

    Vector3 spawnPos;
    Quaternion spawnRot;

    int normalLayer;
    int deadLayer;
    Collider[] cols;
    float fixedY;

    PlayerEvents events;
    PlayerStealth playerStealth;
    PlayerItemInventory playerItemInventory;
    PlayerBuffSystem playerBuffSystem;
    BoxInteraction boxInteraction;


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

        playerItemInventory = GetComponent<PlayerItemInventory>();
        if (playerItemInventory == null) playerItemInventory = gameObject.AddComponent<PlayerItemInventory>();

        playerBuffSystem = GetComponent<PlayerBuffSystem>();
        if (playerBuffSystem == null) playerBuffSystem = gameObject.AddComponent<PlayerBuffSystem>();

        boxInteraction = GetComponent<BoxInteraction>();

        events.RaiseBlackWhiteChanged(isBlack);

        currentStamina = maxStamina;
    }

    void Update()
    {
        if (IsDead)
        {
            Vector3 p = transform.position;
            p.y = fixedY;
            transform.position = p;
            return;
        }

        GetInput();
        UpdateStamina();
        Dodge();
        HandleItemSlotInput();
        UseItem();

        if (bwDown && Time.time >= nextBWTime)
        {
            nextBWTime = Time.time + bwCooldown;
            // 고유색 모드일 때 Ctrl → 고유색 해제 후 흑/백 전환
            if (isUniqueColor)
            {
                isUniqueColor = false;
                events?.RaiseUniqueColorChanged(-1);
            }
            isBlack = !isBlack;
            events?.RaiseBlackWhiteChanged(isBlack);
        }

        // Alt: 고유색 모드 토글
        if (altDown)
        {
            isUniqueColor = !isUniqueColor;
            events?.RaiseUniqueColorChanged(isUniqueColor ? 0 : -1);
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
        sDown1 = Keyboard.current.digit1Key.wasPressedThisFrame;
        sDown2 = Keyboard.current.digit2Key.wasPressedThisFrame;
        sDown3 = Keyboard.current.digit3Key.wasPressedThisFrame;
        sDown4 = Keyboard.current.digit4Key.wasPressedThisFrame;
        sDown5 = Keyboard.current.digit5Key.wasPressedThisFrame;

        dDown   = Keyboard.current.spaceKey.wasPressedThisFrame;
        bwDown  = Keyboard.current.leftCtrlKey.wasPressedThisFrame;
        altDown = Keyboard.current.leftAltKey.wasPressedThisFrame;
    }

    void Move()
    {
        if (isKnockback) return;

        if (followCamera != null)
            moveVec = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;
        else
            moveVec = new Vector3(moveInput.x, 0, moveInput.y).normalized;

        bool hasMove = moveVec.sqrMagnitude > 0.0001f;
        bool walkKey = Keyboard.current.leftShiftKey.isPressed;

        // 스테미나가 없으면 강제 걷기
        bool wantsRun = hasMove && !walkKey;
        bool canRun = wantsRun && currentStamina > 0f;

        if (canRun)
        {
            currentStamina -= staminaDrainRate * Time.fixedDeltaTime;
            if (currentStamina < 0f) currentStamina = 0f;
            isStaminaDraining = true;
            staminaRechargeTimer = 0f;
        }
        else if (hasMove && wantsRun)
        {
            // 달리려 했지만 스테미나 부족 → 걷기로 전환
            walkKey = true;
        }

        float baseSpeed  = speed * (walkKey || !canRun && wantsRun ? 1f : runMultiplier);
        float speedBonus = playerBuffSystem != null
            ? playerBuffSystem.GetValue(PlayerBuffSystem.BuffType.SpeedUp)
            : 0f;
        float finalSpeed = (baseSpeed + speedBonus) * moveSpeedMultiplier;

        Vector3 v = rigid.linearVelocity;
        v.x = moveVec.x * finalSpeed;
        v.z = moveVec.z * finalSpeed;
        rigid.linearVelocity = v;

        bool actuallyRunning = hasMove && canRun;
        bool actuallyWalking = hasMove && !actuallyRunning;

        if (anim != null)
        {
            anim.SetBool("isWalk", actuallyWalking);
            anim.SetBool("isRun",  actuallyRunning);
        }
    }

    void Turn()
    {
        if (followCamera == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = followCamera.ScreenPointToRay(mousePos);
        var floorPlane = new Plane(Vector3.up, transform.position);

        if (floorPlane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            Vector3 dir = hit - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.forward = dir.normalized;
        }
    }

    void FreezeRotation()
    {
        rigid.angularVelocity = Vector3.zero;
    }

    // ── 스테미나 ─────────────────────────────────────────────

    void UpdateStamina()
    {
        if (maxStamina <= 0f) return;

        // 무한 스테미나 버프 활성 중 → 소모/딜레이 없이 풀 충전 유지
        if (playerBuffSystem != null && playerBuffSystem.IsActive(PlayerBuffSystem.BuffType.InfiniteStamina))
        {
            currentStamina = maxStamina;
            isStaminaDraining = false;
            staminaRechargeTimer = 0f;
            return;
        }

        bool running = moveVec.sqrMagnitude > 0.0001f
                       && !Keyboard.current.leftShiftKey.isPressed
                       && currentStamina > 0f;

        if (running || isDodging)
        {
            // 소모 중에는 충전 딜레이 타이머를 계속 초기화
            isStaminaDraining = true;
            staminaRechargeTimer = 0f;
            return;
        }

        // 소모 종료 → 딜레이 카운트 시작
        if (isStaminaDraining)
        {
            staminaRechargeTimer += Time.deltaTime;
            if (staminaRechargeTimer >= staminaRechargeDelay)
                isStaminaDraining = false;
            return;
        }

        // 충전
        if (currentStamina < maxStamina)
        {
            currentStamina += staminaRechargeRate * Time.deltaTime;
            if (currentStamina > maxStamina)
                currentStamina = maxStamina;
        }
    }

    // ── 아이템 슬롯 ─────────────────────────────────────────

    void HandleItemSlotInput()
    {
        if (playerItemInventory == null) return;
        if (sDown1)      playerItemInventory.SelectSlot(0);
        else if (sDown2) playerItemInventory.SelectSlot(1);
        else if (sDown3) playerItemInventory.SelectSlot(2);
        else if (sDown4) playerItemInventory.SelectSlot(3);
        else if (sDown5) playerItemInventory.SelectSlot(4);
    }

    void UseItem()
    {
        if (playerItemInventory == null) return;

        int curSlot = playerItemInventory.SelectedSlot;

        // 슬롯 전환 시 진행 중인 차징/마시기 초기화
        if (curSlot != prevSelectedSlot)
        {
            grenadeHeld          = false;
            grenadeChargeTime    = 0f;
            potionDrinkTimer     = 0f;
            prevSelectedSlot     = curSlot;
            // 버튼 누른 채로 슬롯을 바꿨다면 뗐다가 다시 눌러야 함
            // (예: 포션 마시는 중 수류탄 슬롯으로 바꿔도 즉시 던지지 않음)
            requiresInputRelease = Mouse.current.leftButton.isPressed;
        }

        // 버튼 해제 대기: 아직 누르고 있으면 아이템 사용 차단
        if (requiresInputRelease)
        {
            if (!Mouse.current.leftButton.isPressed) requiresInputRelease = false;
            else return;
        }

        // 차징 중이면 release 이벤트 감지를 위해 SelectedSlotHasItem 체크 전에 처리
        if (grenadeHeld)
        {
            HandleGrenadeRelease();
            return;
        }

        if (!playerItemInventory.SelectedSlotHasItem()) return;
        if (isDodging || isJumping) return;

        // 우선순위 체크: 박스 상호작용이 이번 프레임 좌클릭을 소비했으면 아이템 사용 건너뜀
        // 새 상호작용 추가 시: 해당 컴포넌트의 interactionPriority와 itemUsePriority 비교
        if (boxInteraction != null
            && boxInteraction.BlockingMouseInput
            && boxInteraction.interactionPriority <= itemUsePriority)
            return;

        switch (playerItemInventory.GetSelectedType())
        {
            case PlayerItemInventory.ConsumableType.Grenade:
                HandleGrenadeInput();
                break;
            case PlayerItemInventory.ConsumableType.HealthPotion:
                HandlePotionInput();
                break;
        }
    }

    void HandleGrenadeInput()
    {
        // 좌클릭 누름 → 차징 시작
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            grenadeHeld       = true;
            grenadeChargeTime = 0f;
        }
    }

    void HandleGrenadeRelease()
    {
        bool lmbHeld     = Mouse.current.leftButton.isPressed;
        bool lmbReleased = Mouse.current.leftButton.wasReleasedThisFrame;

        if (lmbHeld)
        {
            float cap = grenadeMaxChargeTime > 0f ? grenadeMaxChargeTime : 1f;
            grenadeChargeTime = Mathf.Min(grenadeChargeTime + Time.deltaTime, cap);
            return;
        }

        if (lmbReleased)
        {
            float force;
            if (grenadeMaxChargeTime <= 0f)
            {
                force = grenadeMaxForce;
            }
            else
            {
                float t = Mathf.Clamp01(grenadeChargeTime / grenadeMaxChargeTime);
                force = Mathf.Lerp(grenadeMinForce, grenadeMaxForce, t);
            }

            if (ThrowGrenadeWithForce(force))
                playerItemInventory.ConsumeSelected();
        }

        grenadeHeld       = false;
        grenadeChargeTime = 0f;
    }

    void HandlePotionInput()
    {
        bool holding = Mouse.current.leftButton.isPressed;

        if (!holding)
        {
            potionDrinkTimer = 0f;
            return;
        }

        potionDrinkTimer += Time.deltaTime;

        if (potionDrinkTimer >= potionDrinkDuration)
        {
            if (HealFromPotion(playerItemInventory.healAmount))
                playerItemInventory.ConsumeSelected();

            potionDrinkTimer = 0f;
        }
    }

    // ── 공개 메서드 ──────────────────────────────────────────

    /// <summary> 체력 포션 사용 </summary>
    public bool HealFromPotion(int amount)
    {
        if (IsDead) return false;
        if (heart >= maxHeart) return false;
        heart = Mathf.Min(maxHeart, heart + amount);
        return true;
    }

    /// <summary>
    /// 차징 힘으로 수류탄 투척.
    /// grenadeThrowAngle(도)로 투척 각도 결정, force로 힘 크기 결정.
    /// 생성 시 Item 컴포넌트/Item 태그 제거 + GrenadeExplosion 부착.
    /// </summary>
    bool ThrowGrenadeWithForce(float force)
    {
        if (grenadeObj == null) return false;

        // 수평 방향: 마우스 → 바닥 or 전방 fallback
        Vector3 flatDir = transform.forward;
        if (followCamera != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = followCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f, aimMask))
            {
                Vector3 toTarget = hit.point - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                    flatDir = toTarget.normalized;
            }
        }

        // 투척 각도 적용 (grenadeThrowAngle 도 → 정규화된 방향)
        float rad = grenadeThrowAngle * Mathf.Deg2Rad;
        Vector3 throwDir = (flatDir * Mathf.Cos(rad) + Vector3.up * Mathf.Sin(rad)).normalized;

        Vector3 spawnPosG = transform.position + transform.TransformDirection(grenadeSpawnOffset);

        GameObject instantGrenade = Instantiate(grenadeObj, spawnPosG, Quaternion.identity);

        // 던진 수류탄을 다시 먹지 못하도록 Item 컴포넌트/태그 제거
        Item itemComp = instantGrenade.GetComponent<Item>();
        if (itemComp != null) Destroy(itemComp);
        if (instantGrenade.CompareTag("Item"))
            instantGrenade.tag = "Untagged";

        // GrenadeExplosion 부착
        GrenadeExplosion explosion = instantGrenade.GetComponent<GrenadeExplosion>();
        if (explosion == null) explosion = instantGrenade.AddComponent<GrenadeExplosion>();
        explosion.fuseTime        = grenadeFuseTime;
        explosion.explosionRadius = grenadeExplosionRadius;
        explosion.explosionDamage = grenadeExplosionDamage;
        explosion.damageMask      = grenadeExplosionMask;
        explosion.explosionEffect = grenadeExplosionEffect;

        // 던진 수류탄만 바닥과 물리 충돌하도록 Trigger 해제 (바닥에 놓인 픽업용 아이템은 Trigger 유지 → OnTriggerEnter로 먹음)
        Collider[] allCols = instantGrenade.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < allCols.Length; i++)
            allCols[i].isTrigger = false;

        // Rigidbody는 프리팹에 에디터에서 붙이고, 여기서는 힘만 가함
        Rigidbody rigidGrenade = instantGrenade.GetComponent<Rigidbody>();
        Collider gCol = instantGrenade.GetComponent<Collider>();
        if (gCol != null && ownerColliders != null)
            for (int i = 0; i < ownerColliders.Length; i++)
                if (ownerColliders[i] != null)
                    Physics.IgnoreCollision(gCol, ownerColliders[i], true);

        if (rigidGrenade != null)
        {
            rigidGrenade.AddForce(throwDir * force, ForceMode.Impulse);
            rigidGrenade.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
        }
        return true;
    }

    // ── 색상 ─────────────────────────────────────────────────

    /// <summary>
    /// 현재 캐릭터 기본 색 반환.
    /// 고유색 모드면 uniqueColors[uniqueColorIndex], 아니면 흑/백.
    /// </summary>
    public Color GetCurrentBaseColor()
    {
        if (isUniqueColor) return uniqueColor;
        return isBlack ? Color.black : Color.white;
    }

    // ── 데미지 ───────────────────────────────────────────────

    /// <summary> 외부(적 근거리/원거리 등)에서 호출하는 데미지 처리 </summary>
    public void TakeDamage(int amount, bool knockback = false)
    {
        if (IsDead) return;
        if (Time.time < dodgeInvincibleUntil) return;
        if (isDamage) return;

        // 무적 버프: 활성 중 모든 피격 무시
        if (playerBuffSystem != null && playerBuffSystem.IsActive(PlayerBuffSystem.BuffType.Invincibility))
            return;

        // 쉴드 패시브: 보유 시 1회 자동 방어
        if (playerItemInventory != null && playerItemInventory.TryConsumeShield())
            return;

        heart -= amount;
        events?.RaiseDamaged(knockback);

        // 스텔스 여부와 관계없이 피격 시 고유색 노출 처리
        playerStealth?.RevealTemporarily();

        if (heart <= 0) { Die(); return; }
        StartCoroutine(OnDamage(knockback));
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsDead) return;

        if (other.CompareTag("Item"))
        {
            Item item = other.GetComponent<Item>();
            if (item == null) return;

            bool picked = false;
            switch (item.type)
            {
                case Item.Type.Coin:
                    coin = Mathf.Min(maxCoin, coin + item.value);
                    picked = true;
                    break;
                case Item.Type.Grenade:
                    picked = playerItemInventory.TryPickup(PlayerItemInventory.ConsumableType.Grenade);
                    break;
                case Item.Type.HealthPotion:
                    picked = playerItemInventory.TryPickup(PlayerItemInventory.ConsumableType.HealthPotion);
                    break;
                case Item.Type.Shield:
                    picked = playerItemInventory.TryPickup(PlayerItemInventory.ConsumableType.Shield);
                    break;
            }
            // 인벤토리 가득 찼으면 아이템을 파괴하지 않음
            if (picked) Destroy(other.gameObject);
        }
        else if (other.CompareTag("EnemyBullet"))
        {
            Bullet enemyBullet = other.GetComponent<Bullet>();
            if (enemyBullet != null)
                TakeDamage(enemyBullet.damage, false);

            if (other.GetComponent<Rigidbody>() != null)
                Destroy(other.gameObject);
        }
    }

    // ── 회피 ─────────────────────────────────────────────────

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
            isGrounded = true;
    }

    void Dodge()
    {
        if (!isGrounded) return;
        if (Time.time < nextActionTime) return;

        bool infiniteStam = playerBuffSystem != null
            && playerBuffSystem.IsActive(PlayerBuffSystem.BuffType.InfiniteStamina);

        if (!infiniteStam && currentStamina < dodgeStaminaCost) return;

        if (dDown)
        {
            if (!infiniteStam)
            {
                currentStamina -= dodgeStaminaCost;
                if (currentStamina < 0f) currentStamina = 0f;
                isStaminaDraining = true;
                staminaRechargeTimer = 0f;
            }

            nextActionTime = Time.time + actionCooldown;
            if (anim != null) anim.SetTrigger("doDodge");
            isDodging = true;
            dodgeInvincibleUntil = Time.time + dodgeInvincibleDuration;

            Vector3 dir = (moveVec.sqrMagnitude > 0.001f) ? moveVec : transform.forward;
            rigid.AddForce(dir * dodgeForce, ForceMode.Impulse);
            Invoke(nameof(EndDodge), 0.6f);
        }
    }

    void EndDodge() { isDodging = false; }

    // ── 사망 / 리스폰 ─────────────────────────────────────────

    IEnumerator OnDamage(bool isBossAtk)
    {
        isDamage = true;

        if (isBossAtk)
        {
            isKnockback = true;
            rigid.AddForce(transform.forward * -25, ForceMode.Impulse);
        }

        yield return new WaitForSeconds(0.5f);
        isDamage = false;

        if (isBossAtk)
        {
            rigid.linearVelocity = Vector3.zero;
            isKnockback = false;
        }

        yield return new WaitForSeconds(0.3f);
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        CancelInvoke();

        isDodging = false;
        isKnockback = false; isDamage = false;
        dodgeInvincibleUntil = 0f;
        moveSpeedMultiplier  = 1f;
        requiresInputRelease = false;

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

        if (anim != null) anim.SetTrigger("doDie");
        events?.RaiseDied();
        StartCoroutine(RespawnAfter(respawnDelay));
    }

    IEnumerator RespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Respawn();
    }

    void Respawn()
    {
        CancelInvoke();
        transform.SetPositionAndRotation(spawnPos, spawnRot);
        heart = maxHeart;
        currentStamina = maxStamina;
        isStaminaDraining = false;
        staminaRechargeTimer = 0f;

        rigid.isKinematic = false;
        rigid.linearVelocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;

        IsDead = false;
        isDamage = false; isKnockback = false; isDodging = false;
        dodgeInvincibleUntil = 0f;
        nextActionTime       = 0f;
        moveSpeedMultiplier  = 1f;
        requiresInputRelease = false;

        if (playerStealth == null)
            SetLayerRecursively(gameObject, normalLayer);

        if (cols != null)
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) cols[i].enabled = true;

        if (anim != null)
        {
            anim.ResetTrigger("doDie");
            anim.ResetTrigger("doDodge");
            anim.SetBool("isWalk", false);
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
