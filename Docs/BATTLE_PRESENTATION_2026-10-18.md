# 공격 연출 개선

## 변경 이유와 동작

이전 `BattleUnitPanel.MotionRoot`는 체력/이름/배경까지 포함한 유닛 패널이었다. 일반 공격 시 패널 전체가 이동해서 네모 카드끼리 부딪히는 모습이었다.

이제 런타임에서 초상 이미지에만 `ActorMotion` 부모를 만들고 모든 기존 공격/필살기 동작은 이 부모를 움직인다. 체력, 이름, 상태 및 카드 배경은 제자리에 남는다. 기존 씬과 프리팹도 코드 변경만으로 적용된다.

일반 공격은 0.64초(전투 재생 속도 적용)의 준비, 공격, 피격, 복귀 단계로 나눴다.

- 물리: 짧은 뒤로 당김과 전진, 대상 위치의 베기, 충격 링 및 파편.
- 마법: 시전 준비 후 속성 투사체가 공격자로부터 대상까지 이동하고 도착 시 폭발.
- 빗나감: 대상 회피, 명중 이펙트/피격음/타격 정지 생략.
- 명중: 투사체 도착/베기 시점에 피격음, 짧은 타격 정지, 대상 반동과 피격 색상.
- 완료 또는 시퀀스 Dispose 시 두 캐릭터 위치/스케일/회전/색 복구.

기존 속성 FX 스프라이트를 재사용하며 모든 이펙트는 UI 입력을 가로채지 않는다. 전투 수치, 명중 판정, 스킬 비용과 턴 규칙은 변경하지 않았다. 종별 원화에 움직임을 주는 방식이며 180종의 프레임별 공격 애니메이션을 새로 제작한 것은 아니다.

## 검증

`MobilePlayabilityChecks.CheckAttackPresentation`은 폴드 2184×1968, 바형 2400×1080, PC 1920×1080에서 양 진영의 물리/마법, 명중/빗나감을 검사한다. 준비/이동/명중/복귀의 7개 시점에서 패널과 모든 텍스트 위치 고정, 투사체와 명중 효과 시점, 스프라이트 및 raycast 설정을 확인한다. 초상을 분리하기 전후의 위치 보존과 실제 초상 중심에 이펙트가 맞는지도 검사한다. 피격 도중 Dispose해서 캐릭터 복귀도 확인한다.

초상 대기 동작은 localPosition 대신 anchoredPosition 기준으로 적용해 화면 비율이 바뀌어도 앵커 배치가 유지된다.

검증 결과:

- 설치된 Unity 2022.3.62f3 배치 컴파일 및 Android 빌드 종료 코드 0.
- 전체 회귀: 63개 지역/필드, 180종, 146개 포털, 3개 인카운터 PASS.
- 화면 비율 3종의 양 진영 × 물리/마법 × 명중/빗나감, 총 168개 동작 시점 검사 PASS. 분리 전후 초상 위치 및 효과 중심 검사 PASS.
- 기존 일반/강제 교체 입력, 6명 목록 스크롤, 각본집, 필드 크기 검사 PASS.
- 초반 자동 대전 1,913/1,920 승으로 기존 결과 유지.
- Android 에뮬레이터에 업데이트 설치/실행 성공. 설치 전후 저장 JSON SHA256 일치. 새 프로세스 실행 로그에 NullReference/MissingReference/치명적 예외 없음.
- 사용자가 수정해 둔 Packages/manifest.json, ProjectSettings/ProjectVersion.txt 보존. 기존 미추적 파일 2,776개의 SHA256 모두 일치.

실물 Fold7 검증은 수행하지 않았으며 화면 비율 검사는 Unity 렌더링/입력 검사다. 이미지 검증 중 타격 중심 오프셋을 발견해 수정한 후 모든 검사를 다시 통과했다.

APK: `Builds/Android/ShadowTheater-Combat-v0.1.6-20261018.apk`, 118,172,440 bytes.

- 패키지: com.yooho.shadowtheater, v0.1.6 / code 7, min API26 / target API36, ARM64.
- SHA256: `773D17DF70BB69C8ABA0C39D1DC5D9BD613EF34A31AA5E992FE219E14E3DF840`
- 기존 QA 개발 서명 SHA256: `ac4da6fb2714b50463bd7161314b895d67855380e3a73f1a776cdfed4c3d9fd9`, apksigner 검증 성공.
- 검증 로그/9개 시점 이미지: `D:/codex-validation/battle-presentation-20261018/final-build.log`, `D:/codex-validation/unityGame-20261016-232237/project/Docs/Generated/Playability/`.
- 에뮬레이터 저장 백업: `D:/codex-validation/battle-presentation-20261018/save-before.json` 및 `.bak`.

180종 고유 원화 연결은 선행 커밋 `181984b`에서 완료했다. 해당 이미지와 기존 필살기 아트는 이번 빌드에도 포함된다. 이 APK는 개발/QA 빌드다.
