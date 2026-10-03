# 최종막 · 그림자 극장

제7막 완료 후 `미망인의 월궁` 동쪽 포털에서 시작하는 최종 이야기다. 생성 메뉴를 다시 실행하면 아래
5개 필드와 전투·대화·엔딩 오브젝트가 함께 만들어진다.

| 순서 | 씬 | 핵심 내용 |
|---:|---|---|
| 1 | `InvertedLobby` | 거꾸로 안내원, 천장 관객, 반전된 대역 |
| 2 | `EndlessBackstage` | 잊힌 소품, 막간 배우, 막간의 관리자 |
| 3 | `FirstActorRoom` | 첫 대사의 메아리, 황금 가면, 최초의 배우 |
| 4 | `CosmicAuditorium` | 별자리 좌석, 침묵의 박수, 마지막 관객 |
| 5 | `FinalCurtain` | 또 다른 연출가의 선택, 각본 밖의 그림자, 엔딩 각본집 |

최종 보스 승리 대화가 `story_finale_unlocked`를 기록한다. 이후 무대 북쪽의 각본집과 상호작용하면
`finale_director_choice`를 거쳐 누적된 기억·자비·통제 선택값에 맞는 엔딩이 재생된다. 엔딩 완료 후에는
타이틀로 돌아가며 엔딩 도감과 다음 회차 기능을 사용할 수 있다.

최종막 콘텐츠 카탈로그는 `Assets/Resources/Data/Act8ContentCatalog.json`이다. 전용 그림자 13종과
스킬 12개를 제공하고, 성장 카탈로그의 `legend_first_actor`, `legend_last_audience`도 지역 보스로 사용한다.

## 완성 초상과 모션

`Assets/Art/Final/Portraits`에는 `inverted_guide`, `silent_applause`, `outside_script_shadow`의 투명
512×512 전투 초상이 들어 있다. `CoreContentBatchGenerator`는 같은 ID의 완성 초상이 있으면 자동 생성
실루엣 대신 해당 이미지를 `ShadowData.silhouetteSprite`에 연결한다. 필드에서는 24×32 전용 픽셀 외형을
사용한다. 세 그림자는 `Assets/Art/Final/Field`의 `_01`, `_02` 프레임을 우선 불러오며, 다른 그림자는
동일 규격 프레임을 자동 생성한다. 역할에 따라 보행·부유·활공 모션을 적용한다. 전투 초상은 대기 호흡과 기존 공격·피격·필살기
연출을 함께 재생한다.

달빛 초원 진행이 막혔던 이전 생성본은 생성 메뉴를 다시 실행해야 한다. 새 버전은 보스 무대 출구를
북쪽 절벽이 아니라 동쪽 길 `(9, 0)`에 두며 별도 퀘스트 플래그로 입장을 막지 않는다.
