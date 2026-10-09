# 모바일 빌드·현지화·실기기 출시 검증

## 권장 실행 순서

1. Unity 메뉴 `Tools > Shadow Theater > Generate and Validate Full Game`을 실행한다.
2. `Tools > Shadow Theater > Mobile > Configure Android and iOS`로 양 플랫폼 설정을 고정한다.
3. `Localization > Validate Full Coverage`가 PASS인지 확인한다.
4. `Mobile > Build Android QA APK`와 `Mobile > Build iOS QA Xcode`로 Development Build를 만든다.
5. Android와 iPhone에서 각각 20분 이상 아래 실기기 시나리오를 수행한다.
6. 기기가 만든 `device_validation_latest.json`을 각각 `Mobile > Import Device QA Report`로 가져온다.
7. `Mobile > Validate Release Gate`가 PASS일 때만 Release AAB/Xcode 빌드를 만든다.

Unity Hub에서 Android Build Support(Android SDK/NDK/OpenJDK 포함)를 설치해야 한다. iOS Xcode 빌드와
서명·업로드는 macOS와 Xcode가 필요하다. Android 업로드 키스토어와 비밀번호, Apple 인증서와
Provisioning Profile은 저장소에 커밋하지 않는다.

## 고정되는 모바일 설정

`Configure Android and iOS`는 앱 ID `com.yooho.shadowtheater`, 버전 `0.1.0`, 가로 회전, Linear 색 공간,
IL2CPP와 Medium Managed Stripping을 적용한다. Android는 API 26 이상·ARM64, iOS는 13.0 이상이다.
QA 메뉴는 Android APK 또는 iOS Development Xcode 프로젝트를, Release 메뉴는 Android AAB 또는 iOS
Release Xcode 프로젝트를 `Builds/`에 만든다. Android Release는 커스텀 키스토어가 없으면 즉시 중단한다.

## 자동 검증

Unity 메뉴 `Tools > Shadow Theater > Generate and Validate Full Game`을 실행한다. 생성 완료 후 다음 항목을
한 번에 검사한다.

- 지역 카탈로그가 정확히 40개이며 ID·씬 이름·진행 순서가 중복되지 않는지
- 최초 지역에서 연결 그래프를 따라 40개 지역 모두 도달 가능한지
- 지역 확장 로스터 97종과 최종 ShadowDatabase 180종이 중복 없이 생성됐는지
- 40개 필드마다 FieldGrid, Player, GameFlow, 포털이 있는지
- 모든 포털의 대상 씬이 실제 40개 지역 중 하나인지
- 40개 씬의 바닥/충돌 Tilemap 참조, 월드맵 및 모든 포털 도착 좌표가 실제 이동 가능한 타일인지
- 심볼 인카운터에 ShadowData가 연결됐는지
- Missing Script가 남아 있지 않은지
- Title과 40개 필드가 Build Settings에서 활성화됐는지
- v1 형식의 손상·중복 샘플이 현재 세이브 버전으로 정상 변환되는지
- Android/iOS 앱 ID·화면 방향·ARM64·IL2CPP 설정이 일치하는지
- 코드·대화·퀘스트·40개 지역·엔딩·그림자·스킬·도구의 한국어/영문이 모두 존재하는지

CI에서는 Unity batchmode에 아래 메서드를 지정하면 오류가 하나라도 있을 때 빌드 실패로 종료된다.

`ShadowTheater.EditorTools.GameRegressionValidator.ValidateForCi`

## 세이브 v7

- `saveGuid`로 세이브 정체성을 유지한다.
- 예전 `Prologue` 맵 ID를 `PrologueTheater`와 시작 좌표 `(0,-5)`로 교정한다.
- 파티가 6명을 넘으면 초과 개체를 각본 서고로 안전하게 이동한다.
- 비어 있거나 중복된 개체 ID는 새 GUID로 복구한다.
- 음수 골드·레벨·경험치, 중복 도감 ID·아이템·플래그를 정규화한다.
- 현재 앱보다 새로운 버전의 세이브는 덮어쓰지 않고 로드를 거부한다.
- 주 파일을 읽지 못하면 `.bak`을 복구하고 손상본은 `.corrupt_yyyyMMddHHmmss`로 남긴다.
- 현재 `ShadowDatabase`에 없는 그림자 개체는 로드 전에 제거하고, 빈 파티는 서고·스타터·기본 기사
  순서로 복구해 능력치 계산 예외와 이어하기 불가를 막는다.
- 저장 좌표나 체크포인트가 맵 수정 뒤 벽·절벽·물·바닥 밖이 되면 같은 맵의 가장 가까운 이동 가능 칸으로
  교정하고 즉시 다시 저장한다.
- 전투 시작 직전에 일관된 체크포인트를 저장하고, 전투 중 앱 중단 저장은 건너뛰어 HP·소모품·보상이
  절반만 반영되는 상태를 방지한다.

## 필드 UI 상호 배제

