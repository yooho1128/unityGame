# 그림자 극장 잔영 — 스크립트 구성 & 씬 세팅

## 폴더
```
Assets/Scripts/
  Data/    ShadowEnums, SkillData(SO), ShadowData(SO), ItemData(SO), ShadowInstance, ShadowDatabase(SO)
  Battle/  BattleTypes, BattleUnit, DamageCalculator, BattleAI, IBattlePresenter,
           DebugBattlePresenter, BattleManager, BattleTestBootstrap
  Save/    SaveData, SaveManager
  Field/   FieldGrid, PlayerController, EncounterSymbol, ScreenFader,
           VirtualDPadButton, VirtualActionButton, VirtualScriptBookButton,
           GameFlowController, MapLoader, MapPortal
  Story/   DialogueData/Repository, StoryNpc, DialogueInteractable,
           QuestData/Repository/Manager, QuestAreaTrigger, BossEncounterTrigger,
           EndingData/Repository/Manager, EndingTrigger
  UI/      DialogueController, DialogueChoiceView, QuestHudController,
           TitleScreenController, StarterSelectionController, StarterCardView,
           ScriptBookController, ScriptBookEntryView
  Editor/  DialogueUIPrefabGenerator, QuestHudPrefabGenerator, FrontEndPrefabGenerator,
           ScriptBookPrefabGenerator
```

## 프로젝트 설정
- Unity 2022.3 LTS 이상, 템플릿 **2D (URP)**
- Player Settings → Active Input Handling = **Both** (에디터 키보드 테스트용. 모바일은 가상 패드 사용)
- Tag `Player` 추가, Layer `Obstacle`, `Unit` 추가

## 에셋 만들기 (Project 창 우클릭 → Create → ShadowTheater)
1. Skill Data: 기본 공격 3종(slash / inkshot / claw) + 스킬들
2. Shadow Data: knight / mage / beast / puppet / crow / mask / censor
3. Item Data: potion / tonic / ink / salve
4. **Resources/ShadowDatabase.asset** 생성 후 위 에셋 전부 등록 (세이브 로드 시 ID로 찾음)

수치는 웹 프로토타입의 "밸런스 데이터" 탭 JSON을 그대로 옮기면 됩니다 (필드명 동일).

## 씬 계층 (단일 씬 + 루트 토글)
```
[Systems]         SaveManager, GameFlowController   ← FieldRoot/BattleRoot 바깥!
[FieldRoot]
   Grid (+FieldGrid)
      Ground (Tilemap)
      Collision (Tilemap, 렌더러 꺼도 됨) → FieldGrid.collisionTilemap
   Player  (SpriteRenderer, Rigidbody2D Kinematic, BoxCollider2D, Tag=Player, PlayerController)
   Symbol_* (SpriteRenderer, CircleCollider2D isTrigger, EncounterSymbol)
   FieldCamera, Global Light 2D, Point Light 2D들
   FieldUI (Canvas) → 방향 버튼 4개(VirtualDPadButton), A 버튼(VirtualActionButton)
[BattleRoot] (비활성)
   BattleManager (+DebugBattlePresenter 또는 추후 DOTween Presenter)
   BattleCamera, 무대 배경, BattleUI
[FaderCanvas] (Sort Order 100) → 전체화면 검은 Image + CanvasGroup + ScreenFader
[DialogueCanvas] (Sort Order 60) → 모바일 대화 패널 + DialogueController
[QuestHUDCanvas] (Sort Order 25) → 현재 퀘스트 목표 + QuestManager
```
- GameFlowController 인스펙터: fieldRoot, battleRoot, battleManager 연결, devStarter에 스타터 1종 지정
- 세이브 파일: `Application.persistentDataPath/save_0.json` (prettyPrint 켜져 있어 바로 열어볼 수 있음)

## NPC 대화 세팅
1. Unity 메뉴 `Tools > Shadow Theater > Generate Dialogue UI Prefab` 실행
2. 생성된 `Assets/Prefabs/UI/DialogueCanvas.prefab`을 씬 최상위에 1개 배치
3. 씬에 EventSystem이 없다면 `GameObject > UI > Event System`으로 1개 생성
4. NPC에 `Collider2D`(Unit 레이어 권장)와 `StoryNpc` 추가
5. `firstDialogueId` / `repeatDialogueId`를 `Resources/Data/DialogueCatalog.json`의 ID와 맞춤

표지판이나 기억의 제단에는 `StoryNpc` 대신 `DialogueInteractable`을 붙인다. `oneShot`과
`completionFlag`를 함께 지정하면 한 번 조사한 뒤에는 재생되지 않는 이벤트 오브젝트가 된다.

