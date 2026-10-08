// PreToolUse(Bash|PowerShell): git push / git clean 차단 (사용자가 직접 한다).
// 나머지 되돌리기(restore·reset --hard·checkout -- 등)와 에셋 쓰기는 hook 이 아니라 규칙으로:
// 바뀔/날아갈 것을 보여 주고 허락받은 뒤 실행 (CLAUDE.md "Approval-gated edits").
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
// push = 외부로 나감, clean = 추적 안 된 파일 영구 삭제(휴지통 없음).
const gitBlock = [
  "git\\s+push\\b",
  "git\\s+clean\\b",
].map((s) => new RegExp(AT + s));

for (const re of gitBlock) {
  if (re.test(cmd)) {
    process.stderr.write(
      `[hook:guard-shell] 차단: ${cmd.slice(0, 200)}\n` +
      `git push / git clean 은 사용자가 직접 한다. 필요하면 명령을 보여 주고 부탁한다.\n`
    );
    process.exit(2);
  }
}
process.exit(0);
