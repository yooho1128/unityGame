# 그림자 극장 잔영 — 스크립트 구성 & 씬 세팅

## 폴더
```
Assets/Scripts/
  Data/    ShadowEnums, SkillData(SO), ShadowData(SO), ItemData(SO), ShadowInstance, ShadowDatabase(SO)
  Battle/  BattleTypes, BattleUnit, DamageCalculator, BattleAI, IBattlePresenter,
           DebugBattlePresenter, BattleManager, BattleTestBootstrap
  Save/    SaveData, SaveManager
  Field/   FieldGrid, PlayerController, EncounterSymbol, ScreenFader,
           VirtualDPadButton, VirtualActionButton, VirtualScriptBookButton, VirtualPartyButton,
           GameFlowController, MapLoader, MapPortal, FieldCameraFollow, FieldAtmosphereController,
           FieldAmbientAudio
  Story/   DialogueData/Repository, StoryNpc, DialogueInteractable,
           QuestData/Repository/Manager, QuestAreaTrigger, BossEncounterTrigger,
           EndingData/Repository/Manager, EndingTrigger, RegionData/Repository/Progress
  UI/      DialogueController, DialogueChoiceView, QuestHudController,
           TitleScreenController, StarterSelectionController, StarterCardView,
           EndingGalleryController, EndingGalleryEntryView, GameSettings, SettingsPanelController,
           WorldMapController, RegionMapNodeView,
           ScriptBookController, ScriptBookEntryView, BattleUIController, BattleFxDirector,
           BattleSfxPlayer,
           BattleUnitPanel, BattleOptionButton, PartyStorageController, PartyStorageEntryView
  Editor/  DialogueUIPrefabGenerator, QuestHudPrefabGenerator, FrontEndPrefabGenerator,
           ScriptBookPrefabGenerator, BattleUIPrefabGenerator, PartyStoragePrefabGenerator,
           WorldMapPrefabGenerator, CoreContentBatchGenerator, LegendaryGrowthBatchGenerator,
           PlayablePrologueGenerator
```

## 프로젝트 설정
- Unity 2022.3 LTS 이상, 템플릿 **2D (URP)**
- Player Settings → Active Input Handling = **Both** (에디터 키보드 테스트용. 모바일은 가상 패드 사용)
- Tag `Player` 추가, Layer `Obstacle`, `Unit` 추가

## 초반 데이터 생성
1. 메뉴 `Tools > Shadow Theater > Generate Core Content Data` 실행
2. `Assets/Data/Generated/Skills`의 스킬 130개 확인
3. `Assets/Data/Generated/Items`의 도구 4개 확인
4. `Assets/Data/Generated/Shadows`의 스타터·프롤로그·제2~7막 그림자 67종 확인
5. `Assets/Resources/ShadowDatabase.asset` 자동 등록 결과 확인

원본은 `Resources/Data/CoreContentCatalog.json`이며 같은 메뉴를 반복 실행하면 ID를 기준으로 기존
ScriptableObject를 갱신한다. 스타터 3종과 수집 가능한 일반 그림자에는 세 단계 성장 데이터가 들어 있다.
전용 아트가 없는 동안에는 `Assets/Art/Generated/Core`에 기능 테스트용 실루엣을 생성한다.

## 플레이 가능한 프롤로그·제2~7막 생성
1. 메뉴 `Tools > Shadow Theater > Generate Playable Prologue` 실행
2. 현재 열려 있는 씬의 변경 사항을 저장하거나 폐기할지 선택
3. 생성이 끝나면 자동으로 열리는 `Assets/Scenes/Prologue/Title.unity`에서 Play

이 메뉴는 프롤로그 4개 필드, `CurtainPass`, 제2~7막 각 5개 필드를 만들고 총 36개 씬을
Build Settings에 등록한다. 제2막은 `Docs/ACT2_CROWN_OF_ASH.md`, 제3막은
`Docs/ACT3_FORBIDDEN_ARCHIVE.md` 참고.

이 메뉴는 핵심·전설 데이터와 모든 UI 프리팹을 먼저 갱신한 뒤 아래 씬을 만든다.

