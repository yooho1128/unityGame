# 최종 픽셀 아트 교체 파이프라인

자동 생성 아트는 플레이 가능한 폴백으로 유지한다. 최종 PNG를 `Assets/Art/Final`에 추가하면 데이터와
40개 필드를 다시 생성할 때 같은 대상의 자동 생성 아트보다 우선 적용된다. 생성 폴더의 파일을 직접
수정하지 않으므로 재생성해도 원본 작업물이 사라지지 않는다.

## 폴더와 파일명

| 대상 | 경로와 파일명 | 규격 |
|---|---|---|
| 전투·각본집 초상 | `Portraits/{shadowId}.png` | 정사각형, 최소 192px, 권장 512×512 |
| 그림자 필드 프레임 | `Field/{shadowId}_01.png`, `_02.png` | 각각 24×32, 투명 배경 |
| 주인공 아래 보기 | `Player/director_down_01.png`, `_02.png` | 각각 24×32 |
| 주인공 위 보기 | `Player/director_up_01.png`, `_02.png` | 각각 24×32 |
| 주인공 옆 보기 | `Player/director_side_01.png`, `_02.png` | 각각 24×32, 왼쪽은 런타임 반전 |
| 지역 타일 | `Tiles/{Theme}_Ground.png`, `_Wall.png`, `_Accent.png` | 각각 32×32 |
| 달빛 초원 타일 | `Tiles/MeadowPixel_{Grass,Path,Water,Cliff,Bush}.png` | 각각 32×32 |

초상과 필드 그림자의 `{shadowId}`는 `ShadowData.shadowId`와 완전히 같아야 한다. 타일 이름은 자동 생성된
`Assets/Art/Generated/Tiles`의 파일명에서 확장자를 제외한 값과 같다.

## 임포트와 전체 반영

1. PNG를 해당 최종 아트 폴더에 넣는다.
2. 메뉴 `Tools > Shadow Theater > Import and Validate Final Pixel Art`를 실행한다.
3. Console에서 크기·이름 오류가 없는지 확인한다.
4. 메뉴 `Tools > Shadow Theater > Generate and Validate Full Game`을 실행한다.

픽셀 프레임과 타일은 Point 필터, 밉맵 없음, 무압축으로 설정된다. 필드 캐릭터와 주인공의 피벗은
아래 중앙이며 타일은 중앙 피벗이다. 초상은 UI용 단일 Sprite로 임포트된다.

## 커버리지 보고서

검사 결과는 `Assets/Art/Final/FinalArtCoverage.json`에 기록된다. 다음 수치를 제공한다.

- 180종 중 완성 초상이 있는 수
- 180종 중 `_01`, `_02`가 모두 있는 필드 그림자 수
- 주인공 필드 프레임 6개 중 완성 수
- 지원 타일 89개 중 완성 수
- 규격 오류 전체 목록과 아직 없는 파일 예시

최종 아트가 없는 대상은 오류가 아니라 자동 생성 아트를 사용한다. 이미 존재하는 PNG의 잘못된 크기,
잘못된 접미사, 존재하지 않는 `shadowId`는 오류이며 전체 회귀 검증에서도 실패로 처리된다.
