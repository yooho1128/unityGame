# 그림자 극장 잔영 (Shadow Theater)

1인 개발 2D 모바일 RPG — 스토리 중심 타일맵 필드 탐색 + 1대1 수집형 턴제 전투 (Unity 2D URP).

| 폴더 | 내용 |
|---|---|
| `Assets/Scripts/Data` | ShadowData / SkillData / ItemData (ScriptableObject), ShadowInstance, ShadowDatabase |
| `Assets/Scripts/Battle` | BattleManager(턴 상태 머신), BattleUnit, DamageCalculator, BattleAI, IBattlePresenter |
| `Assets/Scripts/Save` | SaveData(JSON), SaveManager |
| `Assets/Scripts/Field` | PlayerController(타일 이동), EncounterSymbol(심볼 인카운터), GameFlowController(필드↔전투) |
| `Prototype/shadow-theater.html` | 같은 전투 규칙의 브라우저 프로토타입 — 더블클릭으로 실행, 밸런스 데이터 편집 탭 포함 |
| `Docs/` | 기획 명세서, 씬 세팅 가이드(SETUP.md) |

## 집 PC에서 Unity로 열기
1. 이 저장소를 clone
2. Unity Hub → **Add project from disk** → clone한 폴더 선택 (Unity 2022.3 LTS 이상)
   - `ProjectSettings/`, `Packages/`는 처음 열 때 자동 생성됩니다
3. Package Manager에서 **Universal RP**, **2D Tilemap Extras** 설치 → URP 2D Renderer 설정
4. Player Settings → Active Input Handling = **Both**
5. 처음 연 뒤 생성된 `ProjectSettings/`, `Packages/`, `*.meta` 파일까지 커밋

자세한 씬 구성은 [`Docs/SETUP.md`](Docs/SETUP.md) 참고.

## Unity 없이 작업할 때
- `Prototype/shadow-theater.html`을 브라우저로 열어 전투/포획/밸런스 테스트
- "밸런스 데이터" 탭의 필드명 = SO 필드명 → 확정한 수치를 SO에 그대로 입력
