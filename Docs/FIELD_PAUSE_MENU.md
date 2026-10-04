# 필드 메뉴

필드 탐색 중 우상단 `메뉴` 버튼 또는 에디터의 `Esc` 키로 여는 모바일 메뉴다.

## 생성과 배치

1. Unity 메뉴 `Tools > Shadow Theater > Generate Field Pause Menu`를 실행한다.
2. 생성된 `Assets/Prefabs/UI/FieldPauseCanvas.prefab`을 필드 씬 최상위에 배치한다.
3. 전체 게임 생성 메뉴는 프리팹 갱신과 40개 필드 배치를 자동으로 처리한다.

## 제공 기능

- 현재 지역, 누적 플레이 시간, 보유 금화 표시
- `SaveManager`의 안전 저장과 현재 플레이어 위치 캡처
- 전체·음악·환경음·효과음 음량 실시간 조절 및 `PlayerPrefs` 보존
- 확인 창을 거친 저장 후 `Title` 씬 복귀
- 메뉴가 열린 동안 타일 이동 잠금
- 전투, 대화, 각본집, 파티·서고, 월드맵과 동시 열림 방지
- 한국어/English 실시간 현지화

`Tools > Shadow Theater > Validate Full Game`은 각 필드에 컨트롤러와 핵심 직렬화 참조가 있는지 검사한다.
