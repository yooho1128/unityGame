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
```
- GameFlowController 인스펙터: fieldRoot, battleRoot, battleManager 연결, devStarter에 스타터 1종 지정
- 세이브 파일: `Application.persistentDataPath/save_0.json` (prettyPrint 켜져 있어 바로 열어볼 수 있음)

## 전투만 먼저 확인할 때
빈 씬 → GameObject에 BattleManager + BattleTestBootstrap → ShadowData 연결 → Play.
화면 좌상단 OnGUI 버튼으로 조작, 로그는 Console.

## 다음 작업 후보
1. BattleUI (uGUI) — BattleManager 이벤트 바인딩 (HP바, FP 구슬, 행동 메뉴)
2. DOTween BattlePresenter — 돌진/피격/컷인/카메라 쉐이크 (웹 프로토타입 연출 그대로)
3. NPC 대화 (Ink) + IInteractable NPC + 퀘스트 플래그 연동
4. 타이틀 / 스타터 선택 / 맵 이동(MapLoader)
5. 무한의 훈련소 (TrainingTower: 층 루프 + 배속/오토)
