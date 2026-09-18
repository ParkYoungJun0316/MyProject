"""t5maze.json -> t5build.txt (Unity 에디터 빌더가 읽는 평문).

`execute_code`의 C#이 JsonUtility로는 중첩 배열을 못 읽어서 평문으로 내린다.
한 줄 = 한 오브젝트. 공백 구분.

  MAP <index> <seed>
  W <name> <cx> <cz> <sx> <sz>      1층 벽 (y=7.5, 높이 15는 빌더가 상수로 둔다)
"""
import json
import sys

d = json.load(open(sys.argv[1] if len(sys.argv) > 1 else "t5maze.json"))

out = []
for i, m in enumerate(d["maps"]):
    out.append("MAP {0} {1}".format(i + 1, m["seed"]))
    for name, cx, cz, sx, sz in m["wallBoxes"]:
        out.append("W {0} {1} {2} {3} {4}".format(name, cx, cz, sx, sz))

open("t5build.txt", "w").write("\n".join(out) + "\n")
print("{0} lines, {1} maps".format(len(out), len(d["maps"])))
