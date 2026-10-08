// 씬 저장 잡음 제거 — UI.prefab 인스턴스의 RectTransform 오버라이드 잡음을 HEAD 기준으로 되돌린다.
//
// 검사는 Claude 가 언제든, --apply 는 목록을 보여 주고 사용자 허락을 받은 뒤 Claude 가 실행한다
// (.unity 를 손으로 고치지 않고 이 스크립트로만 지운다 — .claude/skills/scene-noise):
//   node Tools/SceneNoise/strip-scene-noise.js            → 바뀐 씬 전부 검사만 (파일 안 바꿈)
//   node Tools/SceneNoise/strip-scene-noise.js --apply    → 검사 + 잡음 제거
//   씬 경로를 주면 그 씬만:  ... --apply Assets/Scenes/M.Boss.unity
// 옵션: --any-prefab  UI.prefab 말고 모든 프리팹 인스턴스의 같은 속성도 대상
//       --against <파일>  HEAD 대신 이 파일을 기준으로 비교 (테스트용)
//
// 잡음 = HEAD 와 비교해 아래 속성의 오버라이드가 값만 바뀌었거나 새로 생긴 것:
//   m_AnchorMin/Max·m_AnchoredPosition·m_SizeDelta·m_Pivot (.x/.y), m_TextStyleHashCode,
//   "Scrollbar Vertical"(fileID 8644908003647105056) m_IsActive
// 값이 바뀐 것 → HEAD 값으로 되돌림 / 새로 생긴 블록 → 블록째 뺌 / 없어진 것 → 보고만.
// 의도해서 바꾼 UI 위치도 같은 모양이라 되돌려진다 — --apply 전에 목록을 보고 판단한다.
const fs = require("fs");
const { spawnSync } = require("child_process");

const UI_PREFAB_GUID = "a007cd176ff5d384e95a71323f7750ae"; // Assets/Prefab/UI.prefab
const SCROLLBAR_VERTICAL_FILEID = "8644908003647105056";
const RECT_PATH = /^m_(AnchorMin|AnchorMax|AnchoredPosition|SizeDelta|Pivot)\.[xy]$/;

const args = process.argv.slice(2);
const apply = args.includes("--apply");
const anyPrefab = args.includes("--any-prefab");
let against = null;
const files = [];
for (let i = 0; i < args.length; i++) {
  if (args[i] === "--against") { against = args[++i]; continue; }
  if (args[i].startsWith("--")) continue;
  files.push(args[i].split("\\").join("/"));
}

function git(argv) {
  const r = spawnSync("git", argv, { encoding: "utf8", maxBuffer: 1 << 30 });
  if (r.error || r.status !== 0) return null;
  return r.stdout;
}

if (files.length === 0) {
  const out = git(["diff", "--name-only", "--relative", "HEAD", "--", "*.unity"]);
  if (out === null) { console.error("git diff 실패 — 프로젝트 폴더에서 실행하세요."); process.exit(1); }
  out.split("\n").map((s) => s.trim()).filter(Boolean).forEach((f) => files.push(f));
}
if (files.length === 0) { console.log("바뀐 씬 없음."); process.exit(0); }

function isNoise(e) {
  if (!anyPrefab && !e.target.includes(`guid: ${UI_PREFAB_GUID}`)) return false;
  if (RECT_PATH.test(e.path) || e.path === "m_TextStyleHashCode") return true;
  return e.path === "m_IsActive" && e.target.includes(`fileID: ${SCROLLBAR_VERTICAL_FILEID},`);
}

// 오버라이드 블록: "- target: {...}" / "propertyPath: X" / "value: ..."(여러 줄일 수 있음) / "objectReference: {...}"
function parse(lines) {
  const map = new Map();
  const dup = new Set();
  const list = [];
  let doc = "";
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    if (line.startsWith("--- ")) { doc = line; continue; }
    const t = /^\s*- target: (.*)$/.exec(line);
    if (!t) continue;
    const p = /^\s*propertyPath: (.*)$/.exec(lines[i + 1] || "");
    if (!p || !/^\s*value:/.test(lines[i + 2] || "")) continue;
    let j = i + 2;
    while (j < lines.length && !/^\s*objectReference:/.test(lines[j])) j++;
    if (j >= lines.length) break;
    const e = { doc, target: t[1], path: p[1], start: i, end: j + 1, valStart: i + 2, valEnd: j };
    e.key = `${doc}|${e.target}|${e.path}`;
    e.value = lines.slice(e.valStart, e.valEnd).join("\n");
    if (map.has(e.key)) dup.add(e.key); else map.set(e.key, e);
    list.push(e);
    i = j;
  }
  return { map, dup, list };
}

function short(e) {
  const id = (/fileID: (-?\d+)/.exec(e.target) || [])[1];
  return `${e.path} (fileID ${id})`;
}

let totalChanged = 0;
for (const file of files) {
  if (!fs.existsSync(file)) { console.log(`\n[${file}] 파일 없음 — 건너뜀`); continue; }
  const curText = fs.readFileSync(file, "utf8");
  let baseText = against ? fs.readFileSync(against, "utf8") : git(["show", `HEAD:./${file}`]);
  if (baseText === null) { console.log(`\n[${file}] HEAD 에 없는 새 씬 — 건너뜀`); continue; }
  const eol = curText.includes("\r\n") ? "\r\n" : "\n";
  const cur = curText.split(/\r?\n/);
  const base = baseText.split(/\r?\n/);
  const C = parse(cur);
  const B = parse(base);

  const edits = []; // { start, end, replacement[] }
  const reverted = [], removed = [], missing = [];
  for (const e of C.list) {
    if (!isNoise(e) || C.dup.has(e.key) || B.dup.has(e.key)) continue;
    const b = B.map.get(e.key);
    if (!b) {
      removed.push(`${short(e)} = ${e.value.trim().replace(/^value:\s*/, "")}`);
      edits.push({ start: e.start, end: e.end, replacement: [] });
    } else if (b.value !== e.value) {
      reverted.push(`${short(e)}: ${e.value.trim().replace(/^value:\s*/, "")} → ${b.value.trim().replace(/^value:\s*/, "")}`);
      edits.push({ start: e.valStart, end: e.valEnd, replacement: base.slice(b.valStart, b.valEnd) });
    }
  }
  for (const b of B.list) {
    if (isNoise(b) && !B.dup.has(b.key) && !C.map.has(b.key)) missing.push(short(b));
  }

  console.log(`\n[${file}] 값 되돌림 ${reverted.length} / 새 블록 뺌 ${removed.length} / 없어진 오버라이드 ${missing.length}(보고만)`);
  const show = (title, arr) => {
    if (!arr.length) return;
    console.log(`  ${title}:`);
    arr.slice(0, 15).forEach((s) => console.log(`    ${s}`));
    if (arr.length > 15) console.log(`    ... 외 ${arr.length - 15}개`);
  };
  show("값 되돌림", reverted);
  show("새 블록 뺌", removed);
  show("없어진 오버라이드(자동 복구 안 함)", missing);

  if (!edits.length) continue;
  totalChanged++;
  if (!apply) continue;
  edits.sort((a, b) => b.start - a.start);
  for (const ed of edits) cur.splice(ed.start, ed.end - ed.start, ...ed.replacement);
  fs.writeFileSync(file, cur.join(eol), "utf8");
  console.log(`  → 저장함. 에디터에 이 씬이 열려 있으면 디스크에서 다시 여세요(저장하지 말고).`);
}

if (!apply && totalChanged) console.log(`\n검사만 했습니다. 지우려면 --apply 를 붙여 다시 실행하세요.`);
