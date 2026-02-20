---
name: unity-zero-defaults
description: Unity 프로젝트에서 수치화 가능한 항목들을 기본값 0으로 설정하고 Inspector에서 조정 가능하도록 합니다. 속도, 데미지, 거리, 쿨타임 등의 수치를 하드코딩하지 않고 public 또는 [SerializeField]로 노출합니다. Unity 스크립트를 작성하거나 수정할 때 사용합니다.
---

# Unity 수치 기본값 규칙

Unity 프로젝트에서 수치화 가능한 항목들은 기본값을 0으로 설정하고, 모든 수치를 Inspector에서 조정 가능하도록 노출합니다.

## 핵심 원칙

### 1. 기본값은 0으로 설정
- 모든 수치화 가능한 항목의 기본값은 `0`으로 설정
- Inspector에서 실제 값을 설정하도록 유도
- 코드 내부에서 하드코딩된 수치값 사용 금지

### 2. Inspector 노출 필수
- 모든 수치는 `public` 또는 `[SerializeField] private`로 선언
- 코드 내부에서 직접 값을 할당하지 않음
- Inspector에서 조정 가능하도록 필드 노출

### 3. 하드코딩 금지
- 속도, 데미지, 거리, 쿨타임 등 모든 수치값은 하드코딩하지 않음
- 매직 넘버 사용 금지
- 상수값도 변수로 추출하여 Inspector에서 조정 가능하게 함

## 적용 대상 수치

다음과 같은 수치화 가능한 항목들에 적용:

- **이동 관련**: 속도(speed), 가속도(acceleration), 점프력(jumpForce), 최대 속도(maxSpeed)
- **전투 관련**: 데미지(damage), 공격 범위(attackRange), 공격 쿨타임(attackCooldown), 크리티컬 확률(criticalChance)
- **거리 관련**: 감지 거리(detectionRange), 사거리(range), 상호작용 거리(interactionRange)
- **시간 관련**: 쿨타임(cooldown), 지속 시간(duration), 딜레이(delay), 애니메이션 시간(animationTime)
- **체력/스탯**: 최대 체력(maxHealth), 체력 회복량(healAmount), 방어력(defense)
- **기타**: 회전 속도(rotationSpeed), 크기(scale), 강도(force), 중력(gravity)

## 코드 작성 가이드라인

### 올바른 예시

```csharp
// ✅ 좋은 예: 기본값 0, Inspector 노출
public class Player : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 0f;
    [SerializeField] private float jumpForce = 0f;
    [SerializeField] private float attackDamage = 0f;
    [SerializeField] private float attackCooldown = 0f;
    [SerializeField] private float attackRange = 0f;
}

// ✅ 좋은 예: public 필드 사용
public class Weapon : MonoBehaviour
{
    public float damage = 0f;
    public float range = 0f;
    public float cooldown = 0f;
    public float projectileSpeed = 0f;
}
```

### 잘못된 예시

```csharp
// ❌ 나쁜 예: 하드코딩된 값
public class Player : MonoBehaviour
{
    void Update()
    {
        transform.Translate(Vector3.forward * 5f * Time.deltaTime); // 하드코딩
    }
}

// ❌ 나쁜 예: 기본값이 0이 아님
public class Enemy : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3.5f; // 기본값이 0이 아님
    [SerializeField] private float attackDamage = 10f; // 하드코딩
}

// ❌ 나쁜 예: private 필드로 Inspector 노출 안 됨
public class Weapon : MonoBehaviour
{
    private float damage = 0f; // Inspector에서 조정 불가
    private float range = 0f;
}
```

## 체크리스트

코드 작성 시 다음을 확인:

- [ ] 모든 수치화 가능한 항목의 기본값이 `0`으로 설정되었는가?
- [ ] 모든 수치가 `public` 또는 `[SerializeField] private`로 선언되었는가?
- [ ] 코드 내부에 하드코딩된 수치값이 없는가? (예: `* 5f`, `* 10f`, `Time.deltaTime * 3.5f` 등)
- [ ] Inspector에서 조정 가능한 필드로 노출되었는가?
- [ ] 매직 넘버가 없는가?

## 특수 상황

### 배열/리스트의 경우
배열이나 리스트의 요소들도 기본값 0을 유지:

```csharp
// ✅ 좋은 예
[SerializeField] private float[] damageMultipliers = new float[3]; // 모든 요소가 0

// ❌ 나쁜 예
[SerializeField] private float[] damageMultipliers = { 1.0f, 1.5f, 2.0f }; // 하드코딩
```

### ScriptableObject 사용
여러 오브젝트에서 공유하는 수치값은 ScriptableObject로 분리하되, 기본값은 0:

```csharp
[CreateAssetMenu]
public class WeaponData : ScriptableObject
{
    public float damage = 0f;
    public float range = 0f;
    public float cooldown = 0f;
}
```

## 코드 수정 워크플로우

1. **수치값 식별**: 코드에서 하드코딩된 수치값 찾기
2. **필드 추출**: 해당 수치를 필드로 추출
3. **기본값 설정**: 기본값을 `0`으로 설정
4. **Inspector 노출**: `public` 또는 `[SerializeField] private`로 선언
5. **하드코딩 제거**: 코드 내부의 하드코딩된 값 제거
6. **검증**: 체크리스트로 재확인

## 주의사항

- **Unity API 상수**: Unity API의 필수 상수값(예: `Time.deltaTime`, `Vector3.zero`)은 예외
- **계산식**: 수치값 자체만 규칙 적용, 계산식 자체는 문제없음
- **Inspector 설정 안내**: 코드 수정 후 Inspector에서 값을 설정해야 함을 사용자에게 안내
