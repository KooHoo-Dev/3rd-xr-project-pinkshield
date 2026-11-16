---
name: devlog
description: 카드깡 VR 프로젝트의 DevLog(게임 개발·AI 협업 작업노트)를 쓴다 — 일일 노트, 버그 카드(문제→원인→해결→결과·스크린샷), 결정 기록, AI 협업 사례, 포트폴리오 기술 문서 초안 내보내기. 사용자가 "/devlog", "작업노트", "버그 기록", "오늘 기록", "결정 기록", "AI 협업 기록", "포트폴리오용으로 모아줘"를 말하거나, 버그를 해결했거나 설계를 결정한 직후에 사용.
---

# /devlog

DevLog 폴더: 프로젝트 루트 `DevLog/` (Assets 밖). 규칙은 `DevLog/README.md`, 양식은 `DevLog/templates/`.
대화 원문은 `~/claude-worklog`가 자동 기록하므로 여기에는 **골라 정리한 사실**만 씁니다.

## 공통 규칙
- 번호: 폴더별 3자리 연번. 다음 번호 = 해당 폴더의 가장 큰 번호 + 1. 파일 이름 `PREFIX-NNN-짧은-제목.md` (공백은 `-`).
- 새 기록을 만들면 `DevLog/INDEX.md`의 해당 마커(`<!-- bug-rows -->`, `decision-rows`, `ai-rows`, `daily-rows`) **바로 아래 줄**에 행을 넣습니다(최신이 위). ⭐이면 `star-rows`에도 넣습니다.
- 상태가 바뀌면 카드의 표와 INDEX 행을 **둘 다** 고칩니다.
- 사용자 폴더 경로(`C:\Users\<이름>\…`)는 `~`로 바꿔 씁니다.
- 확인하지 않은 원인은 "추정"으로 표시합니다. 사용자가 하지 않은 일을 사용자가 한 것처럼 쓰지 않습니다.
- **AI 협업 칸은 정직하게**: Claude가 제안한 것과 사용자가 판단·수정한 것을 구분합니다. 사용자의 판단 근거를 모르면 지어내지 말고 물어봅니다.
- 쓴 뒤에는 만든·고친 파일 링크와 비어 있는 칸(사용자가 채울 것)만 짧게 알려 줍니다. 커밋은 요청받을 때만.

## `/devlog day [YYYY-MM-DD]`
1. 날짜 기본값은 오늘. `DevLog/daily/<날짜>.md`가 없으면 `templates/daily.md`로 만들고 INDEX `daily-rows`에 행 추가. Day 번호 = 기존 일일 노트 수(첫날 0)… 단 사용자가 개발 일차를 말하면 그것을 따릅니다.
2. 근거 모으기:
   - `git log --since=<날짜 00:00> --until=<다음날 00:00> --oneline`
   - `~/claude-worklog/sessions/<날짜>__3rd-xr-project-pinkshield__*.md` (요청 흐름·수정한 파일) — "기록 근거"에 상대 링크가 아닌 파일 이름으로 적습니다(다른 저장소이므로).
   - 그날 생성·수정된 BUG/DEC/AI 기록
3. "한 일"·"AI 협업" 표를 채웁니다. 이미 있는 내용은 지우지 않고 보탭니다.
4. "내일 할 일"은 사용자가 말한 것만. 없으면 비워 둡니다.

## `/devlog bug [BUG-NNN] [설명]`
- **번호 없음** → 새 카드. `templates/bug.md` 양식으로 만들고, 아는 사실(증상·재현·로그)을 채웁니다. 스크린샷이 클립보드에 있다고 하면 `powershell -File DevLog/tools/devlog-paste.ps1 -Bug BUG-NNN -Name before`.
- **번호 있음** → 기존 카드 보완. 이 대화에서 찾은 원인(`파일:줄`), 해결(무엇을 왜), 결과, 해결 커밋, AI 협업 칸을 채웁니다. 해결되면 상태 `해결`, 해결 날짜 기입, INDEX 행 갱신.
- `screenshots/_inbox/`에 최근 캡처가 있고 사용자가 after라고 하면 `screenshots/BUG-NNN/after.png`로 옮기고 카드 6절에 이미지 링크.
- 트러블슈팅 서사가 분명하면(원인이 의외였거나, 여러 시도 끝에 해결) ⭐ 후보로 제안만 합니다. 표시는 사용자가 정합니다.

## `/devlog decision [주제]`
`templates/decision.md` 양식. 선택지는 실제로 검토한 것만 씁니다. 설계서(`Assets/98. Doc/카드깡_시스템설계서_v*.md`)의 `[결정 필요]` 항목을 확정한 경우 그 절을 "관련"에 적습니다.

## `/devlog ai [주제]`
`templates/ai-collab.md` 양식. 핵심은 "AI가 틀렸거나 맥락을 몰랐던 부분을 사용자가 어떻게 알아챘나". 그런 지점이 없는 단순 위임은 일일 노트 표 한 줄로 충분하다고 안내합니다.

## `/devlog export`
⭐ 기록을 포트폴리오 기술 문서 5단 구조(`~/Documents/GitHub/portfolio-test/format/02-tech-document.md`)에 맞춰 `DevLog/export/기술문서-초안.md`로 모읍니다.
- BUG → `5-2 트러블슈팅` (문제 → 원인 → 해결 → 결과, 한 건당 한 소절)
- DEC → `4 고민과 선택` (선택지 비교 표 유지)
- AI → `5-4 AI 협업`
- 이미지 경로는 `images/<원래이름>` 형태로 바꾸고, 문서 끝에 "복사할 이미지" 목록(원본 경로 → `src/<NN>/images/` 이름)을 붙입니다.
- portfolio-test 저장소에 직접 복사하지 않습니다. 사용자가 요청하면 그때 옮깁니다.
