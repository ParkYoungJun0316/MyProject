using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class Enemy : MonoBehaviour
{
    public enum Type { A, B, C, D };
    public Type enemyType;

    [Header("Stat")]
    public int maxHealth;
    public int curHealth;

    [Header("Attack")]
    public BoxCollider meleeArea;
    public GameObject bullet;

    [Header("Missile")]
    [Tooltip("비어 있으면 transform.position에서 발사")]
    public Transform missileSpawnPoint;


    [Header("Attack Damage (Inspector에서 설정)")]
    public int meleeDamage = 0;
    public int rangedDamage = 0;

    [Header("State")]
    public bool isChase;
    public bool isAttack;
    public bool isDead;

    [Header("Refs")]
    public Rigidbody rigid;
    public BoxCollider boxCollider;
    public MeshRenderer[] meshs;
    public NavMeshAgent nav;
    public Animator anim;

    public float bulletspeed;

    [Header("Detect")]
    public float detectRange = 50f;     // 맵 전체 감지라면 크게(예: 999)
    public float stopDistance = 0.3f;   // 추격 멈춤 임계값

    // 레이어 마스크 — Boss에서도 접근
    protected int playerMask;
    protected int playerStealthMask;

    // 상태 — Boss에서도 접근
    protected Transform currentTarget;
    protected Coroutine attackRoutine;

    [Header("Patrol")]
    public bool usePatrol = true;
    public float patrolRadius = 12f;
    public float patrolInterval = 5f;

    bool isPatrolling;
    Coroutine patrolRoutine;
    Vector3 spawnPos;

    protected virtual void Awake()
    {
        rigid = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        meshs = GetComponentsInChildren<MeshRenderer>();
        nav = GetComponent<NavMeshAgent>();
        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        // 프리팹에 Is Attack/Is Chase 체크돼 있어도 런타임에 초기화 (Enemy C 멈춤 방지)
        isAttack = false;
        isChase = false;


        if (rigid != null)
            rigid.isKinematic = true;

        if (nav != null)
            nav.enabled = true;

        playerMask = LayerMask.GetMask("Player");
        playerStealthMask = LayerMask.GetMask("PlayerStealth");

        if (meleeArea != null)
        {
            var hitbox = meleeArea.GetComponent<EnemyHitbox>();
            if (hitbox != null)
                hitbox.damage = meleeDamage;
        }

        Invoke(nameof(ChaseStart), 0.2f);

        spawnPos = transform.position;
    }

    protected virtual void ChaseStart()
    {
        isChase = true;
        if (anim != null)
            anim.SetBool("isWalk", true);
    }

    protected virtual void Update()
    {
        if (nav == null || !nav.enabled || isDead) return;

        // 1) 은신 중이면 패트롤
        if (IsStealthPlayerDetected())
        {
            StartPatrol();
            UpdateAnimation(); // 애니메이션 상태 업데이트 추가
            return;
        }

        currentTarget = FindVisiblePlayer();

        // 2) 플레이어 사망 체크
        if (currentTarget != null && IsPlayerDead(currentTarget))
        {
            ClearTargetAndStop();
            StartPatrol();
            return;
        }

        // 3) 타겟이 없으면 패트롤
        if (currentTarget == null)
        {
            StartPatrol();
            UpdateAnimation(); // 애니메이션 상태 업데이트 추가
            return;
        }

        // 4) 추격 중일 때
        if (!isAttack)
        {
            StartChase(currentTarget.position);
        }

        if (currentTarget != null)
        {
            StopPatrol();
        }

        UpdateAnimation(); // 매 프레임 속도에 따라 애니메이션 자동 조절
    }

    // ✅ 애니메이션을 실제 속도에 맞춰 조절하는 함수 추가
    void UpdateAnimation()
    {
        if (anim == null || nav == null) return;
        bool walking = nav.enabled && !nav.isStopped && nav.remainingDistance > nav.stoppingDistance;
        anim.SetBool("isWalk", walking);
    }

    void StartPatrol()
    {
        if (!usePatrol) { ClearTargetAndStop(); return; }
        if (isAttack) return;
        if (isPatrolling) return;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttack = false;
        if (anim != null)
            anim.SetBool("isAttack", false);

        isChase = false;
        if (anim != null)
            anim.SetBool("isWalk", true);

        isPatrolling = true;
        patrolRoutine = StartCoroutine(PatrolRoutine());
    }

    void StopPatrol()
    {
        if (!isPatrolling) return;

        if (patrolRoutine != null)
        {
            StopCoroutine(patrolRoutine);
            patrolRoutine = null;
        }
        isPatrolling = false;
    }

    protected bool IsPlayerDead(Transform t)
    {
        if (t == null) return true;

        Player p = t.GetComponent<Player>();
        if (p == null) return true;

        return p.IsDead;
    }

    IEnumerator PatrolRoutine()
    {
    
        while (isPatrolling && !isDead && nav.enabled)
        {
            // ✅ 은신이 풀렸거나 플레이어가 감지되면 즉시 패트롤 종료(추격은 Update가 처리)
            if (!IsStealthPlayerDetected())
            {
                // 다음 Update에서 FindVisiblePlayer로 잡을 수 있게 종료만
                StopPatrol();
                yield break;
            }

            Vector3 nextPos;
            if (TryGetRandomNavPoint(out nextPos))
            {
                nav.isStopped = false;
                nav.SetDestination(nextPos);
            }
            else
            {
                nav.isStopped = true;
                nav.ResetPath();
            }

            yield return new WaitForSeconds(patrolInterval);
        }
    }

    bool TryGetRandomNavPoint(out Vector3 result)
    {
        // 스폰 지점을 중심으로 배회(원하면 transform.position 기준으로 바꿔도 됨)
        Vector3 rand = spawnPos + Random.insideUnitSphere * patrolRadius;
        rand.y = spawnPos.y;

        if (NavMesh.SamplePosition(rand, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            result = hit.position;
            return true;
        }

        result = transform.position;
        return false;
    }

    // ✅ 은신 레이어 감지 여부 (한 프레임이라도 걸리면 즉시 끊기)
    protected bool IsStealthPlayerDetected()
    {
        // Physics 설정에서 Enemy vs PlayerStealth 충돌 OFF 했어도
        // OverlapSphere는 "충돌 매트릭스"와 별개로 작동할 수 있어서
        // 레이어 마스크로 확실히 걸러주는 게 안전함.
        Collider[] stealthHits = Physics.OverlapSphere(transform.position, detectRange, playerStealthMask);
        return stealthHits != null && stealthHits.Length > 0;
    }

    // Player 레이어만 감지
    protected Transform FindVisiblePlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectRange, playerMask);
        if (hits == null || hits.Length == 0) return null;

        // 가장 가까운 플레이어(나중에 pvp면 다수 중 선택)
        float best = float.MaxValue;
        Transform bestT = null;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform t = hits[i].transform;
            float d = (t.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestT = t;
            }
        }
        return bestT;
    }

    protected virtual void StartChase(Vector3 pos)
    {
        isChase = true;
        if (nav != null)
        {
            nav.isStopped = false;
            nav.SetDestination(pos);
        }
        if (anim != null)
            anim.SetBool("isWalk", true);
    }

    protected virtual void ClearTargetAndStop()
    {
        currentTarget = null;
        isChase = false;
        if (anim != null)
            anim.SetBool("isWalk", false);

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttack = false;
        if (anim != null)
            anim.SetBool("isAttack", false);

        if (nav != null)
        {
            nav.isStopped = true;
            nav.ResetPath();
            nav.velocity = Vector3.zero;
        }
    }

    protected virtual void FixedUpdate()
    {
        if (nav == null || !nav.enabled || isDead) return;

        FreezeVelocity();
        TryAttack();
    }

    void FreezeVelocity()
    {
        if (!isChase || rigid == null) return;
        if (rigid.isKinematic) return;

        rigid.linearVelocity = Vector3.zero;
        rigid.angularVelocity = Vector3.zero;
    }

    void TryAttack()
    {
        if (currentTarget != null && IsPlayerDead(currentTarget))
        {
            ClearTargetAndStop();
            StartPatrol();
            return;
        }

        // ✅ 은신이면 공격 시도 자체를 막음
        if (IsStealthPlayerDetected())
            return;

        if (currentTarget == null) return;
        if (isAttack) return;

        float targetRange = 0f;

        switch (enemyType)
        {
            case Type.A: targetRange = 10f; break;
            case Type.B: targetRange = 12f; break;
            case Type.C: targetRange = 25f; break;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist > targetRange) return;

        // 공격 전에 바라보기
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);

        attackRoutine = StartCoroutine(Attack());
    }

    IEnumerator Attack()
    {
        isChase = false;
        isAttack = true;
        if (anim != null)
            anim.SetBool("isAttack", true);

        if (IsStealthPlayerDetected())
        {
            ClearTargetAndStop();
            yield break;
        }

        switch (enemyType)
        {
            case Type.A:
                yield return new WaitForSeconds(0.2f);
                if (IsStealthPlayerDetected()) { ClearTargetAndStop(); yield break; }

                meleeArea.enabled = true;
                yield return new WaitForSeconds(0.3f);
                meleeArea.enabled = false;

                yield return new WaitForSeconds(1f);
                break;

            case Type.B:
                yield return new WaitForSeconds(0.1f);
                if (IsStealthPlayerDetected()) { ClearTargetAndStop(); yield break; }

                if (rigid != null && !rigid.isKinematic)
                    rigid.AddForce(transform.forward * 20, ForceMode.Impulse);
                else
                {
                    float chargeDuration = 0.5f;
                    float chargeSpeed = 8f;
                    for (float t = 0f; t < chargeDuration; t += Time.deltaTime)
                    {
                        transform.position += transform.forward * (chargeSpeed * Time.deltaTime);
                        yield return null;
                    }
                }
                meleeArea.enabled = true;

                yield return new WaitForSeconds(0.5f);
                if (rigid != null && !rigid.isKinematic)
                    rigid.linearVelocity = Vector3.zero;
                meleeArea.enabled = false;

                yield return new WaitForSeconds(2f);
                break;

            case Type.C:
                yield return new WaitForSeconds(0.5f);
                if (IsStealthPlayerDetected()) { ClearTargetAndStop(); yield break; }
                if (bullet == null) { isAttack = false; if (anim != null) anim.SetBool("isAttack", false); yield break; }

                Transform spawn = missileSpawnPoint != null ? missileSpawnPoint : transform;
                Vector3 spawnPosC = spawn.position;
                Quaternion spawnRot = spawn.rotation;

                GameObject instantBullet = Instantiate(bullet, spawnPosC, spawnRot);
                instantBullet.tag = "EnemyBullet";
                Bullet bulletComp = instantBullet.GetComponent<Bullet>();
                if (bulletComp != null)
                    bulletComp.damage = rangedDamage;

                Rigidbody rigidbullet = instantBullet.GetComponent<Rigidbody>();
                if (rigidbullet != null)
                    rigidbullet.linearVelocity = spawn.forward * bulletspeed;

                yield return new WaitForSeconds(1f);
                break;
        }

        // ✅ 공격 끝나고도 은신이면 추격 재개 금지
        if (IsStealthPlayerDetected())
        {
            ClearTargetAndStop();
            yield break;
        }

        isAttack = false;
        if (anim != null)
            anim.SetBool("isAttack", false);

        attackRoutine = null;
    }

    // --- 피격 처리 ---
    void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Melee")
        {
            Weapon weapon = other.GetComponent<Weapon>();
            if (weapon == null) return;

            curHealth -= weapon.damage;
            Vector3 reactVec = transform.position - other.transform.position;
            StartCoroutine(OnDamage(reactVec, false));
            Debug.Log("Melee : " + curHealth);
        }
        else if (other.tag == "Bullet")
        {
            Bullet bullet = other.GetComponent<Bullet>();
            curHealth -= bullet.damage;
            Vector3 reactVec = transform.position - other.transform.position;

            Destroy(other.gameObject);

            StartCoroutine(OnDamage(reactVec, false));
            Debug.Log("Range : " + curHealth);
        }
    }

    public void HitByGrenade(Vector3 explosionPos)
    {
        curHealth -= 100;
        Vector3 reactVec = transform.position - explosionPos;
        StartCoroutine(OnDamage(reactVec, true));
    }

    IEnumerator OnDamage(Vector3 reactVec, bool isGrenade)
    {
        foreach (MeshRenderer mesh in meshs) mesh.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);

        if (curHealth > 0)
        {
            foreach (MeshRenderer mesh in meshs) mesh.material.color = Color.white;
        }
        else
        {
            foreach (MeshRenderer mesh in meshs) mesh.material.color = Color.gray;

            gameObject.layer = LayerMask.GetMask("Player"); ;
            isDead = true;

            ClearTargetAndStop(); // ✅ 죽으면 추격/공격 싹 정리

            nav.enabled = false;
            if (anim != null)
                anim.SetTrigger("doDie");

            if (rigid != null)
            {
                rigid.isKinematic = false;

                if (isGrenade)
                {
                    reactVec = reactVec.normalized;
                    reactVec += Vector3.up * 2f;
                    rigid.freezeRotation = false;
                    rigid.AddForce(reactVec * 5, ForceMode.Impulse);
                    rigid.AddTorque(reactVec * 15f, ForceMode.Impulse);
                }
                else
                {
                    reactVec = reactVec.normalized;
                    reactVec += Vector3.up;
                    rigid.AddForce(reactVec * 5, ForceMode.Impulse);
                }
            }

            if (enemyType != Type.D)
                Destroy(gameObject, 4);
        }
    }
}
