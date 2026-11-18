# BUG-013 · 씬 연결 테스트가 일부러 비운 DropHint.bobber를 실패로 판정

| 항목 | 값 |
|---|---|
| 상태 | 해결 (Unity Test Runner 재실행 확인 전) |
| 심각도 | 낮음 |
| 발견 | 2026-11-18 12:08 |
| 해결 | 2026-11-18 |
| 씬 / 환경 | Shop · 에디터 Test Runner (EditMode) |
| 발견 커밋 | `6b70bc0` (main, 3차 기기 테스트 피드백) |
| 해결 커밋 | `e653e3c` |
| 포트폴리오 | |

## 1. 증상 (문제)
3차 기기 피드백(청소 "놓을 곳 표시")을 넣고 Build Shop Scene을 다시 실행한 뒤 EditMode 테스트를 돌리자 168개 중 1개가 실패했다.

- **기대**: 씬의 모든 CardShop 참조가 연결되어 있으므로 `ShopScene_AllCardShopReferencesAreConnected` 통과
- **실제**: 같은 미연결 항목 `DropHint/DropHint.bobber` 4건으로 실패

## 2. 재현 단계
1. `6b70bc0` 기준으로 Build Shop Scene 실행
2. Test Runner → EditMode → Run All
3. `SceneSetupTests.ShopScene_AllCardShopReferencesAreConnected` 실패

재현율: 항상

## 3. 콘솔 로그
```text
미연결 참조 (Build Shop Scene 다시 실행):
DropHint/DropHint.bobber
DropHint/DropHint.bobber
DropHint/DropHint.bobber
DropHint/DropHint.bobber
  Expected: <empty>
  But was:  < "DropHint/DropHint.bobber", ... (4건) >
at CardShop.Tests.SceneSetupTests.ShopScene_AllCardShopReferencesAreConnected () in Assets/01. Scripts/Tests/SceneSetupTests.cs:38
```
(결과 파일: `~/Downloads/TestResults_20261118_120841.xml`, 168개 중 통과 167 · 실패 1)

## 4. 원인
게임 동작이나 씬 빌드의 문제가 아니라, **테스트 규칙과 코드 표기가 맞지 않았다.**

- `DropHint.bobber`(위아래로 흔들리는 표식)는 쓰레기통 표시에만 있고, 팩 꽂이 **빈 칸 윤곽 4개에는 일부러 비워 둔다** (`CleanupBuilder.SlotHint`가 `Configure(..., null)`로 연결). 4건 = 빈 칸 윤곽 4개.
- 씬 연결 테스트는 비어 있는 오브젝트 참조를 모두 실패로 보되, **툴팁에 "비워" 또는 "비우면"이 들어간 필드만** 비워도 되는 필드로 인정한다 (`SceneSetupTests.IsOptional`, `SceneSetupTests.cs:43-56`).
- 새로 만든 `DropHint.bobber`와 `PackRack.slotHints` · `rackHint`의 툴팁에는 "(없어도 됨)"이라고 적혀 있어, 테스트가 선택 필드로 알아보지 못했다.

## 5. 해결
선택 필드의 툴팁 문구를 프로젝트 규칙에 맞게 바꿨다. 코드 동작 · 씬은 그대로이므로 Build Shop Scene 재실행은 필요 없다.

- `DropHint.bobber`: "위아래로 흔들리는 표식. 비워 두면 흔들지 않음 (꽂이 빈 칸 윤곽)"
- `PackRack.slotHints` · `rackHint`: "… 비워 두면 윤곽 없음" · "… 비워 두면 안내 없음" (지금은 연결돼 있지만 같은 표기로 맞춤)

다른 방법: 빈 칸 윤곽에도 흔들림 표식을 만들어 연결할 수도 있었지만, 윤곽은 꽂히는 자리를 정확히 보여 줘야 해서 움직이지 않는 편이 맞다고 보고 툴팁만 고쳤다(Claude 판단).

## 6. 결과
- 수정 후 Runtime · Editor · Tests 어셈블리 컴파일 오류 없음 (Unity 밖에서 Roslyn으로 확인)
- **Unity Test Runner 재실행으로 168개 통과 확인: 아직** ← 확인 후 채우기

## 7. AI 협업
- 내가 한 일: 3차 피드백 구현 후 Unity에서 Build Shop Scene · EditMode 테스트를 직접 돌려 실패를 발견하고, 결과 XML을 Claude에게 넘겨 분석을 요청함
- AI가 제안한 것: 실패 원인을 테스트의 선택 필드 판정 규칙(툴팁 "비워")과 새 툴팁 문구 "(없어도 됨)"의 불일치로 짚고, 툴팁 3곳 수정
- 받아들임 / 고침 / 거절 — 이유: 받아들임. 이 버그는 Claude가 `DropHint`를 만들면서 프로젝트의 테스트 규칙을 확인하지 않고 툴팁을 써서 생긴 것
- 관련 대화: worklog 2026-11-18 세션 (device-build-test-feedback-2)

## 8. 배운 점
- 이 프로젝트에서 **비워 둬도 되는 `[SerializeField]` 참조는 툴팁에 "비워 두면 …"이라고 쓴다.** 씬 연결 테스트가 이 문구로 선택 필드를 구분한다.
- 새 컴포넌트를 씬 빌더에 붙일 때는 Unity 밖 컴파일만으로는 부족하다. Build Shop Scene → EditMode 테스트까지 돌려야 이런 씬 단위 규칙 위반이 잡힌다.
