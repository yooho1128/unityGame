# 그림자 극장 잔영 (Shadow Theater)

1인 개발 2D 모바일 RPG — 스토리 중심 타일맵 필드 탐색 + 1대1 수집형 턴제 전투 (Unity 2D URP).

| 폴더 | 내용 |
|---|---|
| `Assets/Scripts/Data` | ShadowData / SkillData / ItemData (ScriptableObject), ShadowInstance, ShadowDatabase |
| `Assets/Scripts/Battle` | BattleManager(턴 상태 머신), BattleUnit, DamageCalculator, BattleAI, IBattlePresenter |
| `Assets/Scripts/Save` | SaveData(JSON), SaveManager, 파티·각본 서고 편성 API |
| `Assets/Scripts/Field` | PlayerController(타일 이동), EncounterSymbol(심볼 인카운터), GameFlowController(필드↔전투) |
| `Assets/Scripts/Story` | JSON 대사/퀘스트/40개 지역 저장소, 목표 추적, 누적 선택 기반 다중 엔딩 |
| `Assets/Scripts/UI` | 모바일 대화창, 퀘스트 HUD, 월드맵, 스타터 선택, 각본집 도감, 파티·서고 편성, 전역 설정 |
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

처음 열 때 Safe Mode가 표시되면 진입해 Console의 첫 오류부터 확인합니다. `ENOSPC`가 보이면 코드
문제가 아니라 디스크 용량 부족이므로 Unity를 종료하고 프로젝트의 `Library`와 로컬 Unity 패키지
캐시를 지운 뒤 충분한 공간에서 다시 가져옵니다. URP가 제공하는 동명 타입과 게임의 `ShadowData`가
충돌하지 않도록 에디터 생성기는 게임 데이터 타입 별칭을 명시합니다.

## 초반 콘텐츠 데이터 빠른 설치
1. Unity 메뉴 **Tools → Shadow Theater → Generate Core Content Data** 실행
2. `Assets/Data/Generated`에 생성된 그림자 37종, 스킬 88개, 도구 4개 확인
3. `Resources/ShadowDatabase.asset`에 모든 데이터가 자동 등록되었는지 확인

기본 카탈로그와 제2·3·4막 콘텐츠 팩을 자동 병합합니다. 스타터 3종과 주요 그림자는 기억 복원·구원·원한
성장 데이터를 포함합니다. 실제 전용 이미지가
아직 없는 개체에는 기능 테스트용 실루엣 PNG를 자동 생성하며, 이후 완성 아트로 교체해도 ID와 세이브는
그대로 유지됩니다. 원본 수치와 Lore는 `Assets/Resources/Data/CoreContentCatalog.json`에서 수정합니다.

## 플레이 가능한 프롤로그 자동 생성
1. Unity 메뉴 **Tools → Shadow Theater → Generate Playable Prologue** 실행
2. 현재 씬 저장 여부를 확인하면 필요한 데이터·UI 프리팹과 프롤로그 씬을 일괄 생성
3. 생성 후 자동으로 열린 `Title` 씬에서 Play

생성되는 동선은 `Title → 잔향 극장 → 잔향 마을 → 달빛 초원 → 달빛 보스 무대 → 찢어진 장막길 →
재의 국경 → 불씨 성도 → 무너진 병영/불씨 지하묘 → 왕관 없는 옥좌 → 서리 나루 → 백색 기록원 →
금단의 서가 → 거울 문고 → 푸른 심연 서고 → 자줏빛 늪 → 울음 마을 → 월아 숲 → 핏빛 달고개 →
잠든 야수의 굴`까지 이어집니다. 각 필드는
23×19 타일의 개방형 구조이며 상하좌우 탐색, 장애물 우회, 모바일 방향키/A 버튼, NPC 대화, 퀘스트,
심볼 인카운터, 각본 기록, 지역 보스 정화까지 한 흐름으로 확인할 수 있습니다. 21개 씬은 Build Settings에
자동 등록됩니다.

제2막에는 그림자 10종과 스킬 20개, 메인 퀘스트 5개, 선택 분기, 지역 보스 2종과 전설 보스
`재의 왕`이 포함됩니다. 전체 구성은 [`Docs/ACT2_CROWN_OF_ASH.md`](Docs/ACT2_CROWN_OF_ASH.md)를
참고하세요.

제3막에는 신규 그림자 10종과 스킬 20개, 메인 퀘스트 5개, 거울 문고 선택 분기, 지역 보스
`책 먹는 용`·`반전된 사서`와 전설 보스 `무한서고의 용`이 포함됩니다. 전체 구성은
[`Docs/ACT3_FORBIDDEN_ARCHIVE.md`](Docs/ACT3_FORBIDDEN_ARCHIVE.md)를 참고하세요.

