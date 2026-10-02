# 그림자 극장 잔영 (Shadow Theater)

1인 개발 2D 모바일 RPG — 스토리 중심 타일맵 필드 탐색 + 1대1 수집형 턴제 전투 (Unity 2D URP).

| 폴더 | 내용 |
|---|---|
| `Assets/Scripts/Data` | ShadowData / SkillData / ItemData (ScriptableObject), ShadowInstance, ShadowDatabase |
| `Assets/Scripts/Battle` | BattleManager(턴 상태 머신), BattleUnit, DamageCalculator, BattleAI, IBattlePresenter |
| `Assets/Scripts/Save` | SaveData(JSON), SaveManager |
| `Assets/Scripts/Field` | PlayerController(타일 이동), EncounterSymbol(심볼 인카운터), GameFlowController(필드↔전투) |
| `Assets/Scripts/Story` | JSON 대사/퀘스트 저장소, 목표 추적, 누적 선택 기반 다중 엔딩 |
| `Assets/Scripts/UI` | 모바일 대화창, 퀘스트 HUD, 스타터 선택, 각본집 도감 |
| `Assets/Editor` | 주요 모바일 UI 프리팹 자동 생성 메뉴 |
| `Prototype/shadow-theater.html` | 같은 전투 규칙의 브라우저 프로토타입 — 더블클릭으로 실행, 밸런스 데이터 편집 탭 포함 |
| `Docs/` | 기획 명세서, 씬 세팅 가이드(SETUP.md) |

## 집 PC에서 Unity로 열기
1. 이 저장소를 clone
2. Unity Hub → **Add project from disk** → clone한 폴더 선택 (Unity 2022.3 LTS 이상)
   - `ProjectSettings/`, `Packages/`는 처음 열 때 자동 생성됩니다
3. Package Manager에서 **Universal RP**, **2D Tilemap Extras** 설치 → URP 2D Renderer 설정
4. Player Settings → Active Input Handling = **Both**
5. 처음 연 뒤 생성된 `ProjectSettings/`, `Packages/`, `*.meta` 파일까지 커밋

## 대화 시스템 빠른 설치
1. Unity 메뉴 **Tools → Shadow Theater → Generate Dialogue UI Prefab** 실행
2. 생성된 `Assets/Prefabs/UI/DialogueCanvas.prefab`을 필드 씬 최상위에 배치
3. NPC 오브젝트에 Collider2D와 `StoryNpc`를 추가하고 대화 ID 지정
4. 대사는 `Assets/Resources/Data/DialogueCatalog.json`에서 수정

대화 데이터는 조건부 줄(`requiredFlag`, `blockedFlag`), 진행 플래그(`setFlag`), 최대 3개의
선택지와 `nextDialogueId` 분기를 지원합니다. 기존 단순 `lines` 대사는 수정 없이 그대로 동작합니다.

## 퀘스트 시스템 빠른 설치
1. Unity 메뉴 **Tools → Shadow Theater → Generate Quest HUD Prefab** 실행
2. 생성된 `Assets/Prefabs/UI/QuestHUDCanvas.prefab`을 필드 씬 최상위에 배치
3. 지역 도착 목표 지점에는 Trigger Collider2D와 `QuestAreaTrigger`를 추가
4. 퀘스트 정의/연결/보상은 `Assets/Resources/Data/QuestCatalog.json`에서 편집

## 보스 컷신 빠른 설치
1. 보스 오브젝트에 Collider2D와 `BossEncounterTrigger` 추가
2. `encounterId`, 적 ShadowData 목록, 레벨 범위를 지정
3. 전투 전/승리 대화 ID를 `DialogueCatalog.json`의 ID와 연결
4. 보스는 Unit 레이어의 비 Trigger Collider로 두어 플레이어가 정면에서 상호작용하게 설정

## 타이틀·스타터·맵 이동 빠른 설치
1. Unity 메뉴 **Tools → Shadow Theater → Generate Title and Starter UI** 실행
2. 타이틀 씬에 `CoreSystems.prefab`, `TitleCanvas.prefab`, EventSystem 배치
3. `TitleCanvas`의 StarterSelectionController에 붉은 불꽃/푸른 서리/자줏빛 그림자 SO를 순서대로 등록
4. 모든 지역 씬을 Build Settings에 추가하고 씬 이름을 `mapId`로 사용
5. 맵 출구에 Trigger Collider2D와 `MapPortal`을 붙여 목표 씬·좌표·방향 지정

## 다중 엔딩
- 대화 선택의 `flagChanges`가 기억 보존·연민·통제 성향을 누적
- 최종 무대의 `EndingTrigger`가 마지막 선택 후 `EndingCatalog.json` 조건을 판정
- 진엔딩, 기억 보존 엔딩, 자비로운 망각 엔딩, 검열단 배드 엔딩 제공
- 해금 엔딩과 마지막 엔딩은 세이브에 기록

## 각본집 도감 빠른 설치
1. Unity 메뉴 **Tools → Shadow Theater → Generate Script Book UI** 실행
2. 생성된 `Assets/Prefabs/UI/ScriptBookCanvas.prefab`을 필드 씬 최상위에 배치
3. `Resources/ShadowDatabase.asset`의 Shadows 목록에 전체 ShadowData를 도감 순서대로 등록
4. 내장된 `각본집` 버튼 또는 에디터의 Tab 키로 열기

도감은 미조우·조우·기록 완료 상태를 구분하며 기록 완료 시 능력치와 `loreUnlocked`를 공개합니다.

자세한 씬 구성은 [`Docs/SETUP.md`](Docs/SETUP.md) 참고.

## Unity 없이 작업할 때
- `Prototype/shadow-theater.html`을 브라우저로 열어 전투/포획/밸런스 테스트
- "밸런스 데이터" 탭의 필드명 = SO 필드명 → 확정한 수치를 SO에 그대로 입력
