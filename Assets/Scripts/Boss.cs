using UnityEngine;
using UnityEngine.AI;
using System.Collections;

// Enemy를 상속 — Boss 오브젝트에는 Enemy 대신 이 컴포넌트를 붙입니다
public class Boss : Enemy
{
    enum BossAttackId { BodySlam, Missile, Rock, Taunt }

    [Header("Boss - Common")]
    public float bossAttackRange = 0;

    [Header("Boss - Body Slam")]
    public float bossChargeWindup   = 0;
    public float bossChargeSpeed    = 0;
    public float bossChargeDuration = 0;
    public float bossChargeCooldown = 0;
    public int   bossChargeDamage   = 0;
    [Tooltip("돌진 중 플레이어 감지 반경(m). Trigger 실패 시 이 거리 안이면 데미지 적용")]
    public float bossChargeHitRadius = 0;

    [Header("Boss - Homing Missile")]
    public Transform  bossMissileSpawnPoint1;
    public Transform  bossMissileSpawnPoint2;
    public GameObject bossMissilePrefab;
    public float bossMissileWindup    = 0;
    public float bossMissileSpeed     = 0;
    [Tooltip("유도 꺾임 각도 (도/초)")]
    public float bossMissileTurnSpeed = 0;
    [Tooltip("0 = 벽 충돌 전까지 유지")]
    public float bossMissileLifetime  = 0;
    public float bossMissileCooldown  = 0;
    public int   bossMissileDamage    = 0;

    [Header("Boss - Rock")]
    public GameObject bossRockPrefab;
    public float bossRockWindup       = 0;
    public float bossRockInitialSpeed = 0;
    [Tooltip("이동 거리 1m당 추가 속도")]
    public float bossRockAcceleration = 0;
    [Tooltip("회전 배율 (속도 1일 때 rad/s)")]
    public float bossRockSpinSpeed    = 0;
    [Tooltip("0 = 무제한")]
    public float bossRockLifetime     = 0;
    public float bossRockCooldown     = 0;
    public int   bossRockDamage       = 0;

    [Header("Boss - Taunt (Jump Slam)")]
    public float bossTauntWindup   = 0;
    public float bossJumpHeight    = 0;
    public float bossJumpDuration  = 0;
    public float bossSlamRadius    = 0;
    public float bossTauntCooldown = 0;
    public int   bossSlamDamage    = 0;

    [Header("Boss Attack Weights (합계 100 권장)")]
    public float bossWeightBodySlam = 0;
    public float bossWeightMissile  = 0;
    public float bossWeightRock     = 0;
    public float bossWeightTaunt    = 0;

    // ── Enemy 애니메이션 메서드 오버라이드 (Boss는 Bool 없음) ──

    // Boss Animator에 isWalk Bool 없음 → 재정의
    protected override void ChaseStart()
    {
        isChase = true;
        // walk 애니메이션 없음 — 이동만 처리
    }

    protected override void StartChase(Vector3 pos)
    {
        isChase = true;
        if (nav != null)
        {
            nav.isStopped = false;
            nav.SetDestination(pos);
        }
    }

    protected override void ClearTargetAndStop()
    {
        currentTarget = null;
        isChase = false;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttack = false;

        if (nav != null)
        {
            nav.isStopped = true;
            nav.ResetPath();
            nav.velocity = Vector3.zero;
        }
    }

    // ── Update/FixedUpdate 오버라이드 ───────────────────────

    protected override void Update()
    {
        if (nav == null || !nav.enabled || isDead) return;

        if (IsStealthPlayerDetected()) { ClearTargetAndStop(); return; }

        currentTarget = FindVisiblePlayer();
        if (currentTarget == null || IsPlayerDead(currentTarget)) { ClearTargetAndStop(); return; }

        // 플레이어 방향으로 회전
        Vector3 lookDir = currentTarget.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir);