| 씬 | 포함 내용 |
|---|---|
| `Title` | 이어하기, 스타터 3종 선택, 첫 지역 진입 |
| `PrologueTheater` | 아리아, 첫 퀘스트, 기억의 제단 |
| `EchoVillage` | 개방형 마을 필드, 표지판, 양방향 포털 |
| `MoonlitMeadow` | 도착 목표, 등불지기, 야생 심볼 4종, 보스 입구 |
| `MoonlitBossStage` | 달빛의 미망인 보스전과 정화 보상 |
| `CurtainPass` | 프롤로그 이후 전환 퀘스트와 제2막 관문 |
| `AshBorder` | 재 황무지 사냥터, 정찰병, 불씨 사냥개·창병 |
| `CinderCity` | 제2막 거점, 기록관 선택지, 병영/지하묘 분기 |
| `RuinedBarracks` | 병영 던전, 무릎 꿇지 않는 대장 보스전 |
| `EmberCatacombs` | 묘실 던전, 잿빛 사제, 목 없는 호위기사 보스전 |
| `CrownlessThrone` | 재의 왕 전설 보스전과 제2막 완료 플래그 |
| `FrostPort` | 제3막 관문, 빙하 뱃사공과 승객 명부 |
| `WhiteArchive` | 눈송이 서기, 금서 색인 복원 |
| `ForbiddenStacks` | 책 먹는 용 지역 보스전 |
| `MirrorVault` | 엔딩 성향 선택지와 반전된 사서 보스전 |
| `BlueAbyss` | 무한서고의 용 전설 보스전과 제3막 완료 플래그 |
| `VioletMarsh` | 제4막 관문, 늪뱀·등불 두꺼비 사냥터 |
| `HowlVillage` | 달술사와 야수 가면 선택 분기 |
| `MoonfangForest` | 초승달 여우·그림자 사슴의 개방형 숲 |
| `BloodmoonRidge` | 붉은 뿔의 추격자 지역 보스전 |
| `SleepingBeastDen` | 밤을 삼킨 짐승 전설 보스전과 제4막 완료 플래그 |
| `ThreadMarket` | 제5막 관문, 천 조각 고양이·바늘 도둑 사냥터 |
| `ClockworkAlley` | 초침 무희와 멈춘 태엽 골목 |
| `MarionetteOpera` | 줄 위의 프리마돈나 지역 보스전 |
| `SeveredWorkshop` | 봉합 기사와 자유 의지 선택 분기 |
| `PuppeteerStage` | 마지막 인형사 전설 보스전과 제5막 완료 플래그 |
| `ErasedStation` | 제6막 관문, 표 없는 승객·먹칠 역무원 |
| `BlankPrison` | 번호 죄수와 제0호 간수 보스전 |
| `RedactionLab` | 원한 처리 선택과 추출 실험체 M 보스전 |
| `SilentCourt` | 무언의 재판장 보스전 |
| `BlackArchive` | 대검열관 노크스 전설 보스전과 제6막 완료 플래그 |

각 필드는 23×19 타일이며 가장자리 벽과 내부 장애물이 있는 개방형 2D 구조다. 한 줄 통로가 아니라
장애물의 위·아래 경로를 선택할 수 있다. 지역별 타일 팔레트, 카메라 배경색, 지역명 HUD가 다르며
필드 카메라는 플레이어의 타일 이동을 부드럽게 추적한다. 생성한 `.unity`, `.prefab`, `.asset`,
`.png`, `.meta` 파일은 모두 Git에 커밋한다.

생성기는 타일에 URP 2D Sprite-Lit 재질을 적용하고 각 지역에 글로벌 라이트와 3~5개의 Point Light 2D를
배치한다. `Environment` 아래의 안개와 빛가루 오브젝트는 호환성을 위해 남지만 픽셀 필드에서는 비활성화되어
Point 필터의 선명한 가장자리를 유지한다. 렌더 파이프라인 자산이 전혀
없을 때만 `ShadowTheaterURP.asset`과 `ShadowTheater2DRenderer.asset`을 만들어 Project/Quality
설정에 연결한다. 이미 지정된 렌더 파이프라인은 보존한다.

