# Unity 배치 검증 — 2026-10-16

- 저장소: `D:\unityGame`, 기준 커밋 `1fed884`.
- 실제 에디터: `C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe` (`96770f904ca7`).
- Unity 2022.3.52f3 설치는 확인되지 않아 해당 버전에서는 실행하지 않았다.
- 기존 파일 3,076개를 외부에 백업했다. 이번 수정 대상 소스·문서 10개를 제외한 3,066개의 SHA-256이 모두 동일했다.
- 원본의 기존 패키지·에디터 버전 변경과 생성 파일은 유지했다. 아래 PASS는 별도 검증 사본에 전체 재생성을 적용한 결과다.

| 실행 | C# 컴파일 | 전체 회귀 | Unity 종료 코드 |
|---|---|---|---|
| 원본의 기존 생성 상태 | PASS | FAIL — 1,263건 | 1 |
| 수정 후 재생성, 기존 로컬 패키지 | PASS | PASS | 0 |
| 수정 후 재생성, main 패키지, Android 대상 | PASS | PASS | 0 |

최종 검사 규모는 지역 63개, 필드 63개, 빌드 씬 64개, 그림자 180종, 포털 146개, 고정 조우 3개다.
한국어 현지화는 2,749/2,749 항목을 통과했다. 저장·전투·성장·스토리 정책과 씬의 직렬화 참조도 기존 전체 검사에 포함된다.
마지막 실행은 기준 커밋의 `Packages/manifest.json`을 사용했으며 URP 14.0.11, Input System 1.7.0, 2D Animation 9.0.3으로 실제 해석된 것을 확인했다.

수정 사항:

- MonoBehaviour 9개를 클래스 이름과 같은 파일로 분리하고 모든 게임 컴포넌트의 Unity 스크립트 연결을 검사한다.
- JSON의 빈 성장 형태가 활성화되는 문제를 차단한다. 이전 로스터의 생성 에셋은 보존하면서 현재 데이터베이스에서 제외한다.
- 메인 41개·개인 기억 6개·소문 7개 퀘스트를 구분하고 동행 이벤트 완료 플래그를 카탈로그에서 확인한다.
- 확장 지역의 도착 트리거를 생성하고 실제 씬의 Reach 목표 연결을 검사한다. 마을 서비스 NPC는 이동 가능한 칸에 배치한다.
- `ShadowTheater.EditorTools.GameRegressionValidator.GenerateAndValidateForCi`로 설정·생성·검증을 실행하고 실패 시 예외로 종료한다.

실행 인수는 `-batchmode -nographics -quit -executeMethod ShadowTheater.EditorTools.GameRegressionValidator.GenerateAndValidateForCi`이며,
마지막 검사에는 `-buildTarget Android`를 추가했다.

기존 경고는 남아 있다. 한국어 전용 설정에 따른 CS0162 경고 4곳, 수작업 아트 교체율 경고 2건,
실기기 QA 리포트 부재 경고 1건이다. APK/AAB·IL2CPP 네이티브 빌드와 실제 기기 플레이 검사는 실행하지 않았다.

백업·검증 사본·원본 로그는 `D:\codex-validation\unityGame-20261016-232237`에 보관했다.
최종 로그는 `regression-committed-packages-android.log`, 기존 파일 보존 결과는 `preservation-check.json`이다.