각본집, 파티·각본 서고, 월드맵, 상점, 도구 가방, 기억 여정, 필드 메뉴, 대화는 한 번에 하나만 열린다.
모든 진입점은 `FieldUiModalState`를 사용하며 전투·맵 전환 중에는 새 화면을 열 수 없다. 실제 기기에서는
서로 다른 상단 버튼을 빠르게 연속 탭한 뒤 화면 중첩과 이동 잠금 잔류가 없는지 확인한다.
필드 메뉴의 `안전 위치 복귀`도 같은 지역 체크포인트 이동과 다른 지역 거점 이동을 각각 시험한다.

## 모바일 성능 프로필

| 항목 | 일반 기기 | 저메모리 기기 |
|---|---:|---:|
| 목표 FPS | 60 | 30 |
| 픽셀 라이트 | 2 | 1 |
| MSAA / 실시간 그림자 / 반사 프로브 | 비활성 | 비활성 |
| 미사용 리소스 회수 | 씬 이동 5회마다 | 씬 이동 5회마다 |
| OS 저메모리 알림 | 즉시 저장 후 리소스·관리 힙 회수 | 동일 |

저메모리 판정 기준은 시스템 RAM 3GB 이하 또는 그래픽 메모리 1GB 이하이며 CoreSystems 프리팹의
`MobilePerformanceController`에서 조절할 수 있다.

## 실기기 자동 기록과 합격 기준

QA 빌드의 `CoreSystems/DeviceValidationRecorder`는 30초마다, 앱 일시 정지와 종료 때 아래 파일을 갱신한다.

`Application.persistentDataPath/device_validation_latest.json`

Android에서는 Android Studio Device Explorer의
`/storage/emulated/0/Android/data/com.yooho.shadowtheater/files/`에서 복사한다. iOS에서는 Xcode의
Devices and Simulators에서 앱 컨테이너를 내려받아 `Documents` 안의 파일을 복사한다. 가져온 원본은
`Docs/DeviceValidationReports/`에 보관되고 요약은 `Docs/Generated/DEVICE_VALIDATION_SUMMARY.md`로 생성된다.
같은 플랫폼에서 여러 번 시험한 경우 가장 최근에 생성된 리포트만 출시 판정에 사용하며, 현재 앱 버전과
리포트의 앱 버전이 다르면 실패한다.
리포트 v2는 Unity 빌드 GUID를 포함하며, GUID가 없는 예전 리포트나 식별 불가능한 빌드는 가져오지 않는다.
리포트 폴더에 파싱할 수 없거나 지원하지 않는 버전의 JSON이 하나라도 있으면 출시 게이트가 실패하며,
해당 경로가 요약 보고서에 `INVALID`로 표시된다.

플랫폼별 PASS 조건은 다음과 같다.

가져오기 시 JSON의 `passed` 값 대신 실제 수치를 공통 정책으로 재판정한다. NaN/Infinity 성능 수치와
기기 모델·생성 시각 누락도 차단한다. 전체 회귀 검증에는 최소 기준 통과, 잘못된 FPS 차단, 오류 로그 차단,
백그라운드 복귀 누락 차단 샘플이 포함된다.

- 20분 이상 실행, 씬 로드 5회 이상 및 서로 다른 씬 3개 이상 방문
- 전투 완료 3회 이상, 저장 성공 3회 이상
- 홈 화면으로 전환 후 복귀를 각각 1회 이상 기록
- 평균 FPS 25 이상, 1초 구간 최저 FPS 10 이상
- 저메모리 콜백 0회, Error/Exception/Assert 로그 0건

`Validate Release Gate`는 기존 전체 회귀 검증과 현지화 전수 검사에 더해, PASS한 Android 리포트와
PASS한 iOS 리포트가 모두 있을 때만 통과한다. 일반 `Validate Full Game`은 리포트가 없으면 경고만 내므로
개발 중 콘텐츠 검사에는 사용할 수 있다.
두 Release 빌드 메뉴도 이 게이트를 내부에서 다시 실행하며, 실패 항목이 있으면 빌드를 시작하지 않는다.

## 실제 기기 최종 확인

자동 검증 이후 Android 저사양/중간급 기기와 iPhone 1종에서 각각 20분 이상 아래를 확인한다.

1. 타이틀부터 서로 다른 막의 지역을 연속 이동해 프레임 저하와 메모리 종료 여부 확인
2. 전투 배속 1×/2×/3×, 필살기, 그림자 교체와 기록을 반복
3. 홈 화면 전환 후 복귀하여 자동 저장과 현재 위치 복원 확인
4. 파티 6명과 각본 서고 다수 보유 상태에서 UI 스크롤 확인
5. 한국어/English 전환 후 잘림, 폰트 누락, 노치·홈 인디케이터 침범 확인
6. 전투 도중 앱을 백그라운드에서 종료한 뒤 재실행하여 전투 직전 필드·파티·인벤토리로 복구되는지 확인
7. 기존 저장이 있는 상태에서 새 게임 취소·확인을 각각 시험하고, 손상/최신 버전 저장 안내 문구를 확인

위 동작 중 씬 이동·전투·저장·백그라운드 복귀와 기술 오류는 자동 리포트에 포함된다. UI 잘림, 터치 영역,
음량과 체감 발열처럼 자동 판정할 수 없는 항목은 체크리스트로 함께 확인한다.
