# 그림자 극장 잔영 (Shadow Theater)

1인 개발 2D 모바일 RPG — 스토리 중심 타일맵 필드 탐색 + 1대1 수집형 턴제 전투 (Unity 2D URP).

| 폴더 | 내용 |
|---|---|
| `Assets/Scripts/Data` | ShadowData / SkillData / ItemData (ScriptableObject), ShadowInstance, ShadowDatabase |
| `Assets/Scripts/Battle` | BattleManager(턴 상태 머신), BattleUnit, DamageCalculator, BattleAI, IBattlePresenter |
| `Assets/Scripts/Save` | SaveData(JSON), SaveManager, 파티·각본 서고 편성 API |
| `Assets/Scripts/Field` | PlayerController(타일 이동), EncounterSymbol(심볼 인카운터), GameFlowController(필드↔전투) |
| `Assets/Scripts/Story` | JSON 대사/퀘스트 저장소, 목표 추적, 누적 선택 기반 다중 엔딩 |
| `Assets/Scripts/UI` | 모바일 대화창, 퀘스트 HUD, 스타터 선택, 각본집 도감, 파티·서고 편성 |
| `Assets/Editor` | 주요 모바일 UI 프리팹 자동 생성 메뉴 |
| `Prototype/shadow-theater.html` | 같은 전투 규칙의 브라우저 프로토타입 — 더블클릭으로 실행, 밸런스 데이터 편집 탭 포함 |
| `Docs/` | 기획 명세서, 씬 세팅 가이드(SETUP.md) |

## 집 PC에서 Unity로 열기
1. 이 저장소를 clone
2. Unity Hub → **Add project from disk** → clone한 폴더 선택
3. 기준 버전인 **Unity 2022.3.21f1** 또는 같은 2022.3 LTS 계열로 열기
4. 첫 실행 시 `Packages/manifest.json`을 기준으로 URP 2D, Tilemap Extras, Input System 등을 자동 설치
5. Player Settings → Active Input Handling = **Both** 확인
6. Unity가 생성한 나머지 `ProjectSettings/`, `Packages/packages-lock.json`, `*.meta` 파일까지 커밋

## 초반 콘텐츠 데이터 빠른 설치
1. Unity 메뉴 **Tools → Shadow Theater → Generate Core Content Data** 실행
2. `Assets/Data/Generated`에 생성된 그림자 7종, 스킬 22개, 도구 4개 확인
3. `Resources/ShadowDatabase.asset`에 모든 데이터가 자동 등록되었는지 확인

스타터 3종과 프롤로그 그림자는 기억 복원·구원·원한 성장 데이터를 포함합니다. 실제 전용 이미지가
아직 없는 개체에는 기능 테스트용 실루엣 PNG를 자동 생성하며, 이후 완성 아트로 교체해도 ID와 세이브는
그대로 유지됩니다. 원본 수치와 Lore는 `Assets/Resources/Data/CoreContentCatalog.json`에서 수정합니다.

## 플레이 가능한 프롤로그 자동 생성
1. Unity 메뉴 **Tools → Shadow Theater → Generate Playable Prologue** 실행
2. 현재 씬 저장 여부를 확인하면 필요한 데이터·UI 프리팹과 프롤로그 씬을 일괄 생성
3. 생성 후 자동으로 열린 `Title` 씬에서 Play

생성되는 동선은 `Title → 잔향 극장 → 잔향 마을 → 달빛 초원 → 달빛 보스 무대`입니다. 각 필드는
23×19 타일의 개방형 구조이며 상하좌우 탐색, 장애물 우회, 모바일 방향키/A 버튼, NPC 대화, 퀘스트,
심볼 인카운터, 각본 기록, 지역 보스 정화까지 한 흐름으로 확인할 수 있습니다. 5개 씬은 Build Settings에
자동 등록됩니다.

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

## 그림자 기억 성장
- `잔영`: 처음 기록한 기본 형태
- `기억 복원`: 레벨과 개인 기억 퀘스트 조건을 충족한 중간 형태
- `진명 각성`: 구원 또는 원한 중 하나를 선택하는 최종 형태

각 형태는 실루엣, 포인트 컬러, 능력치 배율, 추가 스킬, Lore를 독립적으로 가집니다. 각본집 상세
화면에서 조건 확인과 복원/각성을 실행하며 결과는 세이브 버전 4에 저장됩니다.

### 전설·보스 성장 데이터 1차 묶음

달빛의 미망인, 최초의 배우, 마지막 관객의 4형태 콘셉트 시트와 전용 성장·스킬·Lore 데이터가
포함되어 있습니다. Unity 메뉴 **Tools → Shadow Theater → Generate Legendary Growth Batch**를
실행하면 시트 분할, SkillData/ShadowData 생성, ShadowDatabase 등록이 자동으로 처리됩니다.

형태별 이미지와 상세 설계는 [`Docs/LEGENDARY_GROWTH_BATCH_01.md`](Docs/LEGENDARY_GROWTH_BATCH_01.md)를 참고하세요.

## 모바일 전투 UI 빠른 설치
1. Unity 메뉴 **Tools → Shadow Theater → Generate Battle UI** 실행
2. 생성된 `Assets/Prefabs/UI/BattleCanvas.prefab`을 씬의 BattleRoot로 배치
3. `GameFlowController`의 `battleRoot`, `battleManager`에 생성된 오브젝트를 연결

공격·스킬·교체·각본 기록·도구·도주, 강제 교체, Auto, 1/2/3배속을 지원합니다. 스킬·파티·도구
목록은 현재 전투 데이터에서 자동 생성됩니다.

## 파티·각본 서고 빠른 설치
1. Unity 메뉴 **Tools → Shadow Theater → Generate Party and Storage UI** 실행
2. 생성된 `Assets/Prefabs/UI/PartyStorageCanvas.prefab`을 각 필드 씬 최상위에 배치
3. 프리팹에 포함된 **파티** 버튼으로 편성 화면을 열어 그림자를 이동하거나 순서를 변경

파티는 최대 6명이며 포획 당시 자리가 없으면 그림자는 각본 서고에 자동 보관됩니다. 서고 자체에는
수량 제한이 없습니다. 파티가 비거나 전투 가능한 그림자가 한 명도 남는 상황을 막기 위해 마지막 생존
그림자는 서고로 이동할 수 없으며, 편성 변경 결과는 즉시 세이브됩니다.

자세한 씬 구성은 [`Docs/SETUP.md`](Docs/SETUP.md) 참고.

## Unity 없이 작업할 때
- `Prototype/shadow-theater.html`을 브라우저로 열어 전투/포획/밸런스 테스트
- "밸런스 데이터" 탭의 필드명 = SO 필드명 → 확정한 수치를 SO에 그대로 입력
