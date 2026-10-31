# 100 Bags Back — Claude Code 작업 규칙

## 기본 규칙 (먼저 따를 문서)
@Docs/0x_Agent_Rules.md
@.agy/rules/00_Agent_Integrated_Guidelines.md

## 에이전트 역할 분담
Agent Rules의 6단계 파이프라인을 Claude와 Gemini가 나눠 맡는다.

| 단계 | 담당 | 내용 |
| --- | --- | --- |
| 1. 설계 제안 | Claude | Echo & Verify로 유저 확인을 받은 뒤 설계안 작성 |
| 2. 리뷰 | Gemini | `gemini-reviewer` 서브에이전트로 설계 검토 |
| 3. 구현 | Claude | 리뷰 반영 후 구현 |
| 4. 유니티 에디터 적용 | Claude | unityMCP로 씬/프리팹/SO 적용 |
| 5. 테스트 | Claude + Gemini | Claude가 unityMCP로 콘솔 에러를 확인하고, `gemini-reviewer`로 구현 검증 |
| 6. 최종 승인 | 유저 | |

## Gemini 검토 규칙
- Gemini는 Antigravity CLI(`agy`)로 호출한다. 직접 실행하지 말고 항상 `gemini-reviewer` 서브에이전트를 통해 호출한다.
- 모델은 검토 종류로 나눈다. 호출할 때 검토 종류를 반드시 밝힌다.
  - 기획 검토(GDD·기획 문서, 코드가 없는 설계안): Gemini 3.8 Flash (`gemini-3.8-flash-medium`)
  - 코드 관련 검토(코드 설계, 구현 검증): Gemini 4 Argon (`gemini-4-argon-medium`)
- 설계안이 정리된 직후, 그리고 구현을 마친 직후에 검토를 받는다. 사소한 픽스는 검토를 생략한다.
- 판정이 `CHANGES_REQUESTED`면 CRITICAL/MAJOR 항목을 반영하고 다시 검토받는다. 최대 3회까지 하고, 그래도 남으면 멈추고 유저에게 보고한다.
- Gemini의 지적이 기획 결정을 바꾸는 내용이면 반영하기 전에 유저에게 묻는다 (뇌피셜 금지 원칙).
- `[IDEA]` 항목은 구현하지 않는다. `Docs/Architecture/02_Feature_Backlog.md`에 추가할지 유저에게 제안만 한다.
- 유저에게 보고할 때 Gemini 판정, 반영한 항목, 반영하지 않은 항목과 이유를 함께 정리한다.
- 보고에는 검토마다 `gemini-reviewer`가 돌려준 모델과 토큰 사용량(입력/출력/사고/합계)을 표로 붙이고, 여러 번 검토했다면 합계도 적는다.

### 사용량 초과 대응
Gemini는 구독에서 남는 사용량을 활용하려고 쓰는 것이지, 다른 모델의 교차 검증이 필수라는 뜻은 아니다.
- 사용량 한도 초과(한도·429·quota 에러)로 검토를 못 받으면 검토 없이 "통과"로 처리하지 않는다. 해당 작업을 "검토 보류"로 표시하고 `.claude/review/pending.md`에 커밋 해시·검토 종류·대상 파일을 적은 뒤 작업은 계속한다. 커밋 메시지와 유저 보고에도 "검토 보류"를 적는다.
- 한도가 풀리면 밀린 항목을 묶어 한 번에 검토받고 `pending.md`에서 지운다.
- 변경이 크거나 다음 작업이 그 검토 결과에 의존하는데 한도가 오래 막혀 있으면, Claude 자체 검토(`code-review` 스킬)로 대신한다. 보고에는 "Claude 자체 검토"로 표시하고, 이후 Gemini 재검토는 선택 사항으로 둔다.
- 다른 Gemini 모델로 몰래 바꾸지 않는다. 모델을 바꾸려면 유저에게 묻는다.
