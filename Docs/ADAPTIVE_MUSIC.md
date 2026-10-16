# 적응형 음악 시스템

`AdaptiveMusicDirector`는 `CoreSystems`와 함께 씬 전환 후에도 유지되며 두 AudioSource를 사용해 음악을
교차 전환한다. 전체 음량과 별도로 설정 화면의 음악 음량을 적용한다.

## 음악 큐

- 타이틀 1개
- 제1막부터 제8막까지 필드 음악 8개
- 일반·라이벌·보스 전투 음악 3개
- 엔딩 음악 1개

출시용 음악은 `Assets/Audio/Final/Music/{MusicCue}.wav`의 13개 16마디 편곡 루프를 우선 사용한다.
곡 길이는 약 31~53초이며 스트리밍으로 가져온다. `ReleaseMusicComposer`는 없는 곡만 생성하므로
같은 경로에 제작 음원을 넣어도 재생성으로 덮어쓰지 않는다. 반복 경계·클리핑·무음·DC 편향과 실제
CoreSystems 참조는 출시 자산 감사에서 검사한다.

폴백용 13개 8초 무봉제 WAV는 `Assets/Audio/Generated/Music`에 생성된다. 메뉴
`Tools > Shadow Theater > Generate Adaptive Music`으로 단독 생성할 수 있으며,
`Generate Title and Starter UI`와 전체 게임 생성에도 자동 포함된다.

필드 씬이 로드되면 `RegionCatalog.json`의 `act`를 읽어 해당 막 음악을 선택한다. 전투 진입 시
`BattleMode`에 따라 일반·라이벌·보스 음악으로 바뀌고, 전투 종료 후 현재 지역 음악으로 돌아간다.
최종 선택 대사가 시작되면 엔딩 음악을 사용한다.

생성 음악이 누락되어도 런타임 합성 루프가 폴백으로 작동한다. 전문 제작 음악으로 교체할 때는
`CoreSystems.prefab`의 `AdaptiveMusicDirector.clips` 배열에서 같은 `MusicCue` 순서로 AudioClip을
바꾸면 된다. 재생성 후에도 유지할 제작 음원은 `Assets/Audio/Final/Music/{MusicCue}.wav`에 넣는다.

`Tools > Shadow Theater > Validate Full Game`은 CoreSystems의 음악 감독, 13개 큐와 클립 연결을
검사한다.
