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


## Drive 전달 상태

Google Drive 계정 연결과 QA 텍스트 업로드를 확인했다. APK 업로드는 연결 도구의 104,857,600-byte 제한으로 HTTP 413이 발생했다. 원본 APK 137,410,480 bytes는 보존했다. 별도 검증 복사본에서 다른 기본 패키징 옵션으로 다시 빌드한 결과도 137,474,963 bytes로 제한을 넘었다. native 라이브러리의 debug-section 제거 크기 검사에서도 제거할 debug-section이 없었다. 초상화 아틀라스 Android ETC2_RGBA8 설정은 이미 존재하므로 게임 아트의 해상도/품질을 임의로 낮추지 않았다.

브라우저 업로드는 현재 URL 확인 실패로 컴퓨터 사용 도구가 중단했다. 이 경로는 사용자가 Drive 폴더를 직접 연 뒤 재확인이 필요하다. APK의 Google Drive 업로드는 아직 완료되지 않았다.
