---
name: lifecycle-fix
description: >-
  Player lifecycle diagnose ladder — SSOT: NetworkDesign.md §11 (axis/doors/
  forbidden/symptom tables live there only, not restated here). Entry point
  and walk order come from §11.5, not a fixed direction. Output format
  replaces Bug Hunter report for lifecycle bugs — do not mix. Use for death/
  reload/spawn/OnPlayersReady/camera/HP bugs, or invoke explicitly with
  @lifecycle-fix. Diagnose only until user OK.
---

# Lifecycle Fix (diagnose)

**SSOT: `Assets/Docs/NetworkDesign.md` §11.** 축·문·금지·증상표는 여기서 복사하지
않는다 — Docs가 유일한 원문 (`architecture.mdc`: "Docs를 승인 없이 Rules/Skills로
자동 승격하지 말 것" — 이 승격은 사용자 승인 하에 진행됨). 이 스킬은 그 위에 **워크
플로 제약 + 절차 + 출력 포맷**만 얹는다.

관련성이 있으면 자동으로 쓸 수 있고, 명시적으로 **@lifecycle-fix**로도 호출 가능.
**코드 수정 금지** — 사용자 OK 후 **Cause-Site Only**(`diff-only`). lifecycle
버그는 Bug Hunter 리포트 포맷 대신 **이 스킬의 출력 포맷을 쓴다** (혼용 금지).

## When to use

- 사망/리로드 후 카메라 미바인드
- HP 미리셋 / 플레이어 실종 / 죽은 포즈 잔존
- 입력 안 됨·이중 입력 (③ Owner)
- 스폰 수 오류 / `OnPlayersReady` 미발행
- 수명주기 축을 분기 없이 유지하라고 할 때

## When NOT to use

- 함정·발사체·일반 UI만 → @bug-hunter (+ @ngo-debug)
- 새 기능 설계 → @plan-first
- 이미 제안 승인("수정해") 후 → 구현만, §11.4 금지 목록 API 되살리지 않기

## Read first (코드 보기 전에)

`Assets/Docs/NetworkDesign.md`:
- **§11.0** — 축 표 (①Load ②Spawn ③Owner ④Ready ⑤Play) + Writer 유일 열
- **§11.1** — ①로 들어오는 문 (표 내용만 신뢰 — Docs 자체 문 개수 표기가
  §11.0/§11.1 사이에서 다르니 개수는 하드코딩하지 말 것)
- **§11.4** — 금지된 평행 축 (`Respawn`, `ForceRespawn`, `ReloadCurrentScene`, 수동
  `OnPlayersReady` Invoke, DDOL 플레이어 가정 등)
- **§11.5** — 증상 → 먼저 볼 칸 표. **진입 칸과 검증 순서는 여기서 정한다.**
  방향은 증상마다 다름 (예: 카메라=④→⑤, UI/패드=⑤→④) — 고정된 "항상 ⑤부터"
  같은 순서를 강제하지 않는다

## 워크플로 제약 (Docs에 없는, 이 스킬이 추가하는 것)

1. **진입 = §11.5 매핑.** 증상에 맞는 행의 "먼저 볼 칸"에서 시작해 "그다음" 순서로
   검증. **첫 실패에서 정지.**
2. **Writer 유일 재확인** — 고칠 때 그 칸의 Writer(§11.0 Writer 열)만 수정.
   Consumer/증상 사이트에 복구 분기 추가 금지.
3. §11.4 목록의 이름은 **원인 후보 1순위** — grep으로 먼저 확인.
4. 진단만 — **사용자 OK 전 코드 금지.** OK 후엔 `diff-only`(Cause-Site Only)로
   원인 칸만 수정.

## Procedure

1. **Read** `NetworkDesign.md` §11.0 / §11.1 / §11.4 / §11.5 (필요한 만큼만).
2. **Entry** — §11.5에서 증상과 일치하는 행 찾기. "먼저 볼 칸"에서 시작.
3. **Walk** — 그 행이 지정한 순서("먼저" → "그다음")로 검증. 순서·방향은 증상마다
   다를 수 있음 (Docs 순서를 따를 것, 임의로 ⑤부터 강제하지 않음). **첫 실패에서
   정지.**
4. **Classify**
   - 로컬 결함 (구독 타이밍 등) → 그 파일만
   - 계약 결함 (DDOL vs `destroyWithScene`, Respawn 이중 등) → 계약 한 줄 +
     소비자 목록 → 한 패스 정렬 + 옛 경로 삭제
5. 아래 포맷으로 출력 후 **중단**.

## Output format

**Symptom:** [증상 + §11.5 매칭 행]
**Entry:** [§11.5가 지정한 진입 칸]
**Ladder walk:** [진입 칸]=[ok/fail+근거] → [다음 칸]=… (Docs 순서대로, 첫 fail에서 멈춤)
**Broken step:** [①-⑤ 또는 사망/클리어 문]
**NOT broken:** [칸 + 이유]
**Root cause:** [근거]
**Fix proposal:** [원인 지점(Writer) + 같은 가정 파일 · 새 분기 없음]
**Verify:** [ParrelSync/Build로 재확인할 시나리오]
**Impact:** [다른 Consumer/Client/솔로에 영향 있는지]
**Out of scope:** [예: ① 수정 금지]
**Status:** waiting for user OK to implement

## Don't

- Docs §11 표를 이 파일에서 다시 베껴 쓰지 않기 (Docs/Skill 이중 동기화 방지)
- §11.5 무시하고 고정 방향(예: 항상 ⑤부터)으로 걷기
- 근거 없이 ① 또는 사망/클리어 문으로 에스컬레이션
- 옛/새 수명주기 병행
- 이 턴에서 코드 수정
- 이 출력 포맷과 Bug Hunter 리포트 포맷 혼용 (lifecycle 버그면 이 포맷만 사용)
