# Cursor 바이브 코딩 셋업 — 공동 작업용 브리프 (개정)

> 이 문서는 Cursor AI 환경 재구축의 SSOT 브리프다. 단계 작업 시 이 파일을 기준으로 한다.

## 0. 이 브리프의 목적

1인 Unity 개발용 **Cursor AI 환경(Rules / Skills / Sub-agent / MCP / AGENTS)** 을 **처음부터 다시** 구축한다.  
유행 킷을 잔뜩 받지 말고, **이 게임(4인 협동·NGO·Steam 목표)** 에 필요한 것만 선별한다.

---

## 1. 프로젝트 맥락 (사실 — AI가 임의 변경 금지)

### 스택

- Unity **6.3 LTS (6000.3.9f1)**, C#, URP, Input System, uGUI
- 네트워크: **Netcode for GameObjects 2.9**
- **Steamworks 미연동.** Transport는 추후 Steam Networking 교체 예정  
  → Facepunch / Steamworks.NET / Mirror / Photon / Relay **임의 제안·단정 금지**. 미정이면 **질문**
- 카메라: **TopDownCamera** (Cinemachine을 카메라 SSOT로 취급 금지)

### 테스트 환경 (중요)

- **LAN 디스커버리/실기 LAN은 현재 안 됨**
- 테스트는 **1컴**에서:
  - **ParrelSync** (에디터 클론)
  - **빌드 + 에디터/빌드** 조합
- AI는 “LAN으로 붙여서 테스트하라”고 가정하지 말 것. ParrelSync/빌드 경로를 기본으로 할 것

### 장르·아키텍처

- 4인 협동 생존/액션, Steam 출시 목표
- Listen-Server (Host + Clients)

### 권한 — 기본 방향 (단, Docs 검토 전 Rules 확정 금지)

- 게임 판정(HP·데미지·함정 히트·스테이지): **Host**
- 플레이어 입력·카메라·로컬 연출: **Owner**
- 이동 권한 최종형(Owner CNT vs Host 이동)은 `NetworkDesign` §9A 등과 **코드 현실이 어긋날 수 있음** → **문서 전수 검토 + 사용자 승인 전 Rules에 못 박지 말 것**

### 발사체/ArrowTrap — 사용자 확정 해결 방향 (버그: Client에서 끊기며 옴)

**합의된 방향 (사용자가 명시):**

1. **Host:** 발사체 **스폰 + 초기 속도(velocity) 설정**
2. **Client:** 그 속도를 **받아서 스스로 비행**(로컬에서 날림)  
   → Client에서 화살이 끊기며 오는 문제 대응

**AI 주의:**

- 현재 코드(`ArrowTrap`)는 Host 스폰·Host velocity 후 `NetworkObject.Spawn` 중심
- `NetworkDesign`은 발사체·물리를 Host 쪽으로 더 넓게 서술하는 구간 있음
- 따라서 이 발사체 모델은 **브리프상 “사용자 합의 방향”** 이며,  
  **Docs 정리·사용자 검토 통과 후에만** Multiplayer Rule / AGENTS에 공식 문장으로 편입
- AI가 “전부 Host 물리” 또는 “전부 Client 예측”으로 멋대로 통일하지 말 것

### 세션·Docs 정책 (매우 중요)

- `Assets/Docs/NetworkDesign.md`
- `Assets/Docs/CheerAndTutorialDesign.md`
- `Assets/Docs/PostMVP_Multiplayer_Backlog.md`
- 기타 Docs  
→ **셋업 과정에서 전수 검토 대상**
- Docs 내용이 Rules/Skills/AGENTS에 들어가야 할 때가 있으면 **반드시 사용자에게 검토·승인 받을 것**
- **Rules에 Docs 내용을 함부로 자동 적용하지 말 것**
- 세션 이탈·재접속 등 정책도 **Docs 검토 패키지에 포함**해 사용자 재확인 후 SSOT화 (코드와 불일치 가능 → 구현 맞춤은 별 티켓)

### 기존 Cursor 설정 — 폐기

- 기존 `.cursor/skills/**` **전부 삭제** (처음부터 재구축)
- 기존 rules/mdc가 있으면 **전부 삭제**
- **이전 skill/rule 내용은 잊을 것.** 재사용·요약 이식 금지. 필요하면 1단계에서 외부 소스로만 다시 가져옴
- User Rules(Cursor 설정 UI)는 이 브리프 범위 밖일 수 있음 → 프로젝트 `.cursor`는 클린 슬레이트로 시작

---

## 2. 큰 목표 (3단계) + 보강

### 뼈대 (유지)

1. **가져오기** — 검증·실사용 + 나에게 필요한 AI 구조만 선별  
2. **정리·삭제** — 요약 보고 → 불필요 삭제 → **삭제 목록 / 채택 목록**  
3. **커스터마이징** — **먼저 docs로 바꿀 계획 합의** → 그다음 파일 수정  

### 보강 단계 (채택)

