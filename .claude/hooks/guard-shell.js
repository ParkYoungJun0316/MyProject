// PreToolUse(Bash|PowerShell): 되돌리기 어려운 git 명령과 셸을 통한 보호 파일 쓰기 차단.
// git commit 은 막지 않는다 — 사용자가 "커밋해"라고 했을 때만 한다 (CLAUDE.md).
// heredoc 본문과 작은따옴표 문자열은 "실행되는 명령"이 아니므로 검사에서 뺀다.
const fs = require("fs");
let input = {};
try { input = JSON.parse(fs.readFileSync(0, "utf8")); } catch (e) { process.exit(0); }
const original = (input.tool_input || {}).command || "";
if (!original) process.exit(0);

// 1) heredoc 본문 제거: <<EOF / <<'EOF' / <<-"EOF" ... EOF
let cmd = original.replace(/<<-?\s*['"]?(\w+)['"]?[^\n]*\n[\s\S]*?\n\s*\1\s*(?=\n|$)/g, " ");
// 2) 작은따옴표 문자열 제거 (셸에서 리터럴)
cmd = cmd.replace(/'[^']*'/g, "''");
// 3) 줄바꿈·연속 공백 정리
cmd = cmd.replace(/\s+/g, " ");

// 명령 시작 위치에서만 매치: 줄 처음, ;, &&, ||, |, (, $(
const AT = String.raw`(?:^|[;&|(]\s*)`;
const gitBlock = [
  "git\\s+push\\b",
  "git\\s+reset\\s+--hard\\b",
  "git\\s+checkout\\s+(?:--|\\.)",
  "git\\s+restore\\b",
  "git\\s+clean\\b",
  "git\\s+stash\\s+(?:drop|clear|pop)\\b",
  "git\\s+branch\\s+-D\\b",
  "git\\s+rebase\\b",
  "git\\s+commit\\b[^;&|]*--amend\\b",
  "git\\s+(?:rm|mv)\\b",
].map((s) => new RegExp(AT + s));

for (const re of gitBlock) {
  if (re.test(cmd)) {
    process.stderr.write(
      `[hook:guard-shell] 차단: ${cmd.slice(0, 200)}\n` +
      `되돌리기 어려운 git 명령은 사용자가 직접 한다. 필요하면 명령을 보여 주고 부탁한다.\n`
    );
    process.exit(2);
  }
}

// 셸로 보호 파일에 쓰기 (sed -i, >, tee, rm, mv, cp, Set-Content, Out-File ...)
const protectedPath = /\.(?:unity|prefab|asset|meta|mat|controller|anim|asmdef|inputactions)\b|ProjectSettings\/|Packages\//i;
const writeOp = new RegExp(
  AT + "(?:sed\\s+-i\\b|tee\\b|rm\\b|mv\\b|cp\\b|Set-Content|Out-File|Remove-Item|Move-Item|Copy-Item|New-Item)|>\\s*\"?(?!/dev/null|&|\\$null)[^|&\\s>]"
);
// 쓰기 연산자 검사는 큰따옴표 문자열을 비운 뒤 한다 ("Gamepad>/leftStick" 같은 검색어 오인 방지).
// 경로 검사는 원문으로 한다 (> "Assets/x.prefab" 처럼 따옴표 친 경로도 잡히게).
const cmdNoDq = cmd.replace(/"[^"]*"/g, '""');
if (protectedPath.test(cmd) && writeOp.test(cmdNoDq)) {
  process.stderr.write(
    `[hook:guard-shell] 차단: 셸로 씬·프리팹·에셋·설정 파일을 바꾸려 함.\n${cmd.slice(0, 200)}\n` +
    `읽기(cat/grep)는 되지만 쓰기는 사용자가 에디터에서 한다.\n`
  );
  process.exit(2);
}
process.exit(0);
