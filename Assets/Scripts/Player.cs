using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Move")]
    public float speed = 0f;
    public float runMultiplier = 0f;

    [Header("Combat")]
    public GameObject[] weapons;
    public bool[] hasWeapons;

    public GameObject[] grenades;
    public int hasGrenades;
    public GameObject grenadeObj;

    public Camera followCamera;
    public LayerMask aimMask;

    [Header("Stat")]
    public int ammo;
    public int coin;
    public int heart;

    public int maxAmmo = 0;
    public int maxCoin = 0;
    public int maxHeart = 0;
    public int maxHasGrenades = 0;

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

    public bool IsDead { get; private set; }

    public Vector2 moveInput;
    Vector3 moveVec;

    bool isGrounded;
    bool isJumping;
    bool isDodging;
    bool isSwap;
    bool isReload;
    bool isDamage;
    bool isKnockback;

    bool sDown1, sDown2, sDown3;
    bool fDown, rDown, gDown, bwDown, dDown, eDown;

    bool isFireReady = true;
    float fireDelay;
    float nextActionTime = 0f;
    float nextBWTime = 0f;

    Rigidbody rigid;
    Animator anim;

    GameObject nearObject;
    Weapon equipWeapon;

    [Header("Grenade Throw")]
    public float grenadeForce = 0f;
    public float grenadeUpForce = 0f;
    public Vector3 grenadeSpawnOffset = Vector3.zero;
    public Collider[] ownerColliders;

    Vector3 spawnPos;
    Quaternion spawnRot;

    int normalLayer;
    int deadLayer;
    Collider[] cols;
    float fixedY;

    PlayerEvents events;
    PlayerStealth playerStealth;


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

        events.RaiseBlackWhiteChanged(isBlack);

        if (weapons != null)
        {
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] != null) weapons[i].SetActive(false);
                if (hasWeapons != null && i < hasWeapons.Length) hasWeapons[i] = false;
            }
        }
        equipWeapon = null;
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
        Dodge();
        Interation();
        Swap();
        Attack();
        Reload();
        Grenade();

        if (bwDown && Time.time >= nextBWTime)
        {
            nextBWTime = Time.time + bwCooldown;
            isBlack = !isBlack;
            events?.RaiseBlackWhiteChanged(isBlack);
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

        dDown  = Keyboard.current.spaceKey.wasPressedThisFrame;
        rDown  = Keyboard.current.rKey.wasPressedThisFrame;
        gDown  = Mouse.current.rightButton.wasPressedThisFrame;
        eDown  = Keyboard.current.eKey.wasPressedThisFrame;
        bwDown = Keyboard.current.leftCtrlKey.wasPressedThisFrame;

        if (equipWeapon != null && equipWeapon.type == Weapon.Type.Range)
        {
            bool auto = equipWeapon.isAuto;
            fDown = auto ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;
        }
        else
        {
            fDown = Mouse.current.leftButton.wasPressedThisFrame;
        }
    }

    void Move()
    {
        if (isKnockback) return;

        // 마우스 방향 = 캐릭터 정면 → W = 정면, S = 뒤, A = 왼쪽, D = 오른쪽 (맵 기준 아님)
        if (followCamera != null)
            moveVec = (transform.forward * moveInput.y + transform.right * moveInput.x).normalized;
        else
            moveVec = new Vector3(moveInput.x, 0, moveInput.y).normalized;

        bool hasMove = moveVec.sqrMagnitude > 0.0001f;
        bool walkKey = Keyboard.current.leftShiftKey.isPressed;

        float finalSpeed = speed * (walkKey ? 1f : runMultiplier);

        Vector3 v = rigid.linearVelocity;
        v.x = moveVec.x * finalSpeed;
        v.z = moveVec.z * finalSpeed;
        rigid.linearVelocity = v;

        if (anim != null)
        {
            anim.SetBool("isWalk", hasMove && walkKey);
            anim.SetBool("isRun",  hasMove && !walkKey);
        }
    }

    void Turn()
    {
        if (followCamera == null) return;

        // 마우스 방향 = 캐릭터 정면 (레이로 지면 찍어서 그 방향으로 회전)
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

    void Attack()
    {
        if (equipWeapon == null) return;

        fireDelay += Time.deltaTime;
        isFireReady = fireDelay >= equipWeapon.rate;

        if (fDown && isFireReady && !isDodging && !isSwap && !isJumping)
        {
            equipWeapon.Use();
            if (anim != null)
                anim.SetTrigger(equipWeapon.type == Weapon.Type.Melee ? "doSwing" : "doShot");
            fireDelay = 0f;
        }
    }

    void Reload()
    {
        if (equipWeapon == null) return;
        if (equipWeapon.type == Weapon.Type.Melee) return;
        if (ammo == 0) return;

        if (rDown && !isDodging && !isSwap && !isJumping && isFireReady)
        {
            isReload = true;
            if (anim != null) anim.SetTrigger("doReload");
            Invoke(nameof(ReloadOut), 3f);
        }
    }

    void ReloadOut()
    {
        if (equipWeapon == null) { isReload = false; return; }

        int reAmmo = ammo < equipWeapon.maxAmmo ? ammo : equipWeapon.maxAmmo;
        equipWeapon.curAmmo = reAmmo;
        ammo -= reAmmo;
        isReload = false;
    }

    void Grenade()
    {
        if (hasGrenades <= 0) return;
        if (!gDown || isReload || isSwap) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = followCamera.ScreenPointToRay(mousePos);

        if (!Physics.Raycast(ray, out RaycastHit hit, 200f, aimMask)) return;

        Vector3 spawnPosG = transform.position + transform.TransformDirection(grenadeSpawnOffset);
        Vector3 flat = hit.point - spawnPosG;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) flat = transform.forward;

        Vector3 throwDir = flat.normalized + Vector3.up * grenadeUpForce;

        GameObject instantGrenade = Instantiate(grenadeObj, spawnPosG, Quaternion.identity);
        Rigidbody rigidGrenade = instantGrenade.GetComponent<Rigidbody>();
        rigidGrenade.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        Collider gCol = instantGrenade.GetComponent<Collider>();
        if (gCol != null && ownerColliders != null)
            for (int i = 0; i < ownerColliders.Length; i++)
                if (ownerColliders[i] != null)
                    Physics.IgnoreCollision(gCol, ownerColliders[i], true);

        rigidGrenade.AddForce(throwDir * grenadeForce, ForceMode.Impulse);
        rigidGrenade.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);

        hasGrenades--;
        if (grenades != null && hasGrenades >= 0 && hasGrenades < grenades.Length)
            grenades[hasGrenades].SetActive(false);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Floor"))
            isGrounded = true;
    }

    void Dodge()
    {
        if (!isGrounded) return;
        if (Time.time < nextActionTime) return;

        if (dDown && !isSwap)
        {
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

    void Swap()
    {
        int weaponIndex = -1;
        if (sDown1) weaponIndex = 0;
        else if (sDown2) weaponIndex = 1;
        else if (sDown3) weaponIndex = 2;

        if (weaponIndex == -1) return;
        if (hasWeapons == null || weaponIndex >= hasWeapons.Length || !hasWeapons[weaponIndex]) return;
        if (isJumping || isDodging) return;

        Weapon next = weapons[weaponIndex]?.GetComponent<Weapon>();
        if (next == null) return;
        if (equipWeapon == next) return;

        if (equipWeapon != null) equipWeapon.gameObject.SetActive(false);

        equipWeapon = next;
        equipWeapon.gameObject.SetActive(true);

        if (anim != null) anim.SetTrigger("doSwap");

        isSwap = true;
        Invoke(nameof(SwapOut), 0.4f);
    }

    void SwapOut() { isSwap = false; }

    void Interation()
    {
        if (!eDown) return;
        if (nearObject == null) return;
        if (isJumping || isDodging) return;

        if (nearObject.CompareTag("Weapon"))
        {
            Item item = nearObject.GetComponent<Item>();
            if (item == null) return;

            int weaponIndex = item.value;
            if (hasWeapons == null || weaponIndex < 0 || weaponIndex >= hasWeapons.Length) return;

            hasWeapons[weaponIndex] = true;
            nearObject = null; // Destroy 전에 참조 해제
            Destroy(nearObject == null ? null : nearObject); // 아래에서 재처리
        }
    }

    // Interation 버그 수정: nearObject 참조 꼬임 방지를 위해 분리
    void Interation_Fixed()
    {
        if (!eDown) return;
        if (nearObject == null) return;
        if (isJumping || isDodging) return;

        if (!nearObject.CompareTag("Weapon")) return;

        Item item = nearObject.GetComponent<Item>();
        if (item == null) return;

        int weaponIndex = item.value;
        if (hasWeapons == null || weaponIndex < 0 || weaponIndex >= hasWeapons.Length) return;

        hasWeapons[weaponIndex] = true;

        GameObject toDestroy = nearObject;
        nearObject = null; // 먼저 참조 해제

        Equip(weaponIndex);   // 장착
        Destroy(toDestroy);   // 그 다음 삭제
    }

    void Equip(int weaponIndex)
    {
        if (weapons == null || weaponIndex < 0 || weaponIndex >= weapons.Length) return;

        if (equipWeapon != null)
        {
            equipWeapon.gameObject.SetActive(false);
        }

        equipWeapon = weapons[weaponIndex].GetComponent<Weapon>();
        if (equipWeapon == null) return;

        equipWeapon.gameObject.SetActive(true);
    }

    /// <summary> 외부(적 근거리/원거리 등)에서 호출하는 데미지 처리. 근거리·원거리 공격 모두 이 경로로 적용. </summary>
    public void TakeDamage(int amount, bool knockback = false)
    {
        if (IsDead) return;
        if (Time.time < dodgeInvincibleUntil) return;
        if (isDamage) return;

        heart -= amount;
        events?.RaiseDamaged(knockback);

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

            switch (item.type)
            {
                case Item.Type.Ammo:
                    ammo = Mathf.Min(maxAmmo, ammo + item.value);
                    break;
                case Item.Type.Coin:
                    coin = Mathf.Min(maxCoin, coin + item.value);
                    break;
                case Item.Type.Heart:
                    heart = Mathf.Min(maxHeart, heart + item.value);
                    break;
                case Item.Type.Grenade:
                    if (grenades != null && hasGrenades >= 0 && hasGrenades < grenades.Length)
                        grenades[hasGrenades].SetActive(true);
                    hasGrenades = Mathf.Min(maxHasGrenades, hasGrenades + item.value);
                    break;
            }
            Destroy(other.gameObject);
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

    IEnumerator OnDamage(bool isBossAtk)
    {
        isDamage = true;
        // 색상 플래시는 PlayerVisualController가 events.OnDamaged 통해 MPB로 처리

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

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Weapon")) return;
        if (nearObject != other.gameObject) nearObject = other.gameObject;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Weapon")) nearObject = null;
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        CancelInvoke();

        isDodging = false; isSwap = false; isReload = false;
        isKnockback = false; isDamage = false;
        dodgeInvincibleUntil = 0f;

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

        rigid.isKinematic = false;
        rigid.linearVelocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;

        IsDead = false;
        isDamage = false; isKnockback = false; isDodging = false;
        isSwap = false; isReload = false;
        dodgeInvincibleUntil = 0f;
        nextActionTime = 0f;
        fireDelay = 0f;

        if (playerStealth == null)
            SetLayerRecursively(gameObject, normalLayer);

        if (cols != null)
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) cols[i].enabled = true;

        if (anim != null)
        {
            // Animator 파라미터 리셋 (doDie 트리거 등이 남아있을 수 있음)
            anim.ResetTrigger("doDie");
            anim.ResetTrigger("doDodge");
            anim.ResetTrigger("doReload");
            anim.ResetTrigger("doSwap");
            anim.ResetTrigger("doSwing");
            anim.ResetTrigger("doShot");
            
            // Bool 파라미터 초기화
            anim.SetBool("isWalk", false);
            anim.SetBool("isRun", false);
            
            // Animator를 Idle 상태로 강제 전환
            anim.Play("Idle", 0, 0f);
            anim.Update(0f);
        }

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