| 단계 | 내용 |
|------|------|
| **0** | 클린 슬레이트 + 브리프/필터 SSOT (기존 skill/rule 삭제, Docs는 “검토 대기”로만 링크) |
| **0.5** | **Docs 전수 검토** (NetworkDesign, Cheer…, PostMVP…) — 사용자 승인 전 Rules 반영 금지 |
| **1** | 외부 구조 선별 가져오기 (임시 폴더만) |
| **2** | 요약·삭제·채택 |
| **2.5** | 충돌 검사 (외부 vs 브리프 vs **승인된** Docs 문장만) |
| **3-A** | Customization Spec 문서 승인 |
| **3-B** | Spec대로 `.cursor` 적용 |
| **4** | MCP + ParrelSync/빌드 스모크 |

**금지:** 킷 일괄 다운로드, Mirror/Photon/ECS/UI Toolkit/웹 킷, Docs→Rules 자동 반영, LAN 테스트 가정

---

## 3. 목표 최소 구성 (파일은 3단계 후 생김 — 0단계 직후 기존 skill 없음)

### 필수 문서

- 루트 `AGENTS.md` — **신분증만** (스택, 테스트 환경, 1차 밴, Docs 링크). 권한·발사체·세션 세부는 넣지 않음 → Phase 0.5 Docs 검토 후 Rule/Docs로  

- Docs 검토 로그: `.cursor/docs/docs-review.md` (항목별: 유지/수정/Rules 반영 여부 / 사용자 승인)  
- 3단계: `.cursor/docs/customization-spec.md`  
- 1단계 필터: `.cursor/docs/import-filter.md`

### Rules (예정 7 — Docs 승인 문장만 반영)

1. Unity Coding  
2. Multiplayer (NGO) — **승인된** 권한·발사체·세션 정책만  
3. Architecture — 추측 구현 금지  
4. Bug Hunting — 재현→원인→최소수정→영향범위  
5. Git — 커밋 전 요약  
6. Plan First  
7. Diff Only  

### Sub-agent 역할

- 개념상 상시: Multiplayer Debug, Bug Hunter  
- 요청 시: Explore, Code Review, Architecture Review  
- Explore는 **요청시 권장** (상시 비추천)

### Skills (예정, 기존 폐기 후 신규만)

- NGO 디버그 (재현 템플릿 + 로그 포맷) — ParrelSync/빌드 전제  
- ParrelSync + 1컴 빌드 테스트 헬퍼 (캐시/세이브/로그 경로 분리)  
- Commit Generator  
- (선택) README Generator  
- Steam 체크리스트는 **Steam 착수 후**

### MCP

1. Unity Editor MCP  
2. GitHub MCP  
3. NGO/Steam 문서는 연동 시점  

---

## 4. 단계별 작업 지시서

### 0단계 — 클린 슬레이트 + 필터

**사람:** 이 브리프 승인, 기존 `.cursor/skills`(및 rules) 삭제 승인  

**AI:** skill/rule 삭제, `AGENTS.md`(신분증만)·`import-filter.md` 초안. **권한·발사체·세션 세부는 AGENTS/Rules에 쓰지 말 것** → 0.5

**완료:** 프로젝트 `.cursor`에 구 skill/rule 0개, AGENTS(슬림)·filter 초안 존재

### 0.5단계 — Docs 전수 검토 (Rules 반영 게이트)

대상: NetworkDesign, CheerAndTutorialDesign, PostMVP_Multiplayer_Backlog, 기타 Docs.  
**승인 없이 Rules/Skills에 Docs 내용 적용 금지.**

### 1단계 — 가져오기 (임시 폴더만)

필터 통과분만 `_cursor_import/` (gitignore 권장). 본 프로젝트 `.cursor` 덮어쓰기 금지.

### 2단계 — 요약 → 삭제 → 채택

`import-review.md`: 삭제된 파일 / 가져갈 파일

### 2.5단계 — 충돌 검사

`conflict-matrix.md`

### 3-A — Customization Spec (파일 수정 전 필수)

`customization-spec.md` 사용자 승인 후만 3-B

### 3-B — Spec 적용

`.cursor/rules/*.mdc`, `skills/*/SKILL.md`, AGENTS 갱신

### 4단계 — MCP + 테스트 루프

Unity Editor MCP, GitHub MCP. ParrelSync/빌드 스모크. LAN 테스트 항목 넣지 말 것.

---

## 5. 전 단계 하드 가드

1. 없는 클래스/API 가정 금지 → 검색 또는 질문  
2. **Docs → Rules 자동 적용 금지** (사용자 검토 필수)  
3. Steam/Mirror/Photon/Facepunch 단정 금지  
4. **LAN 테스트 가정 금지** (ParrelSync/빌드)  
5. 기존 skill/rule 내용 재활용 금지 (삭제 후 재구축)  
6. 3-A 전 커스텀 수정 금지  
7. Plan First / Diff Only  
8. 발사체 모델을 Host-only 물리로 멋대로 되돌리거나, 미승인 채 Docs와 다르게 Rules화 금지  

---

## 6. 한 줄 요약

**구 skill/rule은 폐기하고 처음부터 다시 짓는다.**  
테스트는 **LAN이 아니라 ParrelSync+빌드(1컴)**.  
발사체는 **Host 스폰·초기속도 / Client 자가비행** 방향으로 가되 **Docs 검토·사용자 승인 전 Rules화 금지**.  
큰 흐름은 **0 클린 → 0.5 Docs 검토 → 1 선별 가져오기 → 2 삭제·채택 → 3 Spec 후 커스텀 → 4 MCP**.
