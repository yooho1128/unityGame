# 전투 복기 요약과 저장 안정성 — 2026-11-01

필드 메뉴 → **전투 복기** 상단에서 현재 저장의 최근 20전 결과를 한눈에 확인한다.

## 변경

- 승리·각본 기록·패배·도주·중단·진행 중인 전투 수를 표시한다.
- 승률은 `(승리 + 각본 기록) / (승리 + 각본 기록 + 패배)`이다. 도주·중단·진행 중 기록은 제외한다.
- 평균 턴은 도주를 포함한 완료 전투만 집계한다. 집계 대상이 없으면 `—`를 표시한다.
- 긴 기록과 빈 기록으로 전환할 때 스크롤 위치를 초기화한다.
- 복기 파일의 임시 쓰기·교체가 성공한 뒤 캐시를 확정한다. 실패한 완료 처리가 캐시를 오염시켜 같은 기록의 재시도를 중복으로 거부하던 문제를 수정했다.
- 기존 게임 저장 형식·전투 수치·보상은 변경하지 않는다.

## 집중 검증

- 실제 파일 잠금으로 수정 전 문제를 재현했다. 기존 구현은 `failed completion preserves cache and primary` 검사에서 실패한다.
- 수정 후 .NET 집중 검사 250개 통과: 저장 실패 시 캐시/주 파일 보존, 같은 시작·완료의 재시도, 재로드, 슬롯 분리, 백업 복구, 집계 분모·정수 경계를 포함한다.
- 합성 전투 기록 분석 12개와 지역 퍼즐/전투 복기 소스 연결 검사 통과. 합성 수치는 실제 플레이 밸런스 자료가 아니다.
- Unity 2022.3.62f3 C# 컴파일 통과. `SourceOperationBatch.RunForCi`로 12초 Play Mode 기동, Player/Grid/GameFlow 초기화 정상, 런타임 오류 0건, 종료 코드 0을 확인했다.
- 별도 Editor Play Mode 집중 검사 통과: 실제 파일 잠금·실패 후 재시도·JSON 재로드, 빈 기록/결과별 집계, 실제 포인터 hit-test와 복기 열기·이전/다음·닫기, 스크롤 영역/요약 높이, 부모 메뉴의 이동 잠금 유지·해제. 런타임 오류 0건, 종료 코드 0이다.
- 원본 입력 5,541개 중 이번 수정 6개 파일을 제외한 5,535개는 SHA-256이 일치한다. README의 기존 내용도 추가 문구를 제외하고 바이트가 일치한다. 원본 프로젝트에서 Unity를 실행하지 않았다.

main 기준 소스를 복사해 이번 변경만 덮은 검증본을 사용했다. 로컬의 기존 미커밋 수정·생성 파일은 커밋에 포함하지 않았다.
검증본의 에디터/의존성은 설치된 2022.3.62f3와 Unity가 해결한 패키지(URP 14.0.12 등)를 사용했다. 기존 로컬 패키지/버전 변경은 그대로 보존했다.
기존 obsolete API·도달 불가 코드 경고는 남아 있으며, 이 기록은 모든 기능의 수동 플레이나 구버전 Unity 검증을 뜻하지 않는다.

검증 복사본과 증거: `D:\codex-validation\review-qa-20261101`.
재현 로그는 `storage-before.txt`, Unity 로그는 `unity-boot.log`와 `unity-focused.log`,
원본 보존 결과는 `preservation.json`, 집중 검사 결과는 검증본의 `Docs/Generated/BattleReviewQa/WORKFLOWS.txt`에 둔다.

```powershell
dotnet run --project Tools/ReviewPuzzleChecks/ReviewPuzzleChecks.csproj
python -X utf8 Tools/check-review-puzzles.py
python -X utf8 Tools/test-battle-review-summary.py
```

Unity 기동은 별도 검증 복사본에서 `ShadowTheater.EditorTools.SourceOperationBatch.RunForCi`,
복기 파일/UI 집중 검사는 `ShadowTheater.EditorTools.BattleReviewPlayModeQa.RunForCi`를 실행한다.
두 진입점은 자체 종료하므로 `-quit`를 함께 지정하지 않는다.

APK/AAB·Player 빌드, 기기/에뮬레이터 설치, 전체 회귀 검사와 장시간 대전은 수행하지 않았다.
실제 전투 밸런스와 수동 플레이 검수는 별도 범위다.