대화 중에는 `PlayerController.Lock()`이 적용된다. 화면 탭 또는 에디터의 Space/Z/Enter로 진행하며,
첫 탭은 타자 효과를 완성하고 다음 탭은 다음 줄로 이동한다. 첫 대화를 끝내면 `completionFlag`가
세이브되어 이후 상호작용부터 반복 대사가 출력된다. ID는 추후 Ink를 도입할 때 knot 이름으로 유지한다.

### 대화 조건과 선택지
- 줄의 `requiredFlag`: 해당 플래그가 지정 값 이상일 때만 출력
- 줄의 `blockedFlag`: 해당 플래그가 없을 때만 출력
- 줄/선택지의 `setFlag`: 해당 지점에 도달하거나 선택했을 때 진행 플래그 기록
- 선택지의 `nextDialogueId`: 선택 후 이어질 대화 시퀀스
- 선택지의 `startQuestId`: 선택과 동시에 시작할 선택 퀘스트

선택지는 모바일 버튼으로 최대 3개 표시되며 에디터에서는 숫자키 1~3으로도 고를 수 있다.
기존 `lines`만 가진 JSON은 변경 없이 계속 사용할 수 있다.

## 퀘스트 세팅
1. Unity 메뉴 `Tools > Shadow Theater > Generate Quest HUD Prefab` 실행
2. 생성된 `Assets/Prefabs/UI/QuestHUDCanvas.prefab`을 씬 최상위에 1개 배치
3. `Resources/Data/QuestCatalog.json`에서 퀘스트와 다음 퀘스트 ID를 편집
4. 장소 도착 목표는 Trigger Collider2D 오브젝트에 `QuestAreaTrigger`를 붙이고 `areaId`를 일치시킴

목표 타입은 `Talk`, `Reach`, `Record`, `Defeat`, `Flag`이다. NPC 대화 완료, 그림자 기록 성공,
전투 승리는 각각 기존 런타임에 연결되어 자동 집계된다. `targetId`를 `*`로 지정하면 종류가 맞는
모든 이벤트를 집계한다. 진행도와 보상 수령 여부는 세이브 버전 2에 저장된다.

## 보스 컷신 세팅
보스 오브젝트에 비 Trigger `Collider2D`와 `BossEncounterTrigger`를 붙이고 Unit 레이어로 지정한다.
`enemyParty`에 보스/부하 ShadowData, `encounterId`, 레벨 범위, 전투 전/승리 대화 ID를 입력한다.
플레이어가 정면에서 상호작용하면 전투 전 대사가 재생되고 보스전으로 전환된다. 승리하면 필드 복귀 후
정화 대사를 재생하고 `victoryFlag`와 고정 인카운터 클리어를 저장한다. 패배하면 보스는 남아 재도전할 수 있다.

## 타이틀과 스타터 선택
1. 메뉴 `Tools > Shadow Theater > Generate Title and Starter UI` 실행
2. 생성된 `Assets/Prefabs/Systems/CoreSystems.prefab`과 `Assets/Prefabs/UI/TitleCanvas.prefab`을 타이틀 씬에 배치
3. EventSystem을 1개 배치
4. `TitleCanvas > StarterSelectionController > Starters`에 아래 3개 ShadowData를 순서대로 등록
   - 붉은 불꽃: 멸망한 왕국의 기사
   - 푸른 서리: 금기된 책의 마도사
   - 자줏빛 그림자: 밤의 방랑 야수
5. `TitleScreenController`의 `firstScene`, `firstCell`, 시작 아이템을 설정

이어하기 버튼은 정상 세이브가 있을 때만 활성화된다. 새 게임은 스타터 선택 직후 세이브를 만들고
첫 씬의 지정 좌표로 이동한다. `CoreSystems`는 씬 전환 후에도 유지되며 저장과 맵 이동을 담당한다.

## 지역 간 맵 이동
1. 이동 대상이 되는 모든 씬을 `File > Build Settings > Scenes In Build`에 추가
2. 길 끝/문 타일에 Collider2D와 `MapPortal`을 추가
3. 자동 출구는 `activateOnTouch=true` + Trigger Collider, 문은 false + 비 Trigger Collider로 설정
4. `targetScene`, `arrivalCell`, `arrivalFacing`을 반대편 입구의 안전한 타일로 지정
5. 여관/극장 입구처럼 부활 지점도 갱신할 곳은 `setCheckpoint` 활성화

맵 이동 시 화면이 페이드되고 목표 씬/좌표/방향이 먼저 안전 저장된다. 도착 직후 0.65초 동안
포털 재진입을 막아 양방향 출구 사이에서 즉시 되돌아가는 현상을 방지한다. `requiredFlag`와
`blockedFlag`로 스토리 진행에 따른 출구 잠금도 가능하다.

## 다중 엔딩 세팅
`CoreSystems.prefab`에는 `EndingManager`가 포함된다. 최종 무대의 상호작용 오브젝트에 Collider2D와
`EndingTrigger`를 붙이고, 최종장 진입 시 `story_finale_unlocked` 플래그를 설정한다. 플레이어가
상호작용하면 마지막 선택 대사 후 `Resources/Data/EndingCatalog.json`의 조건을 우선순위순으로 판정한다.

