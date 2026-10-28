# 진화 단계별 외형

기존 진화 조건과 능력치 그대로 **기억복원 113개, 진명·구원 30개, 진명·원한 30개 형태**를 지원한다.
진화가 정의되지 않은 종에 임의의 진화나 스킬을 추가하지 않았다.

## 표시와 저장

- 173개 형태 모두 512×512 컬러 초상, 24×32 픽셀 동행 걷기 2프레임을 제공한다.
- 기존 신규 속성 11종의 기억복원 초상은 보존하고 나머지 162개 초상을 추가했다. 동행 346장은 새로 제작했다.
- 구원은 밝은 보호자 장식과 복장, 원한은 뒤틀린 윤곽과 날카로운 장식을 사용한다. 원래 배역의 정체성을 기준으로 생성했다.
- `ShadowInstance.Silhouette` / `FieldFrames`가 종 ID·저장된 단계·구원/원한 분기로 Resources 카탈로그를 조회한다.
  전투 패널, 파티/보관함, 아이템 대상, 도감 상세의 기존 초상 연결에 적용된다.
- 동행은 진화 이벤트를 구독해 즉시 새 걷기 프레임으로 바뀐다. 잔영은 기존 기본 프레임을 사용한다.
- 기존 세이브의 `shadowId`, `memoryStage`, `awakeningPath`로 선택하므로 저장 형식 변경이나 새 게임이 필요 없다.
  생성된 데이터베이스가 예전 상태여도 진화 정의가 있는 개체는 런타임 카탈로그에서 외형을 찾는다.
- 에디터 콘텐츠 생성기에도 형태별 초상/프레임 바인딩을 추가했다. 성장 수치, 조건, HP, 스킬에는 변경이 없다.

## 소스와 재생성

- `Docs/EvolutionArtSources.json`: 원화 시트·참고 시트·형태 순서·검수한 행 분할 위치.
- `Docs/ArtSources/Evolution/`: 22개 생성 시트, 22개 기존 배역 참고 시트, 생성 프롬프트 기록.
- `Assets/Resources/Data/EvolutionArtCatalog.json`: 런타임 조회 표.
- `Assets/Resources/Art/Evolution/`: 완성 초상과 동행 프레임. 초상 PPU 256, 동행 PPU 32, Point 필터, 밉맵 없음.

```sh
python Tools/prepare-evolution-art.py
python Tools/sync-evolution-codex.py /path/to/openWorld
python Tools/check-evolution-art.py /path/to/openWorld
```

Pillow가 필요하다. `sync-shadow-codex.py`의 전체 도감 동기화 마지막에도 진화 아트 동기화를 실행한다.
웹 도감은 각 진화 카드에 전용 초상과 걷는 동행을 표시한다. 동작 줄이기 설정에서는 애니메이션을 멈춘다.

## 확인 결과와 남은 확인

- 정적 검사: 173개 형태 대응, 중복 없는 초상, 346개 프레임 크기/이진 알파/16색 이하/48~240픽셀 면적/연결성, 모든 걷기 쌍 차이 통과.
- C# 변경 파일 구문 트리 검사 통과. Unity 컴파일 검사는 아니다.
- 웹: Vite 빌드, 191개 기본 카드/11속성 검사, 173개 진화 초상과 346개 동행 파일의 브라우저 로딩,
  걷기 전환, 동작 줄이기, 모바일 가로 넘침, JavaScript 오류 검사 통과.
- Unity 에디터가 이 환경에 없으므로 임포트·컴파일·Play Mode·실제 진화와 기존 저장 재개는 미검증이다.
  집 PC에서 별도 검증 복사본으로 `Completion → Generate Full Game and Audit`,
  `ShadowTheater.EditorTools.SourceOperationBatch.RunForCi`를 실행한다 (`-quit` 제외).
- `GameRegressionValidator`에는 모든 활성 형태의 리소스, 단계별 선택, 잔영 복귀, 동행 규격을 확인하는 검사를 추가했다.
  Unity에서 기억복원/구원/원한 후 동행 즉시 교체, 재시작 후 유지, 전투·보관함·도감 초상 표시를 육안 확인해야 한다.
  모바일 텍스처 메모리와 생성 원화의 작은 화면 가독성도 실기기 확인이 필요하다.