제4막에는 신규 그림자 10종과 스킬 20개, 메인 퀘스트 5개, 울음 마을 선택 분기, 지역 보스
`붉은 뿔의 추격자`와 전설 보스 `밤을 삼킨 짐승`이 포함됩니다. 전체 구성은
[`Docs/ACT4_NIGHT_OF_BEASTS.md`](Docs/ACT4_NIGHT_OF_BEASTS.md)를 참고하세요.

각 필드에는 지역별 Sprite-Lit 재질, 글로벌 라이트와 3~5개의 포인트 조명이 생성됩니다. 픽셀 가장자리를
선명하게 유지하기 위해 기존 안개·빛가루 레이어와 포스트 프로세싱은 필드에서 비활성화됩니다. 제2막은
붉은 재폭풍·주황색 화로·청록색 묘실·진홍색 왕좌 팔레트, 제3막은 서리 항구·백색 기록원·남색 서가·
보랏빛 거울·푸른 심연 팔레트를 구분해 사용합니다. 렌더 파이프라인이 비어 있는 새
프로젝트에서는 전용 URP 2D Renderer 자산을 자동 생성하며 기존 설정은 덮어쓰지 않습니다.

각 지역의 지속 환경음과 간헐 효과음도 자동 생성됩니다. 극장은 낮은 무대 공명, 마을은 부드러운
바람과 등불음, 초원은 밤바람과 높은 빛벌레 소리, 보스 무대는 월식 저음을 사용합니다. 제2막에는
재바람, 성도 화로, 지하묘 공명, 왕좌 저음이 추가됩니다. 맵 이동과
전투 진입 시 화면 페이드와 함께 소리도 자연스럽게 줄어들며, 실제 AudioClip을 지정하면 합성음을
자동으로 대체합니다.

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
- 결말 후 타이틀의 `엔딩 기록관`에서 4개 결말의 해금률과 상세 문구 열람
- `다음 회차 시작`은 엔딩 도감·완주 횟수만 계승하고 파티·퀘스트·선택을 초기화

## 게임 설정

`Generate Title and Starter UI`로 만든 타이틀에는 설정 화면이 포함됩니다. 전체 음량, 환경음, 효과음,
진동, 대화 출력 속도, 언어를 조절할 수 있으며 값은 `PlayerPrefs`에 저장되어 세이브 슬롯과 회차에
상관없이 유지됩니다. 환경음 슬라이더는 필드의 지속음과 간헐음을 즉시 갱신하고, 효과음은 전투 스킬과
필살기에 적용됩니다. 진동은 모바일 전투의 타격 순간에만 발생합니다. 언어 전환 상태와 변경 이벤트는
준비되어 있으며 실제 영문 대사·UI 문자열 카탈로그 연결은 다음 현지화 단계에서 확장합니다.

## 40개 지역 월드맵

8개 막, 총 40개 지역의 이름·씬 ID·권장 레벨·환경·연결 경로·대표 출현 그림자·지역 보스가
`Assets/Resources/Data/RegionCatalog.json`에 정의되어 있습니다. Unity 메뉴
**Tools → Shadow Theater → Generate World Map UI**를 실행하면 모바일 월드맵 프리팹이 생성됩니다.

월드맵은 잠김/새 지역/방문 완료/현재 위치를 구분합니다. 새 지역은 인접 필드 출구로 직접 발견해야 하고,
한 번 방문한 지역만 빠른 이동할 수 있습니다. 지역 해금과 방문 기록은 세이브 버전 6에 보존되며 기존
세이브는 현재 지역을 기준으로 자동 이전됩니다. 전체 막 구성은
[`Docs/WORLD_MAP.md`](Docs/WORLD_MAP.md)에서 확인할 수 있습니다.

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

진명을 얻은 수집 가능 그림자는 구원·원한 형태마다 전용 필살기 1개를 획득합니다. 현재 1차 구현은
일반/스타터 6종과 전설·보스 3종, 총 9종의 양쪽 분기 18개를 포함합니다. 불꽃 왕관, 얼어붙은 서고,
달의 야수, 꼭두각시 실, 재의 새, 얼음 가면, 달꽃, 살아 있는 각본, 우주 객석의 9개 테마가 각각
컷인·화면 섬광·돌진·피격 흔들림·색상 파편을 조합합니다.

### 전설·보스 성장 데이터 1차 묶음

