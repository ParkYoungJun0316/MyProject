// PostToolUse(Write): 아직 커밋 안 된 .cs 를 썼으면 재사용 검토·도메인 주인 확인을 상기시킨다.
// 판단은 못 하고 "잊는 것"만 막는다. 커밋되면 조용해진다 (수정 하나 = 커밋 하나).
const fs = require("fs");
const { spawnSync } = require("child_process");
let input = {};
try { input = JSON.parse(fs.readFileSync(0, "utf8")); } catch (e) { process.exit(0); }
const raw = ((input.tool_input || {}).file_path || "").split("\\").join("/");
if (!raw.toLowerCase().endsWith(".cs") || !raw.includes("/Assets/")) process.exit(0);

const r = spawnSync("git", ["ls-files", "--error-unmatch", raw], { encoding: "utf8" });
if (r.error) process.exit(0);          // git 없음 등 — 조용히 통과
if (r.status === 0) process.exit(0);   // 이미 추적 중인 파일 — 새 파일 아님

const name = raw.split("/").pop();
process.stderr.write(
  `[hook:new-script] 미커밋 스크립트 작성: ${name}\n` +
  `확인하고 빠졌으면 지금 한다:\n` +
  `  1. 같은 역할의 기존 스크립트를 Assets 전체에서 grep 했고, 재사용/확장 대신 새로 만든 이유를 사용자에게 보고했는가\n` +
  `  2. Assets/Docs/GameArchitectureBoundaries.md 의 어느 도메인 소속인지 정했는가 — 새 상태 주인·새 의존이면 그 문서도 고쳤는가\n` +
  `  3. 네트워크 동기화가 끼면 Assets/Docs/NetworkDesign.md 권한 표(Host 판정·Owner 이동·NetworkDamageUtil 단일 경로)와 맞는가\n` +
  `  4. 서바이벌용이면 Assets/Docs/SurvivalDesign.md §4.3(스토리 코드 보호)에 따라 기존 스토리 경로에 손댄 양·다시 테스트할 범위를 보고했는가\n` +
  `끝나면 사용자에게 커밋을 권한다 (수정 하나 = 커밋 하나).\n`
);
process.exit(2);