선택지의 `flagChanges`에는 누적할 플래그와 증감값을 여러 개 지정할 수 있다. 기본 성향은 다음과 같다.
- `choice_memory`: 양수는 기억 보존, 음수는 기억 놓아주기
- `choice_compassion`: 당사자의 선택과 연민을 존중
- `choice_control`: 검열과 강제 통제를 선택

현재 샘플 엔딩은 `이름을 되찾은 극장`, `끝나지 않는 공연`, `다정한 망각의 새벽`, `텅 빈 막`의
4종이다. 해금된 엔딩 ID와 마지막 엔딩 ID는 세이브 버전 3에 보존된다. 한 세이브에서 마지막 선택을
되돌려 점수를 반복 누적하지 않도록 `EndingTrigger.allowReplay`의 기본값은 false다.

## 각본집 도감 세팅
1. 메뉴 `Tools > Shadow Theater > Generate Script Book UI` 실행
2. 생성된 `Assets/Prefabs/UI/ScriptBookCanvas.prefab`을 각 필드 씬 최상위에 배치
3. `Resources/ShadowDatabase.asset > Shadows`에 전체 ShadowData를 원하는 도감 번호 순서로 등록
4. 각 ShadowData의 `loreLocked`, `loreUnlocked`, 실루엣, 포인트 컬러를 입력

프리팹에는 필드용 `각본집` 버튼이 포함된다. 에디터에서는 Tab으로 열고 Escape/X로 닫을 수 있다.
각본집이 열리면 플레이어 이동이 잠기며 전투·대화 중에는 열리지 않는다. 공개 단계는 다음과 같다.
- 미조우: 번호와 `???`만 표시
- 조우: 이름과 검은 실루엣, `loreLocked` 표시
- 기록 완료: 포인트 컬러, 속성/역할, 기본 능력치, `loreUnlocked` 전체 표시

전체·조우·기록 완료 필터와 `기록 수 / 전체 수` 진행률을 지원한다. 신규 기록은 기존
`SaveManager.MarkRecorded()` 및 전투 포획 흐름을 그대로 사용하므로 별도 세이브 설정이 필요 없다.

## 그림자 기억 성장 세팅
각 `ShadowData`의 `Memory Growth`에서 세 형태를 설정한다.

- `restoredForm`: 기억 복원 형태. 권장 레벨 10~15
- `salvationForm`: 구원 진명 각성. 권장 레벨 25 이상
- `grudgeForm`: 원한 진명 각성. 권장 레벨 25 이상

사용할 형태는 `enabled`를 켜고 `formName`, 실루엣, HDR 포인트 컬러, 요구 레벨,
`requiredFlag`, 능력치 배율, 추가 스킬, 추가 Lore를 입력한다. `requiredFlag`에는 해당 그림자의
개인 비극 퀘스트 완료 플래그를 연결한다. 예: `memory_knight_restored`, `truth_knight_revealed`.

성장 규칙:
1. 기록 직후에는 모든 개체가 `잔영`이다.
2. 기억 복원은 잔영 상태에서만 가능하며 복원 형태의 레벨/플래그를 검사한다.
3. 진명 각성은 기억 복원 후에만 가능하며 구원/원한 형태가 서로 다른 조건과 능력치를 가질 수 있다.
4. 각성 순간 현재 HP 비율을 유지하고 최대 HP, 공격, 방어, 속도, 치명타, 회피가 즉시 갱신된다.
5. 형태별 `bonusSkills`는 전투 AI와 플레이어 스킬 검증에 실제 보유 스킬로 포함된다.
   진명 각성 후에도 기억 복원 단계에서 얻은 추가 스킬은 유지된다.

각본집은 파티와 서고에서 해당 종의 가장 높은 성장 단계/레벨 개체를 찾아 표시한다. 상세 화면의
`기억 복원`, `구원 각성`, `원한 각성` 버튼에서 조건을 확인하고 성장을 실행한다. 성장 단계와 분기,
해금된 형태는 `ShadowInstance`에 저장되며 구버전 세이브는 버전 4로 자동 이관된다.

## 전투만 먼저 확인할 때
빈 씬 → GameObject에 BattleManager + BattleTestBootstrap → ShadowData 연결 → Play.
화면 좌상단 OnGUI 버튼으로 조작, 로그는 Console.

## 다음 작업 후보
1. BattleUI (uGUI) — BattleManager 이벤트 바인딩 (HP바, FP 구슬, 행동 메뉴)
2. DOTween BattlePresenter — 돌진/피격/컷인/카메라 쉐이크 (웹 프로토타입 연출 그대로)
3. 파티 편성·보관함 UI
4. 엔딩 갤러리와 회차 시작
5. 설정/오디오/언어 메뉴
