# 출시 자산 파이프라인

기준 버전은 Unity 2022.3.52f3입니다. 출시 후보를 만들기 전에 Unity 메뉴
`Tools → Shadow Theater → Release → Generate and Audit Assets`를 실행합니다.

이 메뉴는 다음 작업을 순서대로 수행합니다.

1. 180종 그림자와 전체 스킬 데이터를 다시 생성합니다.
2. 수작업 최종 초상·필드 도트를 우선 적용하고 규격을 검사합니다.
3. 진명 필살기 9개 테마의 FX 프리팹과 캐스트/타격 WAV를 연결합니다.
4. 일반 기술에 불꽃·서리·그림자·무속성 캐스트/타격 WAV 8개를 생성해 연결합니다.
5. 13개 BGM, 29개 환경 테마의 지속/간헐음 58개, 8개 지형의 발걸음 32개를 생성합니다.
6. 지역 진입 카드의 지역색 배경 광원과 마을·던전·숨은 지역·보스·전설 모티프 참조를 검사합니다.
7. `Assets/Reports/ReleaseAssetCoverage.json`에 실제 연결 수와 누락 목록을 기록합니다.

일반 기술은 전용 프리팹이 없어도 공격 대상 위치에 속성색 파편이 터집니다. 물리 기술은 긴 파편,
마법 기술은 많은 파편, 회복 기술은 녹색 원형 파동, 상태 이상 기술은 더 큰 맥동으로 구분됩니다.
음향은 스킬별 위력·대상에 따라 피치와 볼륨이 조정됩니다.

수작업 초상이 없는 그림자는 `Assets/Art/Generated`에 역할·속성·등급 기반 컬러 픽셀 초상을 만듭니다.
파일명 규격을 맞춘 수작업 파일이 `Assets/Art/Final`에 있으면 언제나 생성 초상보다 우선합니다.
따라서 생성 초상은 출시 중 누락 이미지를 막는 안전망이며, 보고서의 `finalPortraits`와
`finalFieldActors`는 수작업 교체 진척을 별도로 보여 줍니다.

## 출시 전 실행 순서

1. `Generate and Audit Assets`
2. `Tools → Shadow Theater → Generate and Validate Full Game`
3. Console의 컴파일 오류와 `[Regression]` 오류가 0인지 확인
4. Android Development Build를 실기기에서 20분 이상 플레이하고 QA 보고서 내보내기
5. Android와 iOS 보고서를 가져온 뒤 `Mobile → Validate Release Gate`

`Audit Assets Only`는 데이터를 다시 만들지 않고 현재 연결 상태만 확인합니다. 감사 실패 항목은
전체 회귀 검사와 출시 게이트에도 오류로 전달됩니다. 수작업 아트 교체율은 게임 실행을 막지는 않지만
경고로 남으므로, 스토리 핵심 배역과 상점 스크린샷에 등장하는 그림자부터 교체합니다.

클라우드나 CI에 Unity Editor가 없을 때는 C# 정적 검사만 가능하며, 메뉴 실행·Android 빌드·실기기 성능은
검증 완료로 간주하지 않습니다.
