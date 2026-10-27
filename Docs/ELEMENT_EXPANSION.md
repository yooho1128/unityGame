# 11속성 그림자 확장

## 콘텐츠

기존 180종 ID와 성장 형태를 유지하고 희귀 11종을 추가하여 소스 로스터는 191종이다.
신규 종마다 기본 공격·고유 기술·복원 기술 3개(총 33개), 레벨 18 기억 복원과 별도 복원 초상을 제공한다.
희귀 등급의 기존 정책에 맞춰 진명 구원/원한 분기는 추가하지 않는다.
신규 종의 지역은 ElementExpansionEncounters.json에 정의하고 조우 레벨은 해당 지역의 권장 레벨을 따른다.
기존 지역 확장 생성 뒤에 신규 카탈로그를 합치므로 이전 세이브의 생성 ID가 바뀌지 않는다.

| 속성 | 1.5배로 공격하는 상대 |
|---|---|
| 불 | 풀, 얼음 |
| 물 | 불, 땅 |
| 풀 | 물, 전기 |
| 얼음 | 풀, 바람 |
| 빛 | 어둠 |
| 어둠 | 빛, 에스퍼 |
| 무속성 | 에스퍼 |
| 땅 | 불, 전기 |
| 전기 | 물, 바람 |
| 바람 | 땅, 에스퍼 |
| 에스퍼 | 얼음, 빛, 무속성 |

동일 속성은 1배, 위 관계의 반대 방향은 0.75배, 나머지는 1배다.
사용자가 지정한 빛↔어둠, 에스퍼↔무속성은 양방향 1.5배를 우선한다. 면역은 없다.
기준은 Assets/Resources/Data/ElementCatalog.json이다.
기존 None 기술의 시전자 속성 상속은 유지하고 forceNeutralElement=true인 신규 무속성 기술만 고정 무속성이다.
기존 enum 0~3 값을 보존한다. 기존 불/얼음/어둠 삼각 상성은 새 표로 바뀌므로 스타터 균형은 플레이 테스트가 필요하다.

## 이미지와 재생성

AI 생성 원본 14장과 셀→ID 매핑을 Docs/ArtSources/ElementExpansion 및 ElementExpansionSources.json에 보관한다.
사용자가 승인한 분할·축소·팔레트 정리를 Tools/prepare-expansion-art.py로 재현한다.
기존 전용 동행이 없던 167종과 신규 11종에 356 PNG를 추가했다.
기존 13종의 26 PNG는 덮어쓰지 않는다. 동행은 24×32, 2프레임이다.
신규 동행은 불투명/투명 알파, 최대 12색, 48~240 픽셀의 연결된 실루엣을 사용한다.
기본/복원 초상 22장은 512×512 RGBA다.

## 검증과 한계

- python Tools/check-element-expansion.py /workspace/openWorld: 11종/33기술/121상성, 191종 프레임 쌍과 신규 이미지 규격 및 도감 데이터 검사.
- python Tools/sync-shadow-codex.py /workspace/openWorld: 기존 도감 180종 설정을 보존하며 동기화.
- C# 구문 파서 검사와 git diff --check. 구문 검사는 Unity 컴파일을 대신하지 않는다.
- openWorld npm ci --ignore-scripts / npm run build 및 Chromium 도감 기능 검사.
- GameRegressionValidator에 121조합·상호 강점·무속성 상속 회귀 검사를 추가했다. Unity에서는 아직 실행하지 않았다.

이 환경에는 Unity가 없어 생성 에셋/씬 재생성, 컴파일, Play Mode, 실기기 성능·전투 균형은 미검증이다.
Unity 2022.3.52f3 별도 검증 복사본에서 Completion → Generate Full Game and Audit 후 SourceOperationBatch.RunForCi로 기동 검증해야 한다.
기존 Docs/Generated의 180종 보고서는 과거 결과이며 이번 검증 결과가 아니다.
기존 13종 중 knight의 픽셀 면적과 puppet의 반투명 알파 등은 현행 필드 품질 기준과 불일치한다.
기존 파일은 보존했으며 이번 신규 178종의 엄격한 픽셀 검사와 별도로 다뤄야 한다.
