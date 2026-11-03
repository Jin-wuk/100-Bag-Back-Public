// agy --output-format json 결과에서 답변 본문을 그대로 출력하고, 마지막 줄에 토큰 사용량을 붙인다.
// 사용법: node .claude/scripts/agy-result.js <result.json> <모델명>
const fs = require("fs");

const [file, model] = process.argv.slice(2);
const raw = fs.readFileSync(file, "utf8");

let result;
try {
  result = JSON.parse(raw);
} catch {
  // 시간 초과나 권한 오류일 때는 JSON이 아니므로 원문 그대로 보여 준다.
  console.log(raw);
  process.exit(0);
}

const u = result.usage || {};
console.log(result.response);
console.log("\n---");
console.log(
  `[${result.status}] 모델 ${model} | 입력 ${u.input_tokens} / 출력 ${u.output_tokens} / 사고 ${u.thinking_tokens} / 합계 ${u.total_tokens} 토큰 | ${Math.round(result.duration_seconds)}초`
);
