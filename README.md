# 그림자 극장 잔영 (Shadow Theater)

1인 개발을 목표로 하는 스토리 중심 2D 모바일 수집형 턴제 RPG의 Unity 프로젝트 저장소입니다.

## 프로젝트 방향

- 플랫폼: iOS / Android
- 필드: 2D 타일맵 탐색과 심볼 인카운터
- 전투: 그림자 1대1 턴제 전투
- 성장: 스토리 모드와 반복 파밍 모드
- 아트: 실루엣 캐릭터와 포인트 컬러, URP 2D 조명 중심
- 데이터: 변경되지 않는 원본은 `ScriptableObject`, 플레이 중 상태는 일반 C# 런타임 객체로 분리

## 현재 포함된 항목

- Unity용 `.gitignore`
- Unity YAML 병합 및 Git LFS 기본 규칙
- `Assets` 기본 폴더 구조
- 공통 enum
- `ShadowData`, `SkillData` ScriptableObject 골격
- `ShadowRuntime` 전투 런타임 상태 골격
- `BattleManager` 전투 흐름 골격

Unity가 프로젝트를 생성하면서 관리해야 하는 `Packages`, `ProjectSettings`와 각 에셋의 `.meta` 파일은 아직 포함하지 않았습니다. 집 PC에서 Unity 프로젝트를 생성한 뒤 해당 파일을 이 저장소에 합치고 커밋합니다.

## 폴더 구조

```text
Assets/
├─ Art/
│  ├─ Animations/
│  ├─ Materials/
│  └─ Sprites/
├─ Audio/
│  ├─ Music/
│  └─ SFX/
├─ Prefabs/
│  ├─ Characters/
│  └─ UI/
├─ Scenes/
├─ ScriptableObjects/
│  ├─ Shadows/
│  └─ Skills/
├─ Scripts/
│  ├─ Battle/
│  ├─ Core/
│  ├─ Data/
│  ├─ Field/
│  ├─ Runtime/
│  └─ UI/
└─ Settings/
```

## 집 PC에서 시작하기

1. Git과 Git LFS를 설치합니다.
2. `git lfs install`을 한 번 실행합니다.
3. 이 저장소를 복제합니다.
4. Unity Hub에서 **2D URP** 프로젝트를 별도의 빈 임시 폴더에 생성한 뒤 Unity Editor를 닫습니다.
5. 임시 프로젝트의 `Packages/`, `ProjectSettings/`를 복제한 저장소 루트로 복사합니다.
6. 임시 프로젝트의 `Assets/`는 폴더 전체를 덮어쓰지 말고, URP 템플릿이 만든 렌더러·설정 에셋만 저장소의 `Assets/Settings/`로 옮깁니다. 기존 `Assets/Scripts/`는 유지합니다.
7. Unity Hub에서 `Add project from disk`로 복제한 저장소 폴더를 추가하고 엽니다.
8. Unity에서 `Edit > Project Settings > Editor`를 열고 아래처럼 설정합니다.
   - Version Control Mode: `Visible Meta Files`
   - Asset Serialization Mode: `Force Text`
9. Unity가 생성한 `Packages/`, `ProjectSettings/`, `.meta` 파일을 포함해 첫 Unity 초기화 커밋을 만듭니다.
10. 필요한 패키지는 Unity Package Manager에서 설치합니다. URP 2D, 2D Tilemap Editor 등 실제로 사용할 패키지만 선택합니다.

> Unity 버전은 팀에서 사용할 버전을 정한 뒤 `ProjectSettings/ProjectVersion.txt`로 고정합니다. 현재 저장소는 특정 Unity 버전을 임의로 고정하지 않습니다.

## 데이터와 런타임 상태 원칙

`ShadowData`와 `SkillData`에는 이름, 기본 능력치, 아이콘처럼 원본 데이터를 저장합니다. 현재 HP, FP, 상태이상처럼 플레이 중 바뀌는 값은 `ShadowRuntime`에 둡니다. ScriptableObject 에셋을 전투 중 직접 수정하지 않습니다.

## Git 작업 원칙

- `.meta` 파일은 대응하는 Unity 에셋과 항상 함께 커밋합니다.
- `Library`, `Temp`, `Logs`, 빌드 결과물은 커밋하지 않습니다.
- Scene과 Prefab의 동시 수정은 가급적 피합니다.
- PSD, WAV, MP4 등 큰 바이너리 파일은 Git LFS로 관리합니다.
- API 키, 서명 키, 비밀번호는 저장소에 올리지 않습니다.