같은 `Environment`에는 `FieldAmbientAudio`가 포함된다. 지역별 6초 무봉제 지속음과 간헐 원샷을
런타임에 생성하며 극장·마을·초원·월식 보스·재 황무지·불씨 성도·지하묘·재의 왕좌의 파형과 음역이
다르다. `ambienceClip`과 `detailClip`을
지정하면 완성 음원으로 자동 교체되고, `ambienceVolume`, `detailVolume`, `detailInterval`로 지역별
밀도를 조절한다. `MapLoader`와 `GameFlowController`가 맵 이동·전투 진입 전에 환경음 페이드를
시작하며 필드 복귀 시 자동으로 다시 페이드 인한다.

기본 진행 순서:
1. 잔향 극장에서 아리아와 대화
2. 잔향 마을을 지나 달빛 초원 도착
3. 야생 그림자를 기록하고 등불지기와 대화
4. `prologue_04_echoes` 완료 후 북쪽 보스 입구 개방
5. 달빛의 미망인을 정화해 파티 또는 각본 서고에 합류
6. 찢어진 장막길과 재의 국경을 지나 불씨 성도 도착
7. 병영과 지하묘의 두 증언을 모아 왕관 없는 옥좌 개방
8. 재의 왕을 정화하고 `act2_complete` 플래그 획득

달빛 초원 진입 포털은 초원 서쪽 입구를 체크포인트로 지정한다. 보스 무대처럼 다른 씬에서 패배한
경우에도 `GameFlowController`가 달빛 초원 씬을 다시 로드하고 파티를 회복한다.

