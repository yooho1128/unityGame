# Android APK 0.1.9

- 파일: `Builds/Android/ShadowTheater-v0.1.9-20261018.apk`
- 크기: 137,410,480 bytes (131.0 MiB)
- 앱 ID: `com.yooho.shadowtheater`
- 버전: 0.1.9 / Android version code 10
- ARM64 (`arm64-v8a`), IL2CPP, min API 26, target API 36
- Development/AllowDebugging 테스트용 APK, 기존 0.1.8과 동일한 Android debug 인증서 서명
- SHA-256: `AC486725FDA7AF69CC058BC3D5D1A8EDEDEBF635774B0E0C1A318134CB8CDC25`
- MD5: `d5b77fe8abe2a0f9438f3d7babdb17a4`

Unity 2022.3.62f3의 MobileBuildConfigurator.BuildAndroidQa를 별도 검증 복사본에서 실행했다. Unity 종료 코드 0, Build Report 성공. APK Signature Scheme v2 서명 검증, ZIP 전체 CRC 검사, ARM64 libil2cpp 포함 검사를 통과했다.

소스 변경 및 자동 QA 범위는 [QA 보고서](QA_0.1.9_2026-10-18.md)를 참고한다. 설치 실행 및 기능 확인은 QA 보고서에 기록했다. Google Drive 연결과 QA 텍스트 업로드는 성공했으나, 131 MiB APK는 연결 도구의 100 MiB 제한(HTTP 413)으로 업로드가 거절되어 브라우저 업로드를 시도한다.
