---
name: editor-verify
description: 수정 뒤 "잘 됐는지 확인해", 씬·Hierarchy·콘솔 상태 질문, 스크린샷 요청에 사용한다. Unity MCP 를 읽기 전용으로 써서 상태를 보고하는 절차.
---

# editor-verify — MCP 로 에디터 상태 확인 (읽기 전용)

1. **읽기만** — Hierarchy·컴포넌트 값·콘솔 로그·게임 뷰 스크린샷을 MCP 로 읽는다. 수정 도구는 쓰지 않는다 (수정은 `mcp-edit`). 사용자에게 "확인해 보세요"라고 넘기지 않는다.
2. **읽기도 씬을 바꿀 수 있다** — Collider 를 MCP components 리소스로 읽으면 `collider.material` 접근으로 PhysicsMaterial 이 새로 생겨 씬이 더러워진다. Collider 는 `execute_code` 로 `sharedMaterial` 만 본다.
3. **컴파일 먼저** — 콘솔에 컴파일 에러가 있으면 그것부터 보고한다. 플레이 중이면 컴파일되는 수정(refresh 포함)을 넣지 않는다.
4. **잘됨 / 안됨 으로 보고** — 기대한 것과 실제를 한 줄씩 대조한다. 안 되면 `bug-fix` 절차로 넘어간다.
5. **스크린샷 위치** — `Assets/Screenshots/` 에, 씬·내용이 드러나는 이름으로 (예: `T1_Door2_새우.png`). Temp 금지. 쓸모없는 컷은 지운다.
6. **에디터 체크리스트** — 사용자가 손봐야 할 Inspector 값·씬 배치가 있으면 "오브젝트 → 컴포넌트 → 값" 순으로 목록을 준다.
