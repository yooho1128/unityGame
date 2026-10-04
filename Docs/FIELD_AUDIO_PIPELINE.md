# 필드 환경음·발걸음 파이프라인

40개 필드는 지역 분위기와 바닥 재질에 맞는 오디오 자산을 자동으로 생성해 사용한다. 외부 음원이
없어도 완전한 오디오 흐름을 확인할 수 있고, 이후 전문 제작 음원으로 참조만 교체할 수 있다.

## 자동 생성

Unity 메뉴 `Tools > Shadow Theater > Generate Field Audio Assets`를 실행하면 다음 자산을 만든다.

- `Assets/Audio/Generated/Field/Ambience`: 29개 테마의 6초 지속음과 간헐 원샷 58개
- `Assets/Audio/Generated/Field/Footsteps`: 8개 지형별 4변형 발걸음 32개

`Tools > Shadow Theater > Generate and Validate Full Game`에도 이 과정이 포함된다. 이어서 생성되는
40개 필드 씬은 `FieldAmbientAudio`의 `ambienceClip`·`detailClip`과 플레이어
`FieldFootstepAudio`의 클립 배열을 자동으로 채운다.

## 지형 분류

| 지형 | 대표 지역 |
|---|---|
| Wood | 극장, 오페라, 인형사 무대 |
| Stone | 마을 석로, 성도, 지하묘, 검열단 시설 |
| Grass | 달빛 초원, 늪, 숲 |
| Ash | 재 황무지, 재의 왕좌 |
| Snow | 서리 항구, 백색 기록원, 금기 서가 |
| Water | 기억의 바다 |
| Metal | 시계 골목, 끊어진 공방 |
| Void | 푸른 심연, 우주의 객석 |

발걸음은 한 칸 이동이 완료되는 순간 재생된다. 네 변형을 순환하고 작은 피치 편차를 적용해 같은
샘플이 반복되는 느낌을 줄인다. 환경음은 맵 이동과 전투 진입 전에 페이드 아웃되고 필드 복귀 시
설정 화면의 환경음 볼륨을 반영해 페이드 인된다. 발걸음은 효과음 볼륨을 따른다.

## 폴백과 최종 교체

생성 WAV가 연결되지 않았을 때도 두 런타임 컴포넌트가 같은 테마의 합성 클립을 메모리에 만들어
게임 진행을 유지한다. 최종 음원은 씬의 AudioClip 참조를 바꾸면 된다. 전체 생성 메뉴를 다시 실행하면
자동 자산이 재연결되므로 수동 교체는 데이터·씬 생성이 끝난 뒤 적용한다.

`Tools > Shadow Theater > Validate Full Game`은 40개 필드 모두에 지속·간헐 환경음과 발걸음 4개가
연결되었는지 검사한다.
