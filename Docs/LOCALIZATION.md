# 한국어·영문 현지화

설정의 `언어` 버튼은 `GameSettings.Language`를 변경하며, `LocalizationRuntime`이 열린 화면의 정적 UI,
동적 컨트롤러와 폰트를 같은 프레임에 갱신한다. 언어 값은 세이브 슬롯과 별개로 `PlayerPrefs`에 유지된다.

## 포함 범위

- 타이틀, 설정, 필드 버튼, 전투 명령과 결과 메시지
- 대화 화자·본문·선택지용 안정적인 ID 키
- 퀘스트 제목·목표·보상
- 파티와 각본 서고, 각본집 도감, 속성·역할·희귀도
- 40개 지역 이름과 월드맵 상태·빠른 이동 UI
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
노출되지 않는다. 새 번역은 코드 수정 없이 카탈로그 항목만 추가하면 된다.

## 전체 누락 검사

Unity 메뉴 `Tools > Shadow Theater > Localization > Validate Full Coverage`는 다음 소스에서 실제 필요한
키를 자동으로 수집한다.

- `Assets/Scripts`의 `L10n.Get`/`L10n.Format` 정적 키와 전투 튜토리얼 동적 키
- `Assets/Scripts`의 `L10n.Text("고정 문구")` 호출
- 전체 대화의 화자·본문·선택지
- 47개 퀘스트의 제목·요약·목표
- 40개 지역과 4개 엔딩 텍스트
- 생성된 180종 그림자, 스킬, 도구와 기억 성장 형태

키 누락, 빈 한국어/영문, `{0}` 같은 서식 변수의 한영 불일치와 중복 키를 실패로 처리한다. 상세 보고서는
`Docs/Generated/LOCALIZATION_COVERAGE.md`, 번역 작업용 탭 구분 파일은
`Docs/Generated/LOCALIZATION_MISSING.tsv`에 생성된다. 이 검사는 전체 회귀 검증과 모바일 출시 게이트에도
포함되므로 미번역 상태로 Release 검증을 통과할 수 없다.
카탈로그의 null 항목·빈 키와 동일 한국어/영문 원문이 서로 다른 번역을 가리키는 모호성도 실패 처리한다.

TSV의 `en` 열을 채운 뒤 `Localization > Import Completed Translation TSV`로 다시 가져온다.
빈 번역 행은 건너뛰고 전체 파일의 중복 키·열 수·서식 변수를 검사한 뒤 반영한다. 줄바꿈 표시 ` ↵ `는
실제 줄바꿈으로 복원되며, 기존 JSON은 `Docs/Generated/LocalizationBackup_날짜.json`에 백업된다.
가져오기 후 커버리지 검사를 다시 실행한다.

## 폰트 폴백

한국어는 `Malgun Gothic`, `Apple SD Gothic Neo`, `Noto Sans CJK KR`, `Noto Sans KR` 순서로 찾고,
영문은 `Arial`, `Roboto`, `Liberation Sans` 순서로 찾는다. 설치된 글꼴이 없으면 Unity의
`LegacyRuntime.ttf`를 사용한다. `Generate Title and Starter UI`를 다시 실행하면 CoreSystems 프리팹에
`LocalizationRuntime`이 자동 포함된다.
