# 한국어 전용 출시와 현지화 구조

현재 출시 언어는 한국어다. `GameSettings.EnglishEnabled`가 `false`이므로 기존 English 저장값도 한국어로
정규화되고 설정 화면의 언어 행은 숨겨진다. `LocalizationRuntime`과 영문 카탈로그 구조는 향후 해외 출시를
다시 검토할 때 사용할 수 있도록 보존한다.

## 포함 범위

- 타이틀, 설정, 필드 버튼, 전투 명령과 결과 메시지
- 대화 화자·본문·선택지용 안정적인 ID 키
- 퀘스트 제목·목표·보상
- 파티와 각본 서고, 각본집 도감, 속성·역할·희귀도
- 63개 지역 이름과 월드맵 상태·빠른 이동 UI
- 네 종류 엔딩의 제목·부제와 엔딩 기록관
- 프롤로그 첫 안내 대사의 영문 기준 번역

## 카탈로그

`Assets/Resources/Data/LocalizationCatalog.json`의 각 항목은 다음 형식이다.

```json
{"key":"map.travel","ko":"빠른 이동","en":"Fast Travel"}
```

정적 UI는 기존 한국어 문구로도 항목을 찾을 수 있다. 동적 콘텐츠는 아래처럼 저장 ID를 키에 포함한다.

- 대화: `dialogue.{dialogueId}.{lineIndex}.text`
- 화자: `dialogue.{dialogueId}.{lineIndex}.speaker`
- 선택지: `dialogue.{dialogueId}.choice.{choiceIndex}`
- 퀘스트: `quest.{questId}.title`, `quest.{questId}.summary`, `quest.{questId}.{objectiveId}`
- 지역: `region.{regionId}.name`, `.act`, `.environment`, `.summary`
- 엔딩: `ending.{endingId}.title`, `.subtitle`, `.archive`

등록되지 않은 키는 한국어 원문을 표시하므로 신규 콘텐츠를 추가해도 빈 문자열이나 키 자체가 화면에
노출되지 않는다. 향후 영문을 다시 활성화하면 코드 수정 없이 카탈로그 항목만 추가하면 된다.

## 전체 누락 검사

Unity 메뉴 `Tools > Shadow Theater > Localization > Validate Full Coverage`는 다음 소스에서 실제 필요한
키를 자동으로 수집한다.

- `Assets/Scripts`의 `L10n.Get`/`L10n.Format` 정적 키와 전투 튜토리얼 동적 키
- `Assets/Scripts`의 `L10n.Text("고정 문구")` 호출
- 에디터 프리팹 생성기의 고정 `Text.text`와 `CreateButton` 라벨
- 전체 대화의 화자·본문·선택지
- 47개 퀘스트의 제목·요약·목표
- 63개 지역과 4개 엔딩 텍스트
- 생성된 180종 그림자, 스킬, 도구와 기억 성장 형태

현재 출시 검증은 한국어 원문·복합 서식과 데이터 소스 무결성을 필수로 처리한다. `L10n.Get` 호출처럼
한국어 폴백 원문이 있는 항목은 별도 카탈로그 키가 없어도 유효하며, 카탈로그 자체의 빈 키·중복 키는 실패한다. 영문 누락은 보고서의
`영문 보류` 수치로 남지만 출시 실패로 처리하지 않는다. 상세 보고서는
`Docs/Generated/LOCALIZATION_COVERAGE.md`, 번역 작업용 탭 구분 파일은
`Docs/Generated/LOCALIZATION_MISSING.tsv`에 생성된다. 이 검사는 전체 회귀 검증과 모바일 출시 게이트에도
포함되므로 한국어 원문이나 필수 데이터가 빠진 상태로 Release 검증을 통과할 수 없다.
카탈로그의 null 항목·빈 키와 중복 키도 실패 처리한다.
대화·퀘스트·지역·엔딩·현지화 JSON이 없거나 파싱되지 않으면 데이터 소스 오류로 실패 처리한다.
짝이 맞지 않는 중괄호 같은 잘못된 복합 서식도 검사한다. 런타임에서 오류가 발견되면 한국어 폴백으로
한 번 더 서식을 시도하고, 폴백도 잘못된 경우 원문을 표시해 게임 진행 중 예외를 막는다.

향후 영문 출시를 재개할 때 TSV의 `en` 열을 채운 뒤 `Localization > Import Completed Translation TSV`로 가져온다.
빈 번역 행은 건너뛰고 전체 파일의 중복 키·열 수·서식 변수를 검사한 뒤 반영한다. 줄바꿈 표시 ` ↵ `는
실제 줄바꿈으로 복원되며, 기존 JSON은 `Docs/Generated/LocalizationBackup_날짜.json`에 백업된다.
가져오기 후 커버리지 검사를 다시 실행한다.

## 폰트 폴백

한국어는 `Malgun Gothic`, `Apple SD Gothic Neo`, `Noto Sans CJK KR`, `Noto Sans KR` 순서로 찾는다.
보존된 영문 모드를 다시 활성화하면 `Arial`, `Roboto`, `Liberation Sans` 순서로 찾는다. 설치된 글꼴이 없으면 Unity의
`LegacyRuntime.ttf`를 사용한다. `Generate Title and Starter UI`를 다시 실행하면 CoreSystems 프리팹에
`LocalizationRuntime`이 자동 포함된다.
