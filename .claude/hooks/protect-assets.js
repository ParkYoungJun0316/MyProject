// PreToolUse(Edit|Write|MultiEdit|NotebookEdit): 씬·프리팹·에셋·설정 파일 수정 차단.
// 이 파일들은 사용자가 에디터에서 만진다. 에이전트는 .cs / Docs / .claude 만 수정한다.
const fs = require("fs");
let input = {};
try { input = JSON.parse(fs.readFileSync(0, "utf8")); } catch (e) { process.exit(0); }
const ti = input.tool_input || {};
const raw = ti.file_path || ti.notebook_path || "";
if (!raw) process.exit(0);
const p = raw.split("\\").join("/");

const protectedExt = [
  ".unity", ".prefab", ".asset", ".meta", ".mat", ".controller", ".anim",
  ".overrideController", ".physicMaterial", ".physicsMaterial2D", ".mixer",
  ".playable", ".signal", ".mask", ".spriteatlas", ".cubemap", ".flare",
  ".renderTexture", ".shadervariants", ".guiskin", ".fontsettings",
  ".terrainlayer", ".lighting", ".preset", ".inputactions", ".asmdef",
];
const protectedDir = ["/ProjectSettings/", "/Library/", "/Temp/", "/Logs/", "/UserSettings/", "/Packages/"];

// 프로젝트 폴더 밖(스크래치·메모리 등)은 검사하지 않는다. 폴더 검사는 프로젝트 루트 기준.
const root = (process.env.CLAUDE_PROJECT_DIR || process.cwd()).split("\\").join("/").replace(/\/$/, "");
const lowerRoot = root.toLowerCase();
const lower = p.toLowerCase();
if (!lower.startsWith(lowerRoot + "/")) process.exit(0);
const rel = "/" + p.slice(root.length + 1);
const byExt = protectedExt.some((e) => lower.endsWith(e.toLowerCase()));
const byDir = protectedDir.some((d) => rel.includes(d));

if (byExt || byDir) {
  process.stderr.write(
    `[hook:protect-assets] 차단: ${raw}\n` +
    `씬·프리팹·에셋·ProjectSettings·Packages 는 사용자가 에디터에서 수정한다.\n` +
    `대신 사용자에게 에디터 체크리스트(어느 오브젝트 → 어느 컴포넌트 → 어떤 값)를 준다.\n` +
    `꼭 직접 바꿔야 하면 사용자가 "MCP로 수정해줘"라고 말한 대화에서 MCP 도구로 한다.\n`
  );
  process.exit(2);
}
process.exit(0);
