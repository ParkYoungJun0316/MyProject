---
name: unity-inspector-checklist
description: Unity 코드 수정 시 Inspector 설정 항목들을 체크리스트로 안내합니다. Layer, Collider, Rigidbody, Cinemachine, NavMesh, Serialized Field 연결 여부를 확인합니다. Unity 프로젝트에서 스크립트를 수정하거나 새 컴포넌트를 추가할 때 사용합니다.
---

# Unity Inspector 체크리스트

Unity 코드를 수정하거나 새 컴포넌트를 추가할 때, 다음 Inspector 설정 항목들을 반드시 확인하고 사용자에게 안내합니다.

## 체크리스트 템플릿

코드 수정 후 다음 체크리스트를 사용자에게 제시합니다:

```markdown
## Inspector 설정 확인 사항

다음 항목들을 Unity Inspector에서 확인해주세요:

- [ ] **Layer 설정**: GameObject의 Layer가 올바르게 설정되었는지 확인
- [ ] **Collider 설정**: 필요한 Collider 컴포넌트가 추가되고 설정되었는지 확인
- [ ] **Rigidbody 설정**: 물리 시뮬레이션이 필요한 경우 Rigidbody 설정 확인
- [ ] **Cinemachine 설정**: 카메라 관련 스크립트인 경우 Cinemachine 설정 확인
- [ ] **NavMesh 설정**: AI 이동이 필요한 경우 NavMesh Agent 및 NavMesh 설정 확인
- [ ] **Serialized Field 연결**: Inspector에서 연결해야 하는 public 필드나 [SerializeField] 필드가 제대로 연결되었는지 확인
```

## 각 항목별 상세 가이드

### Layer 설정
- GameObject의 Layer가 적절한지 확인 (예: Player, Enemy, Ground, UI 등)
- Layer 간 충돌 설정이 올바른지 확인 (Edit > Project Settings > Physics > Layer Collision Matrix)

### Collider 설정
- 필요한 Collider 타입 확인 (BoxCollider, SphereCollider, CapsuleCollider, MeshCollider 등)
- Is Trigger 옵션이 의도한 대로 설정되었는지 확인
- Collider 크기와 위치가 적절한지 확인

### Rigidbody 설정
- 물리 시뮬레이션이 필요한 경우 Rigidbody 컴포넌트 추가 여부 확인
- Use Gravity, Is Kinematic 설정 확인
- Constraints 설정 확인 (Position, Rotation 고정 여부)

### Cinemachine 설정
- 카메라 관련 스크립트인 경우 Cinemachine Virtual Camera 설정 확인
- Follow Target, Look At Target 연결 여부 확인
- Cinemachine Brain 컴포넌트가 Main Camera에 있는지 확인

### NavMesh 설정
- AI 이동이 필요한 경우 NavMesh Agent 컴포넌트 추가 여부 확인
- NavMesh가 Scene에 베이크되었는지 확인 (Window > AI > Navigation)
- NavMesh Agent의 Base Offset, Speed, Acceleration 등 설정 확인

### Serialized Field 연결
- public 필드나 [SerializeField] 속성이 있는 필드가 Inspector에서 연결되었는지 확인
- 필수 필드가 null이 아닌지 확인
- 배열이나 리스트의 요소들이 올바르게 할당되었는지 확인

## 사용 시나리오

이 체크리스트는 다음 상황에서 사용합니다:

1. **새 스크립트 추가 시**: 새 컴포넌트를 추가할 때 필요한 Inspector 설정 안내
2. **기존 스크립트 수정 시**: 코드 변경으로 인해 Inspector 설정이 필요한 경우
3. **컴포넌트 간 상호작용 추가 시**: 다른 컴포넌트와의 연결이 필요한 경우
4. **물리/충돌 관련 기능 추가 시**: Collider, Rigidbody 설정이 필요한 경우
5. **AI/이동 기능 추가 시**: NavMesh 설정이 필요한 경우
6. **카메라 기능 추가 시**: Cinemachine 설정이 필요한 경우

## 안내 방식

코드 수정이 완료되면 다음과 같이 안내합니다:

1. 코드 변경 사항을 설명한 후
2. "다음 Inspector 설정을 확인해주세요:" 라는 문구와 함께
3. 위의 체크리스트를 마크다운 형식으로 제시
4. 특히 변경된 코드와 관련된 항목은 강조 표시

## 예시

```markdown
코드 수정이 완료되었습니다. Player 스크립트에 Rigidbody를 사용하는 이동 로직을 추가했습니다.

## Inspector 설정 확인 사항

다음 항목들을 Unity Inspector에서 확인해주세요:

- [ ] **Rigidbody 설정**: Player GameObject에 Rigidbody 컴포넌트가 추가되었는지 확인 (Use Gravity는 false로 설정)
- [ ] **Collider 설정**: Player GameObject에 적절한 Collider가 설정되어 있는지 확인
- [ ] **Layer 설정**: Player GameObject의 Layer가 "Player"로 설정되어 있는지 확인
- [ ] **Serialized Field 연결**: Speed, JumpForce 등의 필드가 Inspector에서 설정되었는지 확인
```