달빛의 미망인, 최초의 배우, 마지막 관객의 4형태 콘셉트 시트와 전용 성장·스킬·Lore 데이터가
포함되어 있습니다. Unity 메뉴 **Tools → Shadow Theater → Generate Legendary Growth Batch**를
실행하면 시트 분할, SkillData/ShadowData 생성, ShadowDatabase 등록이 자동으로 처리됩니다.
분할기는 원본 시트를 네 장의 독립 PNG로 만든 뒤 각각 `Single Sprite`로 임포트합니다. Unity 2022의
레거시 다중 스프라이트 경계 판정에 영향을 받지 않으며 생성 결과는 `Assets/Art/Generated/Growth`에
저장됩니다.

형태별 이미지와 상세 설계는 [`Docs/LEGENDARY_GROWTH_BATCH_01.md`](Docs/LEGENDARY_GROWTH_BATCH_01.md)를 참고하세요.

## 모바일 전투 UI 빠른 설치
1. Unity 메뉴 **Tools → Shadow Theater → Generate Battle UI** 실행
2. 생성된 `Assets/Prefabs/UI/BattleCanvas.prefab`을 씬의 BattleRoot로 배치
3. `GameFlowController`의 `battleRoot`, `battleManager`에 생성된 오브젝트를 연결

공격·스킬·교체·각본 기록·도구·도주, 강제 교체, Auto, 1/2/3배속을 지원합니다. 스킬·파티·도구
목록은 현재 전투 데이터에서 자동 생성됩니다. `isUltimate` 스킬은 `BattleFxDirector`가 스킬별
테마와 팔레트를 읽어 별도 외부 패키지 없이 진명 컷인을 재생합니다. `BattleSfxPlayer`는 9개 연출
테마에 맞는 임시 합성 SFX를 런타임 생성하며, 실제 `sfxClip`을 지정하면 해당 음원을 우선 사용합니다.
타격 순간에는 실제 전투 카메라 임펄스와 짧은 히트 스톱이 함께 적용됩니다.

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

## 픽셀 필드 비주얼 패스

프롤로그부터 제4막까지 플레이 가능한 20개 필드는 고전 휴대용 수집형 RPG처럼 32px 격자 픽셀 타일맵으로
렌더링합니다. 지역별 바닥·벽·장애물은 Point 필터와 무압축 설정으로 생성되며, 화면에 보이는 물·절벽·
수풀·폐허 타일이 그대로 충돌 타일이 됩니다. 따라서 배경 그림과 실제 이동 판정이 어긋나지 않습니다.
기존 고해상도 달빛 초원 이미지는 콘셉트 원본으로만 보관하며 런타임 필드에는 표시하지 않습니다.

필드 플레이어는 24×32 픽셀 규격의 극단주를 사용합니다. 아래·위·옆 방향마다 2프레임을 생성하고,
왼쪽은 옆 방향을 반전하여 총 4방향 이동을 표현합니다. 걷는 동안 `PixelFieldAnimator`가 프레임을
전환하며 모든 스프라이트에는 Point 필터와 Unity 2022 호환 피벗 설정이 자동 적용됩니다. 달빛 초원의
등불지기와 필드 인카운터 표식도 같은 24px 계열 픽셀 규격을 사용합니다. 다른 NPC·상호작용 대상·
지역 보스는 그림자의 역할군, 포인트 컬러, 성장 등급을 조합한 전용 필드 픽셀 스프라이트를 자동 생성합니다.
모든 필드 카메라는 32 PPU 단위로 위치를 스냅하고 포스트 프로세싱 안개를 끄므로 이동 중 픽셀 가장자리가
흐려지지 않습니다.

달빛 초원은 중앙 공터, 서쪽 입구, 북쪽 길, 상·하단 우회로와 동쪽 연결로를 이동할 수 있습니다.
보스 출구와 복귀 지점도 북쪽 길의 `(-3, 8)` / `(-3, 7)` 좌표에 맞춰 배치됩니다.

필드 전투에서 복귀할 때는 `FieldGrid`가 다시 활성화될 때까지 심볼 AI가 대기한 후 위치와 배회를
복원합니다. 따라서 필드 루트 활성화 순서에 따른 `EncounterSymbol.OnEnable` null 오류가 발생하지
않습니다. 씬 생성기는 저장 전에 계층 전체의 오래된 `Missing Script` 컴포넌트를 자동 제거하고,
`Assets/Prefabs`의 생성 프리팹도 다시 검사합니다. 별도 검사는 Unity 메뉴
**Tools → Shadow Theater → Repair Missing Scripts**에서 실행할 수 있습니다.
