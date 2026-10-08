---
name: scene-noise
description: 씬 저장 잡음(UI.prefab RectTransform 오버라이드) 정기 검사. 커밋을 권하기 직전, MCP 로 씬을 저장한 직후, 사용자가 "잡음 확인/정리"를 말할 때 사용한다.
---

# scene-noise — 씬 잡음 정기 검사

잡음 = 씬을 저장할 때 Unity 가 UI.prefab 인스턴스의 위치·크기 오버라이드(m_AnchorMin/Max·m_AnchoredPosition·m_SizeDelta·m_Pivot 등)를 같이 바꿔 쓰는 것. 게임 UI 배치가 조용히 바뀔 수 있어서 커밋 전에 걷어낸다.

Claude 는 `.unity` 를 손으로 고치지 않는다. **검사·보고는 Claude, 지우기는 허락받은 뒤 Claude 가 스크립트로.**

## 언제
- 커밋을 권하기 직전 — `git status` 에 `.unity` 가 있으면 항상.
- MCP 로 씬을 저장한 직후.
- 사용자가 "잡음 확인 / 정리" 라고 할 때.

## 순서
1. **검사(읽기 전용)** — `node Tools/SceneNoise/strip-scene-noise.js` 를 그대로 실행한다(`--apply` 없이). 바뀐 씬 전부를 HEAD 와 비교해 목록만 낸다. 허락 전에는 `--apply` 를 붙이지 않는다.
2. **판단** — 목록에 사용자가 **의도해서 바꾼 UI 위치**가 섞였는지 본다. 이번에 UI 배치를 일부러 바꾼 작업이 있었으면 그 항목을 짚어 사용자에게 묻는다.
3. **보고·허락** — 쉬운 말로: "씬 N개에 잡음 M줄. 지울까요?" 허락받으면 Claude 가 실행한다.
   ```
   node Tools/SceneNoise/strip-scene-noise.js --apply
   ```
   (특정 씬만이면 뒤에 경로를 붙인다.)
4. **뒤처리 안내** — 그 씬이 에디터에 열려 있으면 저장하지 말고 디스크에서 다시 열어야 한다고 말한다(안 그러면 다음 저장 때 잡음이 되돌아온다).
5. **다시 검사** — 실행한 뒤 1번을 다시 돌려 0 이 됐는지 확인하고 커밋을 권한다.

## 스크립트가 다루지 않는 것
- "없어진 오버라이드" 는 보고만 한다(자동 복구 안 함). 나오면 사용자에게 알린다.
- UI.prefab 말고 다른 프리팹 잡음이 보이면 `--any-prefab` 으로 검사만 해 보고 보고한다.
