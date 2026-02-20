---
name: extensible-architecture
description: Unity 프로젝트에서 확장 가능한 구조를 유지하고 하드코딩을 방지합니다. enum 사용, 변수화, 책임 분리, PvP 확장 고려를 강제합니다. 코드 수정 시 유연성과 확장성을 보장합니다.
---

# 확장 가능한 구조 유지

Unity 프로젝트에서 코드를 작성하거나 수정할 때 확장 가능한 구조를 유지하고 하드코딩을 방지합니다.

## 핵심 원칙

### 1. 하드코딩 금지
- **절대 금지**: 매직 넘버, 문자열 리터럴, 하드코딩된 로직
- **예외 처리**: 하드코딩이 불가피한 경우 반드시 사용자에게 확인 요청

### 2. Enum 사용
- 상태, 타입, 옵션은 반드시 enum으로 정의
- switch/case 문에서 enum 사용
- 문자열 비교 대신 enum 비교 사용

### 3. 변수화
- 모든 상수값은 public/private 필드로 변수화
- Inspector에서 조정 가능하도록 [SerializeField] 또는 public 사용
- 설정값은 ScriptableObject나 Config 클래스로 분리 고려

### 4. 책임 분리 (Separation of Concerns)
- 단일 책임 원칙 준수
- 각 클래스는 하나의 명확한 역할만 담당
- 공통 기능은 별도 컴포넌트나 유틸리티로 분리

### 5. PvP 확장 고려
- 현재는 PvE 구조라도 PvP 확장 가능성 고려
- 플레이어 간 상호작용을 위한 인터페이스 설계
- 네트워크 동기화 가능한 구조로 작성

## 코드 작성 가이드라인

### 하드코딩 체크리스트

코드 작성 전 다음을 확인:

- [ ] 매직 넘버가 있는가? → 변수로 추출
- [ ] 문자열 리터럴이 있는가? → const string 또는 enum으로 변경
- [ ] 특정 인덱스나 배열 길이를 하드코딩했는가? → Length 속성 사용
- [ ] 특정 GameObject 이름을 하드코딩했는가? → Tag, Layer, 또는 참조 변수 사용
- [ ] 특정 키 입력을 하드코딩했는가? → Input System의 Action Map 사용

### Enum 사용 예시

```csharp
// ❌ 나쁜 예: 문자열 비교
if (weaponType == "Melee") { ... }

// ✅ 좋은 예: enum 사용
public enum WeaponType { Melee, Range }
if (weapon.type == WeaponType.Melee) { ... }
```

### 변수화 예시

```csharp
// ❌ 나쁜 예: 하드코딩된 값
rigidbody.linearVelocity = bulletPos.forward * 50f;

// ✅ 좋은 예: 변수화
[SerializeField] private float bulletSpeed = 50f;
rigidbody.linearVelocity = bulletPos.forward * bulletSpeed;
```

### 책임 분리 예시

```csharp
// ❌ 나쁜 예: Player 클래스에 모든 기능
public class Player : MonoBehaviour
{
    void Update()
    {
        // 이동, 공격, UI, 사운드 모두 처리
    }
}

// ✅ 좋은 예: 책임 분리
public class Player : MonoBehaviour { } // 이동만
public class PlayerCombat : MonoBehaviour { } // 전투만
public class PlayerUI : MonoBehaviour { } // UI만
```

### PvP 확장 고려 예시

```csharp
// ✅ 좋은 예: 인터페이스 사용으로 확장성 확보
public interface IDamageable
{
    void TakeDamage(int damage, GameObject attacker);
}

public class Player : MonoBehaviour, IDamageable
{
    public void TakeDamage(int damage, GameObject attacker)
    {
        // PvE: attacker는 Enemy
        // PvP: attacker는 Player도 가능
    }
}
```

## 하드코딩이 불가피한 경우

다음 상황에서는 하드코딩이 필요할 수 있으나, **반드시 사용자에게 확인 요청**:

1. Unity API의 필수 상수값 (예: LayerMask.NameToLayer)
2. 프로토타입 단계의 임시 값
3. 성능이 극도로 중요한 경우 (사용자 확인 후)

확인 요청 예시:
> "이 부분은 하드코딩이 필요한 상황입니다. [구체적 이유]. 진행해도 될까요?"

## 코드 수정 워크플로우

1. **분석 단계**
   - 현재 코드의 하드코딩 여부 확인
   - 확장 가능성 평가
   - 책임 분리 필요성 판단

2. **설계 단계**
   - 필요한 enum 정의
   - 변수화할 값 식별
   - 클래스/메서드 분리 계획

3. **구현 단계**
   - 하드코딩 제거
   - enum 및 변수 적용
   - 책임 분리 적용

4. **검증 단계**
   - 하드코딩 체크리스트 재확인
   - PvP 확장 가능성 재평가
   - 사용자 확인 (필요시)

## 주의사항

- **추정 금지**: 코드 맥락이 부족하면 반드시 사용자에게 요청
- **현재 프로젝트 기준 유지**: 기존 Player, Enemy, Camera 구조를 따름
- **점진적 개선**: 한 번에 모든 것을 바꾸지 말고, 수정하는 부분만 개선
