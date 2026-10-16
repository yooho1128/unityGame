# 진명 필살기 FX·SFX 파이프라인

진명 필살기는 외부 패키지 없이 동작하는 Canvas FX 프리팹과 PCM WAV를 자동 생성한다. 현재
콘텐츠의 고유 필살기 47개는 아래 9개 테마 중 하나를 사용한다.

| 테마 | 표현 |
|---|---|
| FlameCrown | 솟구치는 불꽃과 왕관 |
| FrozenArchive | 모여드는 얼음 기록편 |
| MoonBeast | 달빛 고리와 야수의 파동 |
| PuppetThreads | 내려꽂히는 꼭두각시 실 |
| AshBird | 재에서 펼쳐지는 날개 |
| FrozenMask | 양쪽에서 닫히는 얼음 가면 |
| MoonPetals | 회전하며 피어나는 달꽃 |
| LivingScript | 화면을 가르는 살아 있는 문장 |
| CosmicAudience | 점멸하는 우주 객석 |

## 생성과 연결

1. Unity 메뉴 `Tools > Shadow Theater > Generate Ultimate FX and SFX`를 실행한다.
2. FX 프리팹은 `Assets/Prefabs/FX/Ultimate`에, 시전·타격 WAV는
   `Assets/Audio/Generated/Ultimate`에 생성된다.
3. 생성기가 `Assets/Data/Generated/Skills`의 모든 필살기를 찾아 `fxPrefab`, `sfxClip`,
   `impactSfxClip`에 테마별 자산을 연결한다.

`Tools > Shadow Theater > Generate and Validate Full Game`을 실행해도 이 단계가 자동으로 포함된다.
생성 메뉴는 여러 번 실행해도 같은 경로의 자산을 갱신하므로 콘텐츠 JSON을 수정한 뒤 다시 실행해도
중복 자산이 생기지 않는다.

## 런타임 동작

`BattleFxDirector`는 `fxPrefab`이 있으면 `UltimateFxAssetPlayer`로 테마 궤적을 재생한다. 프리팹이
없거나 구성 요소가 빠졌으면 기존 절차적 문양을 대신 재생하므로 전투가 중단되지 않는다.
`BattleSfxPlayer`도 시전·타격 클립을 개별 확인하고, 비어 있는 클립만 런타임 합성음으로 대체한다.

## 최종 음원·아트로 교체

자동 생성 WAV는 폴백 자산이다. 제작 음원은 `Assets/Audio/Final/Ultimate/{테마}_{Cast,Impact}.wav`에
넣으면 재생성 후에도 우선 연결된다. 특정 스킬 전용 음원은
`Assets/Audio/Final/Skills/{skillId}_{Cast,Impact}.wav`로 테마 음원보다 우선한다.
전용 FX 프리팹은 `Assets/Prefabs/FX/Final/{skillId}.prefab` 또는 `{테마}.prefab`을 사용하며,
스킬 전용 프리팹이 테마 프리팹보다 우선한다. Final 파일은 자동 생성기가 덮어쓰지 않는다.

필살기와 일반 기술은 `Assets/Resources/Art/FX`의 불꽃·결정·초승달·깃털·꽃잎·참격·고리·별·문양·실
마스크를 공유한다. 사각형 파편을 대체하며, 출시 감사는 프리팹에 실제 영구 Sprite 참조가 있는지 검사한다.

`Tools > Shadow Theater > Validate Full Game`은 모든 필살기의 테마, FX 프리팹, 시전 SFX,
타격 SFX가 지정되었는지 검사한다.