        if (isAttack) return;

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist > bossAttackRange)
        {
            StartChase(currentTarget.position);
            return;
        }

        // 공격 범위 진입 → 정지 후 공격
        if (nav != null) { nav.isStopped = true; nav.ResetPath(); }
        attackRoutine = StartCoroutine(BossAttack());
    }

    protected override void FixedUpdate()
    {
        // 보스는 Enemy의 FreezeVelocity/TryAttack 불필요
    }

    // ── 공격 선택 ───────────────────────────────────────────

    BossAttackId BossSelectAttack()
    {
        float total = bossWeightBodySlam + bossWeightMissile + bossWeightRock + bossWeightTaunt;
        if (total <= 0f) return BossAttackId.BodySlam;

        float r = Random.Range(0f, total);
        if (r < bossWeightBodySlam) return BossAttackId.BodySlam;
        r -= bossWeightBodySlam;
        if (r < bossWeightMissile) return BossAttackId.Missile;
        r -= bossWeightMissile;
        if (r < bossWeightRock) return BossAttackId.Rock;
        return BossAttackId.Taunt;
    }

    IEnumerator BossAttack()
    {
        isAttack = true;
        isChase  = false;

        switch (BossSelectAttack())
        {
            case BossAttackId.BodySlam: yield return StartCoroutine(BossBodySlam());     break;
            case BossAttackId.Missile:  yield return StartCoroutine(BossFireMissiles()); break;
            case BossAttackId.Rock:     yield return StartCoroutine(BossThrowRock());    break;
            case BossAttackId.Taunt:    yield return StartCoroutine(BossTaunt());        break;
        }

        isAttack = false;
        attackRoutine = null;
    }

    // ── 개별 공격 ───────────────────────────────────────────

    // 1) 몸통 박치기
    IEnumerator BossBodySlam()
    {
        if (anim != null) anim.SetTrigger("Attack");
        yield return new WaitForSeconds(bossChargeWindup);

        if (meleeArea != null)
        {
            var hitbox = meleeArea.GetComponentInChildren<EnemyHitbox>();
            if (hitbox != null) hitbox.damage = bossChargeDamage;
            meleeArea.enabled = true;
        }

        bool chargeDamageApplied = false;
        float hitRadius = bossChargeHitRadius > 0f ? bossChargeHitRadius : 3f;

        for (float t = 0f; t < bossChargeDuration; t += Time.deltaTime)
        {
            transform.position += transform.forward * bossChargeSpeed * Time.deltaTime;

            // Trigger 실패 시 폴백: 전방 오버랩으로 1회 데미지
            if (!chargeDamageApplied && currentTarget != null)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, playerMask);
                for (int i = 0; i < hits.Length; i++)
                {
                    Player p = hits[i].GetComponent<Player>();
                    if (p != null && !p.IsDead)
                    {
                        p.TakeDamage(bossChargeDamage, true);
                        chargeDamageApplied = true;
                        break;
                    }
                }
            }

            yield return null;
        }

        if (meleeArea != null) meleeArea.enabled = false;
        yield return new WaitForSeconds(bossChargeCooldown);
    }

    // 2) 유도탄 — 두 발사 지점에서 동시 발사 (Homing 컴포넌트 사용)
    void FireMissileFrom(Transform spawnPoint)
    {
        if (spawnPoint == null || bossMissilePrefab == null || currentTarget == null) return;

        GameObject go = Instantiate(bossMissilePrefab, spawnPoint.position, spawnPoint.rotation);
        go.tag = "EnemyBullet";

        Homing hm = go.GetComponentInChildren<Homing>();
        if (hm != null)
        {
            hm.target    = currentTarget;
            hm.speed     = bossMissileSpeed;
            hm.turnSpeed = bossMissileTurnSpeed;
            hm.damage    = bossMissileDamage;
            hm.lifetime  = bossMissileLifetime;
        }
    }

    IEnumerator BossFireMissiles()
    {
        if (anim != null) anim.SetTrigger("Shot");
        yield return new WaitForSeconds(bossMissileWindup);
        FireMissileFrom(bossMissileSpawnPoint1);
        FireMissileFrom(bossMissileSpawnPoint2);
        yield return new WaitForSeconds(bossMissileCooldown);
    }

    // 3) 돌 던지기 — 멀어질수록 가속 (SpinRoller 컴포넌트 사용)
    IEnumerator BossThrowRock()
    {
        if (currentTarget == null) yield break;
        if (anim != null) anim.SetTrigger("BigShot");
        yield return new WaitForSeconds(bossRockWindup);

        if (bossRockPrefab != null)
        {
            Vector3 throwDir = currentTarget.position - transform.position;
            throwDir.y = 0f;
            if (throwDir.sqrMagnitude > 0.001f) throwDir.Normalize();
            else throwDir = transform.forward;

            GameObject rock = Instantiate(bossRockPrefab, transform.position, Quaternion.LookRotation(throwDir));
            rock.tag = "EnemyBullet";

            SpinRoller sr = rock.GetComponentInChildren<SpinRoller>();
            if (sr != null)
            {
                sr.moveDir      = throwDir;
                sr.initialSpeed = bossRockInitialSpeed;
                sr.acceleration = bossRockAcceleration;
                sr.spinSpeed    = bossRockSpinSpeed;
                sr.damage       = bossRockDamage;
                sr.lifetime     = bossRockLifetime;
            }
        }

        yield return new WaitForSeconds(bossRockCooldown);
    }

    // 4) Taunt — 플레이어 위치로 점프 후 내려찍기
    IEnumerator BossTaunt()
    {
        if (currentTarget == null) yield break;

        if (anim != null) anim.SetTrigger("Taunt");

        Vector3 slamTarget = currentTarget.position;
        if (nav != null) nav.enabled = false;

        yield return new WaitForSeconds(bossTauntWindup);

        Vector3 startPos = transform.position;
        Vector3 peak     = Vector3.Lerp(startPos, slamTarget, 0.5f) + Vector3.up * bossJumpHeight;
        float   half     = bossJumpDuration * 0.5f;

        // 상승
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(startPos, peak, t / half);
            yield return null;
        }
        // 하강
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(peak, slamTarget, t / half);
            yield return null;
        }
        transform.position = slamTarget;

        // 착지 충격 — Player 레이어 범위 피해
        Collider[] hits = Physics.OverlapSphere(transform.position, bossSlamRadius, playerMask);
        foreach (var hit in hits)
        {
            Player p = hit.GetComponent<Player>();
            if (p != null) p.TakeDamage(bossSlamDamage, true);
        }

        if (nav != null)
        {
            nav.enabled = true;
            nav.Warp(transform.position);
        }

        yield return new WaitForSeconds(bossTauntCooldown);
    }
}
