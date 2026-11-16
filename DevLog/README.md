# DevLog — 게임 개발 · AI 협업 작업노트

카드깡 알바생의 하루 VR을 만들면서 **버그 · 결정 · AI 협업 · 하루 진행**을 기록합니다.
⭐ 표시한 기록은 나중에 포트폴리오 기술 문서(`portfolio-test/src/<NN-카드깡>/`)로 옮깁니다.

대화 원문은 `~/claude-worklog`가 자동으로 남기므로 여기에는 **골라서 정리한 내용**만 씁니다.

## 폴더

| 경로 | 내용 | 포트폴리오 5단 구조 |
|---|---|---|
| `daily/YYYY-MM-DD.md` | 일일 노트 | 1 개요 · 2 구조의 근거 |
| `bugs/BUG-NNN-제목.md` | 버그 카드 (문제→원인→해결→결과) | **5-2 트러블슈팅** |
| `decisions/DEC-NNN-제목.md` | 결정 기록 (선택지·이유·대가) | **4 고민과 선택** |
| `ai-collab/AI-NNN-제목.md` | AI 제안을 받아들이거나 고치거나 거절한 사례 | **5-4 AI 협업** |
| `screenshots/BUG-NNN/` | 버그별 before/after 스크린샷·GIF | `src/<NN>/images/` |
| `screenshots/_inbox/` | 아직 분류하지 않은 캡처 | |
| `templates/` | 양식 4종 | |
| `tools/devlog-paste.ps1` | 클립보드 이미지 → 파일 저장 | |
| `export/` | `/devlog export` 결과 (기술 문서 초안) | |

## 기록하는 법

### 버그 (Unity 에디터)
- **F9**: 화면 캡처 + 버그 기록 창. 씬 이름 · 플레이 시간 · 최근 콘솔 경고/에러 10줄 · git 커밋을 자동으로 채웁니다.
  플레이 중에는 Game 뷰를, 편집 중에는 Scene 뷰를 찍습니다.
- **Shift+F9**: 스크린샷만 `screenshots/_inbox/`에 저장 (after 사진 등).
- 메뉴 `DevLog > …`에서도 같은 기능을 씁니다.
- 저장하면 `bugs/BUG-NNN-제목.md`가 생기고 `INDEX.md` 버그 표에 한 줄이 추가됩니다.
- 원인·해결·결과는 Claude에게 `/devlog bug BUG-NNN` 으로 맡기거나 직접 채웁니다.

### 버그 (Unity 밖 · Quest 화면 등)
Win+Shift+S로 캡처한 뒤:
```powershell
powershell -File DevLog/tools/devlog-paste.ps1 -Bug BUG-003 -Name after
powershell -File DevLog/tools/devlog-paste.ps1          # _inbox/에 시각 이름으로
```
또는 Claude에게 "클립보드 스크린샷 BUG-003 after로 넣어줘".

### GIF · 영상
Unity Recorder(Package Manager)로 녹화해 `screenshots/BUG-NNN/`에 넣습니다.

### Claude 명령 (`/devlog`)
| 명령 | 하는 일 |
|---|---|
| `/devlog day` | 오늘 일일 노트 만들기·갱신 (커밋 · worklog 링크 자동) |
| `/devlog bug [BUG-NNN]` | 새 버그 카드, 또는 기존 카드의 원인·해결·결과 채우기 |
| `/devlog decision` | 결정 기록 |
| `/devlog ai` | AI 협업 사례 |
| `/devlog export` | ⭐ 기록을 기술 문서 5단 구조 초안으로 모음 |

## 규칙
- 번호는 폴더마다 3자리 연번(BUG-001, DEC-001, AI-001). 지우지 않고 상태만 바꿉니다.
- 스크린샷 이름: `before.png`, `after.png`, 여러 장이면 `before-2.png`, GIF는 `clip.gif`.
- 로그·경로에 들어간 사용자 폴더 경로는 `~`로 바꿔 씁니다(공개 저장소 대비).
- 사실만 씁니다. 확인하지 않은 원인은 "추정"이라고 표시합니다.
- 이 폴더는 `Assets/` 밖에 있어 Unity가 임포트하지 않습니다.
