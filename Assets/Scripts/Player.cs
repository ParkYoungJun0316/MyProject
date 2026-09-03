using System.Collections;
using Unity.Netcode;
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

    [Header("침 미끄럼 (얼음)")]
    [Tooltip("침 위에서 정지 → 풀속도까지 걸리는 시간(초). 클수록 출발이 느리고 발이 밀림.")]
    [SerializeField] [Min(0.05f)] float salivaAccelTime = 1.2f;
    [Tooltip("침 위에서 풀속도 → 정지까지 걸리는 시간(초). 클수록 손 떼도 길게 미끄러짐. Accel보다 크게.")]
    [SerializeField] [Min(0.05f)] float salivaDecelTime = 3.5f;

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

    [Header("즉사 판정")]
    [Tooltip("이 수치 이상의 데미지를 받으면 즉사(Die 애니메이션 재생). 0이면 비활성")]
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
    [Tooltip("Die 애니메이션 시작 Y 좌표. fallDeathY보다 높게 설정 (예: fallDeathY=-10 이면 -5 정도). enableFallDeath가 켜진 경우에만 적용")]
    public float fallAnimY = 0f;

    [HideInInspector] public float moveSpeedMultiplier = 1f;
    int _salivaOverlaps;

    /// <summary>
    /// 네트워크 Owner 여부. NetworkPlayerSetup이 OnNetworkSpawn에서 설정.
    /// true면 이 머신이 물리 이동의 권한자(Owner Authority) — 입력·FixedUpdate 이동·카메라 타겟 전부 이 캐릭터 주도.
    /// 비오너는 ClientNetworkTransform(Owner 권한)으로 위치만 수신.
    /// 오프라인(NGO 미사용) 시 기본값 true → 입력·물리 그대로 동작.
    /// </summary>
    [HideInInspector] public bool isOwnerControlled = true;

    public bool IsDead   { get; private set; }
    public int PlayerId => playerId;

    /// <summary>피격 무적 시간 중 true. NetworkPlayerSetup이 서버에서 중복 피격 방지에 사용.</summary>
    public bool IsDamageInvulnerable => isDamage;

    [HideInInspector] public Vector2 moveInput;
    Vector3 moveVec;

    bool isDamage;
    bool isKnockback;

    bool bwDown, altDown;

    // 낙사 Die 애니메이션이 이미 재생됐는지 추적 (매 프레임 중복 트리거 방지)
    // Owner→Host 낙사 신고 1회 가드 (ReportFallDeathServerRpc 스팸 방지)
    bool fallDeathReported;
    bool fallAnimTriggered = false;
    // 즉사 판정 시 Die()에서 OnInstantKilled 이벤트 발생 여부 결정 (애니메이션은 일반 사망과 동일하게 doDie 통일)
    bool isInstantKill = false;

    float nextBWTime = 0f;

    Rigidbody rigid;
    Animator anim;

    int normalLayer;
    int deadLayer;
    Collider[] cols;
    float fixedY;

    PlayerEvents events;
    PlayerStealth playerStealth;
    PlayerBuffSystem playerBuffSystem;


    public void OnMove(InputValue value)
    {
        if (IsDead || !isOwnerControlled) return;
        if (fallAnimTriggered) { moveInput = Vector2.zero; return; }
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) { moveInput = Vector2.zero; return; }
        moveInput = value.Get<Vector2>();
    }

    void Awake()
    {
        rigid = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

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
        // Die 애니: Owner 로컬. 낙사 확정: Owner Y → ReportFallDeathServerRpc → Host ApplyFallDeath.
        // (Host-only Y는 Owner+CNT void에서 Client를 놓칠 수 있음 — Host Update는 Host-as-Owner 폴백)
        if (!IsDead && enableFallDeath && isOwnerControlled)
        {
            float y = transform.position.y;
            // fallAnimY 통과 시 Die 애니메이션 1회 재생 (fallDeathY보다 높은 지점에서 미리 트리거)
            if (!fallAnimTriggered && y < fallAnimY)
            {
                fallAnimTriggered = true;
                moveInput = Vector2.zero;
                anim?.SetTrigger("doDie");
                events?.RaiseFallDeath();
            }

            if (!fallDeathReported && y < fallDeathY)
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsListening)
                {
                    fallDeathReported = true;
                    GetComponent<NetworkPlayerSetup>()?.ReportFallDeathServerRpc();
                }
            }
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
        // Owner Authority: Owner가 직접 물리 이동. 오프라인도 동일.
        // 비오너(Host 복사본 포함)는 NT로 위치 수신 → Move() 불필요.
        if (!isOwnerControlled) return;

        if (IsDead)
        {
            rigid.linearVelocity  = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
            return;
        }

        Move();
        Turn();
        FreezeRotation();
    }

    void GetInput()
    {
        if (!isOwnerControlled || InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) { bwDown = altDown = false; return; }
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
        if (_salivaOverlaps <= 0)
        {
            v.x = moveVec.x * finalSpeed;
            v.z = moveVec.z * finalSpeed;
        }
        else
        {
            Vector2 horiz = new Vector2(v.x, v.z);
            float dt = Time.fixedDeltaTime;
            if (hasMove)
            {
                float accel = finalSpeed / Mathf.Max(0.05f, salivaAccelTime);
                horiz += new Vector2(moveVec.x, moveVec.z) * accel * dt;
                float max = finalSpeed;
                if (horiz.sqrMagnitude > max * max)
                    horiz = horiz.normalized * max;
            }
            else
            {
                float decel = finalSpeed / Mathf.Max(0.05f, salivaDecelTime);
                horiz = Vector2.MoveTowards(horiz, Vector2.zero, decel * dt);
            }
            v.x = horiz.x;
            v.z = horiz.y;
        }
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

    /// <summary>
    /// HP 차감 없이 피격 연출(애니·무적·넉백)만 처리.
    /// Host → 오너 클라이언트 경로(ForceKillClientRpc 이전 단계)에서 사용.
    /// </summary>
    public void TakeDamageVisualOnly()
    {
        if (IsDead) return;

        // HP UI 갱신은 무적 여부와 무관하게 항상 수행
        // (무적 중 연속 피격 시 _player.heart가 갱신됐어도 UI가 멈추는 버그 방지)
        events?.RaiseDamaged();

        if (isDamage) return; // 무적 중: 애니·효과만 스킵

        playerStealth?.RevealTemporarily();
        anim?.SetTrigger("doHit");
        StartCoroutine(OnDamage());
    }

    /// <summary>
    /// 같은 플레이어의 Punch에 맞았을 때 재생하는 피격 연출. HP·무적과 완전히 무관(PlayerPunch는 넉백만 적용).
    /// NetworkPlayerSetup.NotifyPunchHitClientRpc → 여기로 연결.
    /// </summary>
    public void PlayPunchHitReaction()
    {
        if (IsDead) return;
        anim?.SetTrigger("doPunchHit");
    }

    /// <summary>피격 무적 지속 시간. NetworkPlayerSetup에서 서버 무적 타이머 계산에 사용.</summary>
    public float InvulnerabilityDuration =>
        damageInvulnerabilityDuration > 0f ? damageInvulnerabilityDuration : 0.5f;

    /// <summary>Host가 사망을 확정한 뒤 오너 클라이언트를 통해 호출.</summary>
    public void ForceKill()
    {
        if (!IsDead) Die();
    }

    /// <summary>버프·무적 무시 즉사. Die()에서 OnInstantKilled 이벤트 연동 (애니메이션은 일반 사망과 동일한 doDie).</summary>
    public void KillInstantly()
    {
        if (IsDead) return;
        isInstantKill = true;
        Die();
    }

    /// <summary>
    /// 비오너 클라이언트(Host의 원격 플레이어 로컬 복사본 포함) 사망 플래그만 동기화.
    /// 애니메이션·콜라이더·물리 정지는 Die()를 통해 Owner 머신에서만 처리(의도된 설계).
    /// 이걸 안 하면 Host가 시뮬레이션하는 원격 플레이어의 Rigidbody/Collider가
    /// IsDead=false로 남아 트랩·피격 판정(OnTriggerEnter)에서 사망 상태를 놓칠 수 있음 —
    /// 실제로는 HP NetworkVariable 가드가 이중 방어하지만, 이 플래그도 맞춰 둔다.
    /// </summary>
    public void SyncDeadFlag() => IsDead = true;

    /// <summary>고유색 모드면 uniqueColor, 아니면 blackColor/whiteColor.</summary>
    public Color GetCurrentBaseColor()
    {
        if (isUniqueColor) return uniqueColor;
        return isBlack ? blackColor : whiteColor;
    }

    /// <summary>색 전환 쿨다운 남은 시간(초). 0이면 사용 가능.</summary>
    public float GetBWCooldownRemaining() => Mathf.Max(0f, nextBWTime - Time.time);

    /// <summary>SalivaVolume이 Cover/Hold 중 발판 위에 있을 때. 중첩 카운트.</summary>
    public void AddSalivaOverlap()
    {
        if (IsDead) return;
        _salivaOverlaps++;
    }

    public void RemoveSalivaOverlap()
    {
        if (_salivaOverlaps > 0)
            _salivaOverlaps--;
    }

    /// <summary>무적·피격 쿨 중이면 false, 실제 피격 시 true.</summary>
    public bool TryTakeDamage(int amount)
    {
        if (IsDead) return false;
        if (isDamage) return false;
        TakeDamage(amount);
        return true;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        if (isDamage) return;

        // 비오너 플레이어: 피격 판정은 Host 경로에서만. 로컬 직접 호출 무시
        if (!isOwnerControlled) return;

        // 온라인 모드: HP 변경은 반드시 NetworkDamageUtil → NetworkPlayerSetup 경로만 허용
        // (TakeDamage 직접 호출 시 heart와 _hp NetworkVariable이 어긋나는 버그 방지)
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening) return;
    }

    public void ReceiveDamage(int amount, object source)
    {
        TakeDamage(amount);
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsDead) return;

        if (other.CompareTag("EnemyBullet"))
        {
            var nm = NetworkManager.Singleton;
            // Host만 데미지 확정. Despawn은 NGO가 담당.
            if (nm != null && nm.IsServer)
            {
                Bullet enemyBullet = other.GetComponent<Bullet>();
                if (enemyBullet != null)
                    NetworkDamageUtil.ApplyDamage(this, enemyBullet.damage);
            }
        }
    }

    // ── 사망 / 리스폰 ─────────────────────────────────────────

    IEnumerator OnDamage()
    {
        isDamage = true;
        float invuln = damageInvulnerabilityDuration > 0f ? damageInvulnerabilityDuration : 0.5f;
        yield return new WaitForSeconds(invuln);
        isDamage = false;
    }

    [Header("순수 넉백 (HP 미변경)")]
    [Tooltip("Punch/Breakable/ContactKnockback/DoorController 등 순수 넉백(NetworkDamageUtil.ApplyKnockback) " +
        "적용 시 Move()가 velocity.x/z를 입력값으로 덮어써 넉백을 즉시 지우는 것을 막는 시간(초). " +
        "너무 짧으면 넉백이 거의 안 느껴지고, 너무 길면 피격 후 조작 불능 시간이 길어짐.")]
    [SerializeField] float knockbackSuppressDuration = 0.25f;

    Coroutine _knockbackSuppressRoutine;

    /// <summary>
    /// 순수 넉백 AddForce 직후 호출. Move()가 매 FixedUpdate마다 velocity.x/z를 입력값으로
    /// 덮어써 임펄스를 한 프레임 만에 지우는 문제를 막기 위해, knockbackSuppressDuration
    /// 동안만 Move()를 억제한 뒤 수평 속도를 정리하고 복귀한다.
    /// NetworkPlayerSetup.ApplyKnockbackClientRpc(Punch/Breakable/ContactKnockback/DoorController 공용)에서 호출.
    /// </summary>
    public void SuppressMoveForKnockback()
    {
        if (!isOwnerControlled || rigid.isKinematic) return;
        if (_knockbackSuppressRoutine != null) StopCoroutine(_knockbackSuppressRoutine);
        _knockbackSuppressRoutine = StartCoroutine(KnockbackSuppressRoutine());
    }

    IEnumerator KnockbackSuppressRoutine()
    {
        isKnockback = true;
        yield return new WaitForSeconds(knockbackSuppressDuration);

        Vector3 v = rigid.linearVelocity;
        v.x = 0f; v.z = 0f;
        rigid.linearVelocity = v;

        isKnockback = false;
        _knockbackSuppressRoutine = null;
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;

        CancelInvoke();

        if (_knockbackSuppressRoutine != null)
        {
            StopCoroutine(_knockbackSuppressRoutine);
            _knockbackSuppressRoutine = null;
        }
        isKnockback = false; isDamage = false;
        moveSpeedMultiplier  = 1f;
        _salivaOverlaps = 0;
        fallAnimTriggered    = false;
        fallDeathReported    = false;

        if (playerStealth != null)
            playerStealth.ForceLayer(deadLayer);
        else
            SetLayerRecursively(gameObject, deadLayer);

        if (cols != null)
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) cols[i].enabled = false;

        moveInput = Vector2.zero;
        fixedY = transform.position.y;
        if (!rigid.isKinematic)
        {
            rigid.linearVelocity  = Vector3.zero;
            rigid.angularVelocity = Vector3.zero;
        }

        if (anim != null)
        {
            anim.SetBool("isRun", false);
            anim.ResetTrigger("doDie");
            anim.SetTrigger("doDie");
        }
        if (isInstantKill) events?.RaiseInstantKilled();
        isInstantKill = false;
        events?.RaiseDied();

        // 온라인: 씬 리로드(destroyWithScene:true)가 리스폰을 담당하므로 코루틴 불필요.
        // 로컬 자동 리스폰은 사용하지 않음.
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        for (int i = 0; i < obj.transform.childCount; i++)
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
    }
}
