# 관객 의뢰·전투 UI 빌드 v0.1.18 — 2026-11-01

사용자 요청으로 Windows 64-bit와 Android QA APK 빌드를 완료했다. 두 빌드 종료 코드는 0이다.
입력은 main `3221bb6`에 기존 로컬 수정과 이번 아이템 알림 오류 수정을 더한 작업본이다.
앱 버전 `0.1.18`, Android versionCode `19`는 빌드 복사본에서만 설정했고 원본 버전 설정을 보존했다.

## 변경과 집중 확인

- 관객 의뢰 3택1·전투 UI·전투 복기 요약을 포함한다. 콘텐츠·프리팹·64개 씬을 새로 생성했다.
- Unity 2022.3.62f3 C# 컴파일 및 12초 Editor Play Mode 기동 통과. Player/Grid/GameFlow 초기화 정상, 런타임 오류 0건이다.
- 새로 생성한 전투 프리팹에서 실제 의뢰 선택·변경 잠금·결과 확인·필드 복귀와 16:9/4:3 렌더링을 확인했다.
- 전투 보상 정산이 비활성 Player에서 획득 알림 코루틴을 시작하던 오류를 수정했다. 알림을 큐에 보관하고 필드 활성화 후 표시하며, 비활성화된 코루틴 핸들을 해제한다. 빈 큐는 시작하지 않는다.
- 전투 중 필드 전용 아이템을 추가해 대기 큐·코루틴 미시작·필드 복귀 후 표시 재개를 확인했다. 의뢰/전투 집중 검사 런타임 오류 0건, 종료 코드 0이다.
- 실제 상점 구매/판매/수량 확정/취소·금화 부족 차단과 첫 마을 왼쪽 포탈 왕복을 재확인했다. 런타임 오류 0건, 종료 코드 0이다.
- Windows 빌드 종료 코드 0. 배포 파일 175개의 빌드 원본/복사본 SHA-256, AMD64 PE, 새 기능·알림 수정의 런타임 어셈블리 포함과 ZIP CRC를 확인했다.
- Android QA APK 빌드 종료 코드 0. 앱 ID `com.yooho.shadowtheater`, 버전/코드 `0.1.18/19`, 최소 API 26, ARM64 IL2CPP ELF, 새 기능·알림 수정의 메타데이터 포함, ZIP CRC와 v2 서명을 확인했다. v0.1.17과 서명 인증서 SHA-256이 같다.

## 결과물

- Windows ZIP: `D:\unityGame\Builds\Windows\ShadowTheater-v0.1.18-20261101.zip`
  - 크기: 158,971,283바이트
  - SHA-256: `8A7C22975C06B640036A7690C6C9C2C02E29770144A19246E71D46A15E0A0B8F`
- Windows EXE: `D:\unityGame\Builds\Windows\ShadowTheater-v0.1.18-20261101\ShadowTheater.exe`
- Android APK: `D:\unityGame\Builds\Android\ShadowTheater-v0.1.18-20261101.apk`
  - 크기: 175,698,984바이트
  - SHA-256: `329F766858C26D5D6D5EA9DB89936850E195A80795E292553065032B616BCFEF`

Windows는 ZIP 전체를 풀어 같은 폴더의 EXE를 실행한다. APK는 이전 서명을 유지한 업데이트용 패키지다.

## 보존과 범위

- 빌드 프로젝트·증거: `D:\codex-validation\build-0.1.18-20261101`.
- 기존 원본 입력 5,551개 중 이번 코드/QA 수정 2개를 제외한 5,549개를 해시로 확인했다. 게임·에디터 C# 286개는 빌드 복사본과 일치하며 빌드용 버전 상수만 다르다.
- 원본에서 Unity 임포트·생성·빌드를 실행하지 않았다. 기존 미커밋 변경과 미추적 생성 파일·이전 배포본을 보존했다.
- 전체 회귀 검사·장시간 대전·iOS/AAB·기기/에뮬레이터 설치는 수행하지 않았다. 완성 Player의 수동 플레이는 별도 범위다.
- `unity-boot.log`, `unity-audience.log`, `unity-shop.log`, `unity-windows-build.log`, `unity-android-build.log`, 각 종료 코드, `windows-verification.json`, `apk-verification.json`, `source-preservation.json`을 증거로 남긴다.
