# Android APK 0.1.8

- 파일: `Builds/Android/ShadowTheater-v0.1.8-20261018.apk`
- 크기: 137,399,528 bytes (131.0 MiB)
- 앱 ID: `com.yooho.shadowtheater`
- 버전: 0.1.8 / Android version code 9
- ARM64 (`arm64-v8a`), IL2CPP, 최소 Android API 26, target API 36
- Development/AllowDebugging 테스트용 APK, Android debug 키 서명
- 기존 0.1.7 APK와 앱 ID 및 서명 인증서 SHA-256 일치
- APK SHA-256: `5D099D6FFA92AA2BB8D07CC998DEFA8F77A9BAB9F550B67DF97AFCDA48EF9829`

## 빌드 및 확인

- 현재 게임 소스 e029659와 버전 설정 변경을 별도 검증 복사본에 반영했다. 추적 중인 C# 소스와 데이터 카탈로그 202개를 비교했으며, 이전 검증 이후 달라진 파일은 버전 설정 파일 1개였다.
- Unity 2022.3.62f3의 `ShadowTheater.EditorTools.MobileBuildConfigurator.BuildAndroidQa` 사용. Title 및 63개 필드 씬을 포함하는 기존 빌드 설정을 사용했다.
- Unity 종료 코드 0, Build Report 성공. 빌드 소요 시간 9분 38초.
- `aapt dump badging`: 패키지/버전/SDK/ARM64 확인.
- `apksigner verify --verbose --print-certs`: APK Signature Scheme v2 검증 성공.
- APK ZIP CRC 검사 및 `lib/arm64-v8a/libil2cpp.so` 포함 확인.
- 기기·에뮬레이터 설치, 실제 플레이 및 전체 회귀 검사는 실행하지 않았다.
- 원본의 기존 패키지/에디터 버전 변경과 미추적 생성 파일은 보존했다. 빌드 결과물은 Git에서 제외된 로컬 Builds 폴더에 보관한다.
