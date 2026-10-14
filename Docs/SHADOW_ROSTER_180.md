# 180종 그림자 지역 로스터

전체 도감은 핵심·제2~8막 카탈로그 80종, 지역 확장 97종, 전설 성장 3종으로 총 180종이다.
`Generate Playable Prologue`를 실행하면 데이터 생성 후 지역 확장 로스터를 63개 지역의 랜덤 조우 규칙에 연결한다.

## 지역 확장 97종

- 월드맵의 `featuredShadows` 가운데 기존 80종과 전설 3종에 없는 대표 그림자를 먼저 생성한다.
- 나머지는 막별 생태 모티브를 조합하고, 마을과 보스 전용 무대를 제외한 실제 랜덤 조우 지역에만 순환 배치한다.
- 모든 랜덤 조우 지역은 최소 1종의 지역 전용 일반 로스터를 가진다.
- ID는 `exp_{regionId}_{slot}` 형식이며 기존 생성 순서를 유지한다. 출현 지역을 다시 나눠도 이미 저장된
  그림자 ID와 세이브 참조는 바뀌지 않는다.
- 생성 결과는 `Assets/Resources/Data/RegionalRosterManifest.json`에 기록되며 런타임 랜덤 조우가
  현재 지역의 항목만 읽는다.

## 전투·수집 규칙

| 항목 | 적용 규칙 |
|---|---|
| 권장 레벨 | `RegionCatalog`의 지역 최소·최대 레벨을 그대로 사용 |
| 속성 | 왕관의 재는 Flame, 금기의 서고와 기억의 바다는 Frost, 야수·검열·최종 지역은 Shade 중심 |
| 역할 | 물리 딜러·마법 누커·속도 유틸·탱커·서포터를 지역별로 순환 |
| 희귀도 | 지역 대표 그림자와 7칸 주기 개체는 Rare, 나머지는 Standard |
| 출현 비중 | 대표 목록의 앞쪽 그림자가 자주 나오며 Rare는 Standard보다 낮은 확률로 출현 |
| 보스 보호 | RegionalBoss·Legendary 및 해당 지역의 `bossShadowName`은 일반 랜덤 조우에서 제외 |
| 조우 간격 | 일반 필드 7~11보, 던전 5~8보, 숨겨진 지역 9~13보의 안전 구간 적용 |
| 확정 조우 | 일반 18보, 던전 14보, 숨겨진 지역 22보째까지 조우가 없으면 다음 판정에서 확정 |
| 포획률 | Rare 18%, Standard 34%를 기본값으로 사용하고 남은 HP 보정을 전투 시스템에서 적용 |
| 스킬 | 역할별 기본 공격과 속성별 2개 기술을 배정 |
| Lore | 잠금 상태에는 서식 환경, 해금 상태에는 지역 사건 속 마지막 기억을 기록 |
| 성장 | Standard는 레벨 성장 중심, Rare·보스·전설은 기억 복원/진명 각성을 우선 제작 |

## 생성 순서

1. `Tools > Shadow Theater > Generate Core Content Data`
2. `Tools > Shadow Theater > Generate Legendary Growth Batch`
3. `Tools > Shadow Theater > Generate Playable Prologue`

3번 메뉴는 앞의 데이터 생성도 자동으로 실행한다. 완료 로그에서 핵심 그림자 177종과 성장 전설 3종을
확인한 뒤 `ShadowDatabase.asset`의 최종 로스터가 180종인지 확인한다.
