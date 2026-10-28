# 공연 재미 시스템: 관객 주문 · 연속 흥행 · 주연급 그림자

반복되는 야생 전투를 매번 조금씩 다르게 만들기 위한 세 가지 규칙이다.
그래픽 자산 없이 기존 전투 메시지, 상단 바, 결과 카드만 사용한다.

## 1. 관객 주문 (Audience Request)

전투가 시작되면 객석이 작은 목표 하나를 던진다. 상단 바 `TURN` 아래에 진행 상태가 표시된다.

| 주문 | 조건 | 판정 시점 |
|---|---|---|
| 약점 피날레 | 약점 속성 공격으로 상대를 쓰러뜨림 | 전투 중 |
| 빠른 막내림 | 야생 3턴, 보스 5턴(적 1마리 기준) 안에 종료 | 제한 턴 종료 시 실패 |
| 대역 없는 무대 | 아군 기절 없이 승리 | 기절 시 실패 |
| 앙코르 요청 | 열연 게이지로 앙코르 1회 | 전투 중 |
| 긴장의 순간 | 상대에게 상태 이상 부여 | 전투 중 |
| 결정적 대사 | 치명타 1회 | 전투 중 |
| 다채로운 레퍼토리 | 기본 공격이 아닌 서로 다른 기술 2종 사용(합동기 포함) | 전투 중 |
| 기록의 순간 | 각본 기록 성공 | 기록 시 |

- 파티가 실제로 해낼 수 있는 주문만 뽑는다. 약점 기술, 상태 이상 기술, 2종 이상의 기술이나 기록 가능한 상대가 없으면 해당 주문은 후보에서 빠진다.
- 전투 중 달성하는 주문은 즉시 **FP +1**을 준다.
- 승리 또는 기록으로 끝냈을 때만 보상을 준다. 보상은 **상대 기본 금화의 절반, 최소 15**이고 보스전은 두 배다.
- 누적 10회 달성마다 기억 조각 3개를 준다.
- 첫 전투 튜토리얼(`tutorial_battle_complete` 이전)에는 주문을 내지 않는다.

## 2. 연속 흥행 (Performance Streak)

승리와 기록이 이어질수록 관객이 늘어난다. 도주나 패배가 나오면 0으로 돌아간다.

- 승리 금화 보너스: 2연속부터 회당 +3%, 최대 +30%(11연속)
- 칭호: 3회 흥행 조짐 · 5회 입소문 · 10회 매진 행렬 · 25회 전설의 장기 공연
- 5·10·25·50의 배수 회차에서는 결과 카드에 이정표를 표시한다.
- 연속 기록은 주연급 그림자 출현률도 올린다(아래 참고).
- 도주가 공짜가 아니게 된다. 이 기능의 의도된 긴장감이다.

## 3. 주연급 변주 그림자 (Star Variant)

랜덤 조우에서 낮은 확률로 강화된 개체가 등장한다. 이름 앞에 `★`이 붙는다.

- 출현률: 기본 1/64(약 1.6%). 연속 흥행 1회당 +10%, 최대 2배(1/32)
- 기록할 수 있는 그림자만 대상이다. 등장 전에 전용 전조 메시지가 1.1초 표시되고 그동안 이동이 잠긴다.
- 전 능력치 +10%. 기록하면 그 개체가 그대로 동료가 된다.
- 이미 기록한 종이어도 기억 조각으로 바꾸지 않고 파티나 서고에 합류시킨다.
- 쓰러뜨리면 경험치 ×1.5, 금화 ×2, 드롭 도구가 확정이다.

## 코드 위치

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Battle/AudienceRequest.cs` | 주문 선택·진행·판정·보상·문구 (순수 로직) |
| `Assets/Scripts/Battle/ShowcasePolicies.cs` | `PerformanceStreakPolicy`, `StarVariantPolicy` 수치 |
| `Assets/Scripts/Battle/BattleManager.cs` | 전투 사건을 주문에 전달, 종료 시 `ConcludeShowcase` |
| `Assets/Scripts/Field/RandomEncounterController.cs` | 주연급 판정, 전조 동안 이동 잠금 |
| `Assets/Scripts/Field/GameFlowController.cs` | 연속 흥행·누적 기록 저장 (`ApplyShowcaseRecords`) |
| `Assets/Scripts/UI/BattleUIController.cs` | 상단 바 주문 표시, 결과 카드 요약 줄 |
| `Assets/Editor/ShowcaseRegression.cs` | `GameRegressionValidator` 회귀 검사 |
| `Prototype/shadow-theater.html` | 같은 규칙의 브라우저 체험판 (`?star`로 주연급 조우 강제) |

## 저장 데이터 (v12 → v13)

`SaveData`에 `performanceStreak`, `bestPerformanceStreak`, `audienceRequestsCompleted`, `starVariantsRecorded`를 추가했다.
`ShadowInstance`에는 `starVariant`를 추가했다. 모두 기본값 0/false로 추가되는 필드다.
구버전 저장은 변환 없이 그대로 읽히고 `Sanitize`가 음수를 정리한다.

## 함께 반영한 리뷰 제안

- **C-2 희귀 조우 전조 중 입력 잠금**: 희귀·주연급 전조 동안 플레이어를 잠근다.
  `CanStartEncounter`가 잠긴 플레이어를 거부하므로, 전투 시작 직전 같은 프레임에 잠금을 풀고 `BattleFlow`가 다시 잠근다.

## 기존 컴파일 오류 수정 (별도 커밋)

`Assets/Scripts`를 UnityEngine 2021.3 참조 어셈블리로 Roslyn 컴파일하는 과정에서 기존 오류 7건을 발견해 수정했다.

- `BattleManager`에 `using ShadowTheater.Field` 누락
- 바깥 범위 이름과 겹치는 지역 변수(CS0136): `PixelCharacterMotion`, `PlayerController`, `SaveManager`, `TownNpcPatrol`
- `CompanionAmbientCommentary`: `out _`가 `Vector2Int _` 매개변수에 바인딩됨

## 검증 범위

- 실행한 검사
  - `Assets/Scripts` 전체 Roslyn 컴파일 0 오류
    - 참조 DLL 한계 2건은 검사 복사본에서만 우회: uGUI 2018의 `selectedColor`, Android 전용 `Handheld`
  - 정책 로직 34개 단위 검사 통과(.NET 실행)
  - 프로토타입 Chromium 자동 전투 6회와 데스크톱·모바일 화면 확인, JS 오류 없음
  - 기존 `Tools/` 검사 통과
- **미실행:** Unity 에디터 컴파일, `RunForCi`, Play Mode, 실기기, 경제 밸런스 장기 검증
- 이 환경에는 Unity가 없다. 다음 Unity 실행 때 `ShowcaseRegression`이 감사에 포함된다.
