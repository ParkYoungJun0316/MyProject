---
paths:
  - "Assets/**/*.cs"
---

# C# 스크립트 규칙 (Assets/**/*.cs 를 읽거나 고칠 때 적용)

## 경로는 하나 (SSOT)
- 기능 하나에 경로 하나. 같은 일을 하는 두 번째 스크립트·두 번째 분기를 만들지 않는다.
- 새 `.cs`·새 `if` 분기 전에 `Assets/Docs/GameArchitectureBoundaries.md`(도메인 주인)를 읽고, 관련 키워드로 **Assets 전체**를 grep 해서 기존 스크립트를 전부 나열한다. 하나만 읽고 결정하지 않는다.
- 선택지는 재사용 / 확장 / 신규 셋. 어느 것을 왜 골랐는지 사용자에게 보고한 뒤 만든다.
- 상태(HP·목숨·라운드·준비 등)는 바꾸는 주인이 하나. 주인 도메인이 아닌 곳에서 바꾸지 않는다. 주인이 바뀌면 GameArchitectureBoundaries.md 를 같은 수정에서 고친다.

## 출시된 스토리 코드 보호 (서바이벌 작업 시)
- 스토리 모드는 출시됨. 새 모드 분기를 기존 경로에 얹을지는 무게로 정한다: 가볍고 안전하면 기존 + 경로 추가, 기존 코드를 다시 꼼꼼히 테스트해야 할 정도면 전용 스크립트. 기준은 `Assets/Docs/SurvivalDesign.md` §4.3.
- 기존 재사용을 제안할 때마다 "기존 코드에 손대는 양·다시 테스트할 범위"를 같이 말한다. 애매하면 전용 스크립트 쪽.

## 네트워크 (NGO, `Assets/Docs/NetworkDesign.md` 가 SSOT)
- 판정·HP·규칙은 Host. 이동은 Owner + `ClientNetworkTransform`. 클라는 게임플레이 `NetworkVariable` 을 직접 쓰지 않는다.
- 연속 상태는 `NetworkVariable`, 한 번 일은 `ClientRpc`, 클라 → Host 요청은 `ServerRpc` + Host 검증.
- 데미지는 `NetworkDamageUtil` 하나, 나가기는 `DisconnectManager` → `TitleReturnFlow` 하나, 씬 전환은 `SceneFlowManager` 하나. 평행 경로 금지.
- 재접속·중간 합류·호스트 이전은 없다. 그 경우를 위한 설계를 넣지 않는다.

## 중복 호출 금지
- "호출이 안 된 것 같으니 한 번 더 부른다"는 금지. 대신 호출 경로를 추적한다: 누가 → 누굴 → 어느 순서로.
- 가드 = 같은 호출이 두 번 와도 안전하게 만드는 것(`if (initialized) return;`, null 체크). OK.
- 중복 호출 = 원래 경로가 돌았는지 모른 채 한 번 더 부르는 것. NEVER.
- 새 호출을 넣기 전에 같은 메서드 호출을 grep 해서 이미 어디서 불리는지 확인한다. RPC·이벤트는 Host/클라 양쪽에서 몇 번 도는지도 본다.

## 없는 것 만들지 않기
- 없는 클래스·메서드·API 를 지어내지 않는다. 먼저 repo grep, 없으면 사용자에게 묻는다.
- Unity API 는 이 버전(6000.3) 에 있는지 확인한다. 기억이 애매하면 소스나 선례를 찾는다.

## 에디터 ≠ 빌드
- 출시 빌드는 Windows IL2CPP. 리플렉션·`dynamic`·`System.Diagnostics.Process`·`System.Reflection.Emit`·에디터 전용 네임스페이스(`UnityEditor`)는 빌드에서 조용히 깨진다. 빌드 경로에서 플랫폼 API 를 쓰면 먼저 `C:/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Data/il2cpp/libil2cpp/icalls/` 소스와 프로젝트 선례로 확인한다. 에디터 전용 코드는 `#if UNITY_EDITOR` 로 감싼다.
- "에디터에서 됐다"는 "된다"가 아니다. 멀티는 ParrelSync 클론 또는 로컬 빌드 두 개로(LAN 아님), Steam 관련은 Steam 빌드로 일찍 확인하라고 사용자에게 말한다.

## 코드 스타일
- 기존 프로젝트 패턴을 먼저 따른다. 컴포넌트 참조는 `Awake`/`Start` 에서 캐시, `Update` 안 `GetComponent`/`Find` 금지. UI 는 구독·표시만 하고 씬 오브젝트를 이름으로 찾지 않는다.
- 시스템 간 신호는 이벤트(C# event / UnityEvent). 서로 직접 참조해서 호출하는 그물을 만들지 않는다.
- 튜닝값(체력·속도·데미지·시간)은 `[SerializeField]` 로 Inspector 에 노출. 코드에 숫자 박지 않는다.
- 진단용 로그는 `Debug.Log("[DBG] ...")` 로 표시하고, 버그가 닫히면 같은 수정 범위 안에서 지운다.
- 플레이 모드 중에는 컴파일되는 수정을 넣지 않는다. 사용자가 플레이 중이면 멈추고 알린다.
