# 그림자 극장 잔영 — 스크립트 구성 & 씬 세팅

## 폴더
```
Assets/Scripts/
  Data/    ShadowEnums, SkillData(SO), ShadowData(SO), ItemData(SO), ShadowInstance, ShadowDatabase(SO)
  Battle/  BattleTypes, BattleUnit, DamageCalculator, BattleAI, IBattlePresenter,
           DebugBattlePresenter, BattleManager, BattleTestBootstrap
  Save/    SaveData, SaveManager
  Field/   FieldGrid, PlayerController, EncounterSymbol, ScreenFader,
           VirtualDPadButton, VirtualActionButton, GameFlowController
  Story/   DialogueData/Repository, StoryNpc, DialogueInteractable,
           QuestData/Repository/Manager, QuestAreaTrigger
  UI/      DialogueController, QuestHudController
  Editor/  DialogueUIPrefabGenerator, QuestHudPrefabGenerator
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

## 퀘스트 세팅
1. Unity 메뉴 `Tools > Shadow Theater > Generate Quest HUD Prefab` 실행
2. 생성된 `Assets/Prefabs/UI/QuestHUDCanvas.prefab`을 씬 최상위에 1개 배치
3. `Resources/Data/QuestCatalog.json`에서 퀘스트와 다음 퀘스트 ID를 편집
4. 장소 도착 목표는 Trigger Collider2D 오브젝트에 `QuestAreaTrigger`를 붙이고 `areaId`를 일치시킴

목표 타입은 `Talk`, `Reach`, `Record`, `Defeat`, `Flag`이다. NPC 대화 완료, 그림자 기록 성공,
전투 승리는 각각 기존 런타임에 연결되어 자동 집계된다. `targetId`를 `*`로 지정하면 종류가 맞는
모든 이벤트를 집계한다. 진행도와 보상 수령 여부는 세이브 버전 2에 저장된다.

## 전투만 먼저 확인할 때
빈 씬 → GameObject에 BattleManager + BattleTestBootstrap → ShadowData 연결 → Play.
화면 좌상단 OnGUI 버튼으로 조작, 로그는 Console.

## 다음 작업 후보
1. BattleUI (uGUI) — BattleManager 이벤트 바인딩 (HP바, FP 구슬, 행동 메뉴)
2. DOTween BattlePresenter — 돌진/피격/컷인/카메라 쉐이크 (웹 프로토타입 연출 그대로)
3. 대화 선택지와 조건 분기(퀘스트 목표 추적/HUD는 완료)
4. 타이틀 / 스타터 선택 / 맵 이동(MapLoader)
5. 무한의 훈련소 (TrainingTower: 층 루프 + 배속/오토)
