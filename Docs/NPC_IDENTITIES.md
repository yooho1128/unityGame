# NPC 이름표와 전용 이미지

이야기 NPC 20명, 숨겨진 기억 NPC 3명, 7개 마을의 서비스 NPC 42명(치유사·소식꾼·조사관·장인·상인·주민)을 위한 고유 ID/이름/역할/이미지 영역 65개를 추가했다.
마을 서비스 인물은 서로 다른 이름을 갖고, 이름 아래 역할에 지역과 직업을 표시한다.

## 연결

- 기준 데이터: Assets/Resources/Data/NpcIdentityCatalog.json.
- 전용 이미지: Assets/Resources/Art/Npcs/npc_atlas_00..04.png, AI 생성 원본 5장, 각 1024×1536 RGBA.
- 각 아틀라스는 4×4, 좌상단부터 행 순서. 마지막 장은 첫 인물만 사용한다. 마지막 인물의 상하 여백은 cropTop/cropHeight로 조정한다.
- NpcIdentityCatalog는 Texture2D에서 각자의 Sprite를 만들고 캐시한다. 그림자 초상이나 동행 프레임을 대체 이미지로 사용하지 않는다.
- 신규 씬 생성은 CreateNpcActor를 사용해 NPC 전용 ID만 저장한다. 이미지·이름표는 Play Mode에서 적용한다.
- 이전 생성 씬은 FieldNpcPresence.Awake가 FieldNpcIdentity를 추가한다. StoryNpc.NpcId 또는 씬 지역+서비스 컴포넌트로 인물을 찾는다.
- ShadowFieldMotion.ConfigureNpc는 이전 그림자의 애니메이션 프레임과 동작 유형을 초기화한다. 기존 이동·대화·퀘스트 ID는 유지한다.
- 이름표는 월드 Canvas이며 이름/역할을 표시한다. 클릭을 가로채는 GraphicRaycaster가 없다. 인사 아이콘과 일상 대사는 이름표 위로 옮겼다.
- 복구 후 등장 인물은 기존 활성화 조건을 따른다. 이동·비활성화 시 이름표도 부모와 함께 움직이거나 숨겨진다.

## 확인

python Tools/check-npc-identities.py는 65개 ID·이름·이미지 영역의 유일성, 알파/영역 크기, 모든 마을 서비스 조합, 이야기·기억 ID, 아틀라스 임포트 크기 보존, 기존 씬 연결 지점을 검사한다.
NpcIdentityValidator는 Unity 전체 회귀 검사에 연결되어 있다. 이 환경에서는 실행하지 않았다.
Prototype/npc-cast.html은 이름·역할·외형 검수용 갤러리이며 Unity 화면을 대신하지 않는다.

이번 환경에서 Python 검사·변경 C# 구문 검사·갤러리 65명 이미지 로딩/검색/모바일 검사와 git diff --check를 수행했다.
Unity가 설치되지 않아 컴파일·Play Mode·실기기 메모리/이름표 가독성/대화와 배회 동작은 미검증이다.
Unity 2022.3.52f3 별도 검증 복사본에서 SourceOperationBatch.RunForCi를 실행한 뒤,
각 마을과 기억 해금 지역에서 이름표·대화·복구 인물·가림·프레임 교체 여부를 확인해야 한다.
