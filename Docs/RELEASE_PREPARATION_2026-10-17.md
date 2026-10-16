# 출시 자산 준비 — 2026-10-17

2026-10-16 23:56부터 약 1시간 동안 본편 규모를 늘리지 않고 출시 표현과 검증을 보강했다.
새 지역·그림자 종·퀘스트·스킬은 추가하지 않았다.

| 준비 항목 | 결과 |
|---|---|
| 주요 보스 전용 초상 | 8장 추가, 전체 전용 초상 21/180종 |
| 전투 배경 | 달빛 극장·재의 왕좌·금단 서고·기억의 바다·우주 객석 5장, 막별 자동 선택 |
| 음악 | 13곡의 16마디 편곡 루프, 약 31~53초, mono PCM16 원본·Vorbis 스트리밍 |
| FX·그림자 | 속성/필살기 마스크 10종, 접지 그림자, HP 채움 Sprite 등 12개 공유 이미지 |
| UI 수정 | Sprite가 없어 감소량을 표현하지 못하던 전투·파티 HP 바 수정 |
| Android 자산 설정 | 초상 ETC2 RGBA·최대 1024, 배경 ETC2 RGB·최대 2048, PNG 원본 보존 |
| 실제 UI 미리보기 | 전투 프리팹 1920×1080 렌더링 3장 |

신규 초상은 재의 왕, 무한서고의 용, 밤을 삼킨 짐승, 마지막 인형사, 대검열관 노크스, 익사한 화가,
또 다른 연출가, 달빛의 미망인이다. 이미지 생성에는 내장 image_gen을 사용했다.
전체 이미지 프롬프트와 경로는 [RELEASE_ASSET_MANIFEST.json](RELEASE_ASSET_MANIFEST.json)에 보관했다.
음악은 프로젝트의 `ReleaseMusicComposer`에서 직접 구성한 합성 편곡이며 외부 음원 샘플은 사용하지 않았다.

Final 폴더의 제작 음악·환경음·스킬 효과음·필살기 프리팹은 생성 자산보다 우선 연결한다.
최종 음악과 FX 마스크는 파일이 없는 경우만 생성하므로 후속 교체본을 덮어쓰지 않는다.

검증 결과:

- Unity 2022.3.62f3, main의 패키지 버전으로 Android 대상 C# 컴파일 및 전체 회귀 PASS.
- 63개 지역·63개 필드·64개 빌드 씬·180종 그림자·146개 포털·고정 조우 3개 유지.
- 최종 음악 13곡의 클리핑·무음·DC 편향·반복 경계 검사 PASS.
- 보스 초상 8장의 정사각형·실제 알파·가장자리, 배경 5장의 가로 규격 확인.
- Final 음악의 CoreSystems 참조, 영구 FX Sprite와 HP 바 연결, 전투 배경·접지 그림자 검사 PASS.
- Android QA APK: ARM64·IL2CPP Development Build PASS, 63,503,872 bytes (약 60.6 MiB).

실기기 Android/iOS 20분 QA와 출시 게이트는 아직 수행하지 않았다. 전용 필드 그림자는 13/180종이며,
나머지 초상·필드와 타일은 기존 생성 아트를 사용하므로 추가 아트 검수 대상이다.

원본의 기존 패키지·에디터 버전 변경과 생성 파일을 보존했다. 전체 재생성과 빌드는 별도 검증 사본
`D:\codex-validation\unityGame-20261016-232237\project`에서 수행했다.
이번 작업의 백업과 원본 로그는 `D:\codex-validation\release-20261016-235618`에 있다.

미리보기: [재의 왕](Generated/ReleasePreviews/Battle_Ember.png),
[무한서고의 용](Generated/ReleasePreviews/Battle_Archive.png),
[또 다른 연출가](Generated/ReleasePreviews/Battle_Cosmos.png).
미디어 수치 검사는 [RELEASE_MEDIA_INSPECTION.json](Generated/RELEASE_MEDIA_INSPECTION.json)에 기록했다.

QA APK: `Builds/Android/ShadowTheater-ReleasePrep-QA-20261017.apk` (Git 제외).
SHA-256: `B32F3834C0142701C1D3A843DDBAB96D5AEDF1ACDFB5C3DAFD831AE824158355`
