# 그래픽·음악·스킬 배포 v0.1.19 — 2026-11-02

Windows 64-bit와 Android QA APK 빌드를 완료했다. 두 빌드 종료 코드는 0이다.
게임 소스는 main `7700228a8c5f7659d9d722bb61c63225e65fe04b`이며, 기존 로컬의 다른 미커밋 변경을 섞지 않고 Git 인덱스에서 별도 빌드 복사본으로 추출했다.
버전은 `0.1.19`, Android versionCode는 `20`이다.

## 최종 스킬 범위

- 기존 목록 199개에 더해, 전체 생성 과정에서 지역 전용기 36개와 자동 진명 궁극기 26개를 발견했다. 추가 62개에도 개별 연출과 5개 그래픽 부품 역할을 지정했다.
- 최종 생성 스킬 **261개 전부**에 연출이 연결됐고 누락은 0개다. 원본 투명 PNG 4장의 서로 다른 그래픽 부품 128개를 사용한다.
- 전 스킬을 이름 표시 없이 같은 색상으로 각각 세 프레임 렌더링한 결과, 261개 시퀀스가 모두 구별됐다. 부품 128개 전부의 실제 그리기 호출을 확인했다.
- 시전·명중 효과음 522개가 유효 범위이며 무음이 아님을 확인했다. 대표 27개 연출 비교에서 명중 콜백 중복과 캐릭터 위치 복원도 검사했다.
- 위 검사는 생성 데이터와 렌더링 검사다. 이전 수동 검사는 [대표 스킬 20개 보고서](MANUAL_SKILL_PLAY_2026-11-02.md)에 기록했다.

## 컴파일과 빌드 검증

- Unity 2022.3.62f3에서 콘텐츠·프리팹·64개 씬을 재생성했다. 최종 음악 13곡 연결과 그래픽 부품 128개 임포트를 확인했다.
- C# 컴파일과 `SourceOperationBatch.RunForCi` 기동을 통과했다. PrologueTheater의 Player/Grid/GameFlow 초기화 정상, Play Mode 12초, 런타임 오류 0건이다.
- 커밋된 C# 및 데이터 카탈로그 323개와 빌드 복사본의 대응 입력이 일치한다. 임시 빌드 도구는 복사본의 Editor 폴더에만 두었다.
- Windows: AMD64 Mono, 배포 파일 175개 해시 일치, 새 연출 코드 포함, ZIP CRC 통과.
- Android: ARM64 IL2CPP, 최소 API 26, 앱 ID `com.yooho.shadowtheater`, 버전/코드 `0.1.19/20`, 새 연출 코드 포함, ZIP CRC·v2 서명 통과. v0.1.18과 서명 인증서가 같다.
- APK 설치, Player 수동 플레이, 전체 회귀 검사와 장시간 전투 시뮬레이션은 이번 배포 검증 범위에 포함하지 않았다. 실제 청음·모바일 터치·성능은 별도 확인이 필요하다.

## 결과물

- Windows ZIP: `D:\unityGame\Builds\Windows\ShadowTheater-v0.1.19-20261102.zip`
  - 161,725,871바이트 / SHA-256 `6839297F284D5C1F3AEF4BE9305BB6FCE312C4B78B222B38FC1EDD581C95DF17`
- Windows EXE: `D:\unityGame\Builds\Windows\ShadowTheater-v0.1.19-20261102\ShadowTheater.exe`
- Android APK: `D:\unityGame\Builds\Android\ShadowTheater-v0.1.19-20261102.apk`
  - 176,034,560바이트 / SHA-256 `36B0FBBCC392BD449C95137BA305524913374D8FF239C8DCEAC2CF38EBCDE025`

Windows는 ZIP 전체를 풀고 EXE를 실행한다. 각 배포 파일 옆에 SHA-256 파일도 보관했다.

## 보존과 증거

- 원본 입력 5,603개를 보존했고, 이번 소유 파일의 의도된 수정 19개 외 변경은 없다. 원본에 Unity 생성 입력이 추가되지 않았다.
- 이번 작업 밖의 Git 상태 2,824개와 이전 배포 파일 23개의 해시를 보존했다.
- [261개 렌더링·음향 검사](Generated/SkillParts128/full-build-checks.txt), [추가 생성 스킬 연출](Generated/SkillParts128/generated-skill-sequences.png), [에디터 기동](Generated/SkillParts128/build-source-operation.txt).
- [Windows 검증](Generated/Build019/windows-verification.json), [APK 검증](Generated/Build019/apk-verification.json), [원본 보존](Generated/Build019/source-preservation.json), [이전 배포 보존](Generated/Build019/previous-releases-preserved.json).
- 빌드 복사본·전체 로그·실행 도구: `D:\codex-validation\main-presentation-build-20261102`.
