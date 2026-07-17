# M Stage Network Board

> **역할:** 미확정 파이프라인을 여기서 잡고, **확정되면 [`NetworkDesign.md`](NetworkDesign.md) §9 / §9.1로 승급**한다.  
> (예: 발사체 **B안** — 보드·논의 → Docs 고정.)  
> **빈 체크리스트 전용이 아님.** 큰 틀을 정하기 위한 작업 md.

**현재 인게임 최우선:** M.Stage 네트워크 완료 (`NetworkDesign` §9.1).  
**현재 보드 포커스:** `M.Stage2` · **C 패턴 · OX Quiz** (이후 챌린지 공통 템플릿).

---

## 0. Board → Docs 승급 규칙

| Board (여기) | NetworkDesign |
|--------------|---------------|
| 후보·논의·미결 | 확정 lock |
| 파이프라인 초안 | §9.1 / 권한 표에 한 줄로 고정 |
| 구현 중 변경 | 승급 후에만 Docs 수정 |

승급 조건: ParrelSync **2인**으로 OX 1회 클리어(시작→판정→데미지→AllCleared) + 아래 **§2 계약**에 미결 없음.

---

## 1. C 패턴 — 공통 파이프라인 (OX로 잠글 틀)

다른 C(ColorTile, Grid, Ring…)에 **그대로 복제**할 골격. OX에서 먼저 확정.

```
[시작]  Host만 퀴즈/라운드 시작 확정
   → 배리어·UI 등 결과 상태 브로드캐스트
[진행]  Host만 타이머·문제(또는 라운드) 인덱스 진행
   → Client는 표시(문제 텍스트·타일 색·타이머 UI)
[판정]  Host만 정답/안전영역 판정 (물리 오버랩·시드 포함)
[피해]  Host → NetworkDamageUtil만 (B 규칙). Client 단독 데미지 금지
[클리어] Host → Objective.Complete → StageManager (D 규칙)
[리셋]  사망 리로드 시 Host가 챌린지 상태 초기화 (D Reset과 정합)
```

**타일/발판(A):** `OXQuizTile` 등은 **상태 표시 + 오버랩 조회 헬퍼**만. 정답·데미지·다음 문제 결정 금지.

---

## 2. OX — 잠가야 할 규칙 (미결 → 확정 후 Docs 승급)

### 2.1 권한

| # | 항목 | 후보 | 상태 |
|---|------|------|------|
| Q1 | 퀴즈 상태머신 (`OXQuizManager`) | **Host only** 진행 | ⬜ 확정 후보 |
| Q2 | Trigger로 퀴즈 시작 | Host만 `StartQuiz` (Client Trigger → 무시 또는 ServerRpc 요청→Host 검증) | ⬜ 미결 |
| Q3 | 문제 셔플 / `questionsPerRun` | **Host만** `Random` · 시드 필요 시 Host 시드 동기화 | ⬜ 확정 후보 |
| Q4 | 타이머 종료·오버랩 판정 | **Host만** | ⬜ 확정 후보 |
| Q5 | 오답·무응답 데미지 | **Host** `NetworkDamageUtil.ApplyDamage` | ⬜ 확정 후보 (코드 경로 있음 — Host 게이트 확인) |
| Q6 | `OnAllCleared` → Objective | Host에서만 Complete (D) | ⬜ 확정 후보 |
| Q7 | 배리어 `DoorController` | Host Open/Close + 기존 Door 동기화 경로 재사용 | ⬜ 미결 (DoorNetworkSync 정합) |
| Q8 | 문제/해설/진행 UI | Host 이벤트 또는 NV/ClientRpc로 문구·인덱스 복제 | ⬜ 미결 (수단 택1) |

### 2.2 동기화 수단 (파이프 핵심 — 여기서 하나 고르면 Docs 승급)

C 챌린지 **공통**으로 쓸 수단. OX에서 정하면 ColorTile 등에 동일 적용.

| 옵션 | 내용 | 비고 |
|------|------|------|
| **α** | `NetworkVariable` (phase, questionIndex, timer end, flags) | 상태 복원·늦은 관찰에 유리 |
| **β** | 단계마다 `ClientRpc` (QuestionReady / Reveal / Cleared) | 구현 단순, 이벤트형 |
| **γ** | 혼합: 핵심 진행 NV + 연출 Rpc | 추천 후보 |

**결정:** ⬜ α / β / γ ________  (정하는 즉시 §9.1 또는 본 Board→Docs)

### 2.3 로컬 전제 (현 코드)

- `OXQuizManager` = `MonoBehaviour` (아직 NetworkBehaviour 아님).
- 데미지는 `NetworkDamageUtil` 호출 있음 → **Host에서만 호출되도록** 게이트가 파이프 필수.
- `Random.Range` 셔플 있음 → Client 실행 시 문제 순서 분기하므로 **Host 전용** 필수.

---

## 3. OX 파이프라인 초안 (합의용)

```
Player Trigger (영역 진입)
  → [Q2] Host: StartQuiz
  → Host: barrier Open + (동기화)
  → Host: pick/shuffle questions [Q3]
  → loop:
       Host: push question + start timer  [Q8 동기화]
       Client: UI·타일 Pending 표시 (A)
       Host: timer end → overlap O/X [Q4]
       Host: wrong → ApplyDamage [Q5]
       Host: reveal + delay
  → Host: AllCleared → barrier Close + OXQuizObjective.Complete [Q6]
```

사망 시: 기존 D(`StageResetOnPlayerDeath`) + Manager `ResetQuiz`가 **Host에서** 재구독·상태 리셋되는지 보드에서 확인 후 Docs에 한 줄.

---

## 4. 승급 체크 (OX 틀 → NetworkDesign)

- [ ] §2 Q1–Q8 미결 해소 (수단 γ/α/β 포함)
- [ ] C 공통 파이프라인(§1) 문구 Docs §9.1에 반영
- [ ] 2인: Trigger→클리어 1회, Client도 같은 문제·같은 판정 체감
- [ ] Client만의 데미지/셔플/Complete 없음
- [ ] 다음 보드 포커스 → `M.Stage3` ColorTile (동일 C 파이프 복제)

---

## 5. 다음에 하지 말 것 (이 보드 범위 밖)

- WindTrap Host 힘 상세 (B Should — M5)
- T 스테이지 C
- Steam / 텔레메트리
- A–F 표 재작성 (이미 NetworkDesign §9.1)