## 씬 계층 (단일 씬 + 루트 토글)
```
[Systems]         SaveManager, GameFlowController   ← FieldRoot/BattleRoot 바깥!
[FieldRoot]
   Grid (+FieldGrid)
      Ground (Tilemap)
      Collision (Tilemap, 렌더러 꺼도 됨) → FieldGrid.collisionTilemap
   Player  (SpriteRenderer, Rigidbody2D Kinematic, BoxCollider2D, Tag=Player, PlayerController)
   Symbol_* (SpriteRenderer, CircleCollider2D isTrigger, EncounterSymbol)
   FieldCamera
   Environment (+FieldAtmosphereController, FieldAmbientAudio)
      GlobalMoonlight (Global Light 2D), LocalLight_* (Point Light 2D)
      FogLayers (6), Motes (24)
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

### 전역 설정

같은 생성 메뉴가 `CoreSystems`에 `GameSettings`, `TitleCanvas`에 설정 패널을 함께 구성한다.
설정은 게임 세이브 JSON이 아니라 `PlayerPrefs`에 별도로 저장되므로 새 게임과 다음 회차에서도 유지된다.

- 전체 음량: `AudioListener.volume`에 즉시 반영
- 환경음: `FieldAmbientAudio`의 지속음·간헐음에 즉시 반영
- 효과음: `BattleSfxPlayer`의 스킬·필살기 음량에 반영
- 진동: Android/iOS 전투 타격 프레임에서 실행
- 대화 속도: 느리게 24자/초, 보통 42자/초, 빠르게 72자/초
- 언어: 한국어/English 선택 상태와 변경 이벤트 제공(영문 문자열 카탈로그는 추후 연결)

## 지역 간 맵 이동
1. 이동 대상이 되는 모든 씬을 `File > Build Settings > Scenes In Build`에 추가
2. 길 끝/문 타일에 Collider2D와 `MapPortal`을 추가
3. 자동 출구는 `activateOnTouch=true` + Trigger Collider, 문은 false + 비 Trigger Collider로 설정
4. `targetScene`, `arrivalCell`, `arrivalFacing`을 반대편 입구의 안전한 타일로 지정
5. 여관/극장 입구처럼 부활 지점도 갱신할 곳은 `setCheckpoint` 활성화

맵 이동 시 화면이 페이드되고 목표 씬/좌표/방향이 먼저 안전 저장된다. 도착 직후 0.65초 동안
포털 재진입을 막아 양방향 출구 사이에서 즉시 되돌아가는 현상을 방지한다. `requiredFlag`와
`blockedFlag`로 스토리 진행에 따른 출구 잠금도 가능하다.

## 40개 지역 월드맵

1. 메뉴 `Tools > Shadow Theater > Generate World Map UI` 실행
2. 생성된 `Assets/Prefabs/UI/WorldMapCanvas.prefab`을 각 필드 씬에 배치
3. `Resources/Data/RegionCatalog.json`의 `sceneName`과 실제 씬 이름을 일치시킴
4. 완성한 지역 씬을 Build Settings에 등록

프리팹의 `월드맵` 버튼은 40개 지역을 진행 순서로 보여준다. 처음 방문한 지역과 인접한 경로가 자동
해금되며, 스토리 관문은 `requiredFlag`까지 만족해야 한다. 새 지역은 필드에서 직접 발견해야 하고 방문
완료 지역만 빠른 이동할 수 있다. 자세한 전체 경로는 `Docs/WORLD_MAP.md` 참고.

## 다중 엔딩 세팅
`CoreSystems.prefab`에는 `EndingManager`가 포함된다. 최종 무대의 상호작용 오브젝트에 Collider2D와
`EndingTrigger`를 붙이고, 최종장 진입 시 `story_finale_unlocked` 플래그를 설정한다. 플레이어가
상호작용하면 마지막 선택 대사 후 `Resources/Data/EndingCatalog.json`의 조건을 우선순위순으로 판정한다.

선택지의 `flagChanges`에는 누적할 플래그와 증감값을 여러 개 지정할 수 있다. 기본 성향은 다음과 같다.
- `choice_memory`: 양수는 기억 보존, 음수는 기억 놓아주기
- `choice_compassion`: 당사자의 선택과 연민을 존중
- `choice_control`: 검열과 강제 통제를 선택

현재 샘플 엔딩은 `이름을 되찾은 극장`, `끝나지 않는 공연`, `다정한 망각의 새벽`, `텅 빈 막`의
4종이다. 해금된 엔딩 ID, 현재 회차, 완주 횟수는 세이브 버전 5에 보존된다. 한 세이브에서 마지막
선택을 되돌려 점수를 반복 누적하지 않도록 `EndingTrigger.allowReplay`의 기본값은 false다. 결말
재생 후에는 기본적으로 `Title` 씬으로 돌아가며 `returnToTitleAfterEnding`으로 이 동작을 끌 수 있다.

타이틀의 `엔딩 기록관`은 잠긴 결말의 제목·내용을 숨기고 해금된 결말만 상세 표시한다. 엔딩을 한 번
이상 본 세이브에는 `다음 회차 시작` 버튼이 나타난다. 새 회차는 스타터를 다시 선택하며 엔딩 도감과
완주 횟수만 유지하고 파티, 서고, 퀘스트, 선택 성향, 재화, 도구는 초기화한다. 완전히 처음부터
시작하려면 기존 `새로운 기억`을 사용한다.

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

### 전설·보스 1차 데이터 생성
1. 메뉴 `Tools > Shadow Theater > Generate Legendary Growth Batch` 실행
2. 생성된 `Assets/Data/Generated/Shadows`의 3개 ShadowData와 전용 스킬을 확인
3. 달빛 초원 보스의 `BossEncounterTrigger.purificationReward`에 `boss_moonlit_widow`를 연결
4. 보상 레벨과 `purificationRewardFlag`를 지정해 중복 지급을 방지

생성기는 `LegendaryGrowthCatalog.json`의 수치와 Lore를 읽고 성장 시트를 잔영·복원·구원·원한
Sprite로 4등분한다. 같은 메뉴를 다시 실행하면 기존 ID의 에셋을 갱신하며 ShadowDatabase에도
중복 등록하지 않는다. 지역 보스 보상은 파티가 6명이면 자동으로 각본 서고에 들어간다.

## 전투만 먼저 확인할 때
빈 씬 → GameObject에 BattleManager + BattleTestBootstrap → ShadowData 연결 → Play.
화면 좌상단 OnGUI 버튼으로 조작, 로그는 Console.

## 모바일 전투 UI 세팅
1. 메뉴 `Tools > Shadow Theater > Generate Battle UI` 실행
2. `Assets/Prefabs/UI/BattleCanvas.prefab`을 BattleRoot로 배치하고 초기 비활성화
3. `GameFlowController.battleRoot`에 프리팹 루트, `battleManager`에 내부 BattleManager 연결
4. 씬의 EventSystem은 필드 UI와 공용으로 1개만 유지

생성 프리팹은 BattleManager와 `IBattlePresenter` 구현체를 함께 포함한다. 플레이어/적 실루엣,
HP·상태·FP·턴 표시와 공격, 스킬, 교체, 각본 기록, 도구, 도주 버튼을 제공한다. 스킬/교체/도구는
동적 스크롤 목록이며 기절 시 강제 교체 화면으로 자동 전환한다. Auto와 1/2/3배속도 상단에서 조작한다.

`BattleFxDirector`는 패키지 의존성 없이 일반 스킬의 돌진·피격 흔들림과 진명 필살기의 전용 컷인,
화면 섬광, 테마 문양을 재생한다. `SkillData`의 `ultimateFxStyle`, 두 FX 색상, 파편 수로 조정하며
Core/Legendary JSON 값을 바꾼 뒤 각 데이터 생성 메뉴를 다시 실행하면 된다. 수집 가능한 9종의
구원·원한 진명에는 각각 전용 필살기 1개, 총 18개가 연결되어 있다.

전투 프리팹에는 `BattleSfxPlayer`도 자동 포함된다. `SkillData.sfxClip`이 비어 있으면 연출 테마별
합성음을 런타임에 한 번 생성해 캐시하고, 음원이 지정되면 실제 음원을 우선 재생한다. `sfxVolume`,
`sfxPitch`, `hitStopDuration`, `cameraShake`로 각 스킬의 감각을 조정할 수 있다. 카메라는 전투 시작 시
활성 `MainCamera`를 자동 탐색하므로 별도 프리팹 참조가 필요 없다.

## 파티 편성·각본 서고 세팅
1. 메뉴 `Tools > Shadow Theater > Generate Party and Storage UI` 실행
2. 생성된 `Assets/Prefabs/UI/PartyStorageCanvas.prefab`을 각 필드 씬 최상위에 배치
3. 씬의 EventSystem은 다른 모바일 UI와 공용으로 1개만 유지

프리팹에는 필드에서 편성 화면을 여는 `파티` 버튼이 포함된다. 전투나 대화 중에는 열리지 않으며,
화면이 열려 있는 동안 플레이어 이동을 잠근다. 위에서부터 파티·각본 서고 목록을 확인하고 선택한
그림자를 `파티로`, `서고로` 이동하거나 `위로`, `아래로` 버튼으로 출전 순서를 바꿀 수 있다.

- 파티 최대 인원은 `SaveData.MaxPartySize`의 6명이다.
- 포획 시 파티가 가득 찼으면 `SaveData.storage`에 자동 보관되며 서고 수량 제한은 없다.
- 파티는 최소 1명을 유지하고, 전투 가능한 마지막 그림자는 서고로 이동할 수 없다.
- 이동·순서 변경 성공 시 즉시 안전 저장하며 HP와 기억 성장 단계도 그대로 유지한다.
- 첫 번째 파티원이 전투의 선봉으로 사용되므로 순서 변경은 다음 인카운터부터 반영된다.

## 다음 작업 후보
1. 필살기별 실제 파티클·녹음 SFX 에셋 교체
2. 자동 생성 픽셀 타일·캐릭터를 최종 수작업 픽셀 아트와 녹음 환경음으로 교체
3. 영문 UI·대사 문자열 카탈로그와 폰트 폴백 연결
4. 최종막 `뒤집힌 로비`부터 마지막 장막 필드·그림자 데이터 제작

현재 `Generate Playable Prologue`는 프롤로그부터 제7막까지 35개 필드를 모두 32px 픽셀 규칙으로 생성한다.
보이는 벽·수풀·폐허는 `Collision` 타일맵에 직접 그려져 이동 판정과 일치하며, NPC와 보스의 24×32 필드
스프라이트도 `ShadowData`의 역할·색·등급에 따라 `Assets/Art/Generated/Characters`에 자동 생성된다.
