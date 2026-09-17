# T5MazeGen — T.Stage5 러너 미로 생성기

`Assets/Docs/TStage5RunnerRedesign.md` M단계에서 쓴 도구. Unity 밖에서 도는 순수 Python 스크립트라
빌드에 포함되지 않는다. 맵을 다시 뽑거나 규칙을 바꿀 때만 사용한다.

## 파일

| 파일 | 역할 |
|---|---|
| `t5gen.py` | 12×12 미로 7개 생성 + 인원별 색 매핑 전수 BFS 검증 → `t5maps.json` |
| `t5emit.py` | `t5maps.json` → `t5layout.txt` (씬 배치용 로컬 좌표. 벽 병합까지 끝낸 형태) |
| `t5verify.py` | 씬에서 읽어 덤프한 `scene_dump.txt`를 `t5maps.json`과 대조 + BFS 재검증 |
| `t5maps.json` / `t5layout.txt` | 현재 씬에 배치된 맵 7개의 확정 데이터 |

## 실행

```bash
python t5gen.py t5maps.json     # 생성 + 검증 (시드는 t5gen.main()의 seed=5000부터 순차 탐색)
python t5emit.py                # 배치용 좌표 산출
python t5verify.py              # 씬 덤프와 대조 (scene_dump.txt 필요)
```

## 검증 규칙 (`t5gen.py`)

- 정답 경로 길이 52~64칸 — 맵 간 난이도를 맞추기 위한 제약
- 정답 경로 위 문 10~12개, 연속한 두 문은 서로 다른 색
- 고유색 문은 경로상에서 서로 인접하지 않게 배치 — 2인(고유 4슬롯이 한 색으로 합쳐짐)에서도 색 전환이 유지되도록
- 색 배분 고정: Blue/Purple/Green/Yellow 각 2, Black 4, White 3 = 15
- BFS 상태 = (칸, 열린 색). 색 전환 1회 = 비용 1, 이동 = 비용 0
- 2인·3인(24가지)·4인(12가지)·설계색 매핑 **전부**에 대해 도달 가능 + 최소 전환 8~12 확인
- 솔로(전부 열림) 도달 가능 확인
- 루프로 정답 경로의 문을 우회할 수 없음 — 우회 시 최소 전환 수가 기준 미달이 되어 자동 탈락

`scene_dump.txt`는 씬에서 벽·문·시작/골 좌표를 읽어 격자 인덱스로 되돌린 것이다.
덤프 코드는 문서 M3 절 참조.
