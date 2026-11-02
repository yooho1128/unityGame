# 대표 스킬 수동 플레이 확인 — 2026-11-02

Unity 2022.3.62f3 에디터의 실제 전투 UI를 마우스로 조작해 고유 스킬 20개를 사용했다. 공격·교체·도주를 포함한 전투 입력은 총 36회였다. 첫 세션과 수정 후 재확인 세션 모두 런타임 오류 0건이며, 최종 `SourceOperationBatch.RunForCi`도 통과했다.

## 환경과 범위

- 별도 검증 복사본: `D:/codex-validation/presentation-polish-20261102/project`.
- 검증 전용 임시 도구는 복사본에만 두었다. 부상 상태의 레벨 60 파티, 시작 FP 5, 약한 공격력의 내구성 표적을 준비하고 실제 `GameFlowController`로 전투에 진입했다. 전투 행동은 자동 제출하지 않았다.
- 마우스로 스킬·기본 공격·교체·속도·도주·결과 계속 버튼을 직접 눌렀다. 1·2·3배속에서 턴 복귀, HP/FP 갱신, 회복 재사용 제한, 상태이상 표시를 확인했다.
- 도주 결과와 승리 결과 모두 계속 버튼을 눌러 필드 복귀를 확인했다. 승리 전환 검사는 표적 HP를 1로 준비한 뒤 실제 기본 공격을 입력했다.
- APK/AAB/Player 빌드 및 에뮬레이터 설치는 실행하지 않았다. 전체 199개 전수 수동 검사나 전투 밸런스 검증은 아니다.

## 사용한 고유 스킬 20개

| 캐릭터 | 실제 사용한 스킬 ID |
| --- | --- |
| 기사 | `slash`, `ember_strike`, `fallen_crown`, `guardian_oath` |
| 인형 | `cut_string`, `puppet_string_reversal`, `stitch` |
| 사서 | `inkshot`, `frost_sigil`, `zero_prophecy`, `memory_sanctuary` |
| 야수 | `claw`, `blood_fang`, `homeward_howl` |
| 까마귀 | `peck`, `crow_dawn_beacon`, `crow_sky_cremation` |
| 미망인 | `widow_fan_cut`, `eclipse_widow`, `moonlit_benediction` |

공격기의 왕관·실 조종 손·얼음 창·턱·깃털·신부 형태와 회복기의 빛·봉합·페이지·울음·꽃 형태가 구별되었다. 궁극기 제목 배너는 공통 UI이며, 그 아래 스킬 그래픽과 배치는 각각 다르다.

## 발견해서 수정한 문제

| 문제 | 수정 및 확인 |
| --- | --- |
| 스킬 목록이 한 글자 폭으로 접힘 | 기존 캔버스의 세로 목록이 행 너비를 제어하도록 보정하고 선택 패널 높이를 확보. 다섯 교체 후보 및 네 개 스킬 행이 정상 표시·선택됨. |
| HP 숫자는 40%인데 게이지는 100% | 주 패널과 파티 슬롯 모두 가로 Filled 이미지와 유효 스프라이트를 보장. 304/759에서 약 40%로 표시되고 회복 후 길이·색상도 갱신됨. |
| 전투 위에 필드 메뉴가 남아 FP를 가림 | 이전 구조의 필드 전용 캔버스를 FieldRoot에 연결. 전투 중 숨겨지고 필드 복귀 시 다시 나타남. |
| 이전 전투 프리팹에서 극장 배경 누락 | SafeArea 유무에 맞춰 배경 이미지를 HUD 뒤에 생성. 수정 후 실제 전투에서 극장 배경 확인. |
| 회복 페이지·상단 파편이 화면에서 잘림 | 회전한 이미지의 외곽 크기를 계산해 핵심 문양뿐 아니라 이동체·파편에도 화면 경계를 적용. 기억의 서고와 봉화를 실제 UI에서 다시 사용했고, 페이지가 화면 안에 표시되는 장면을 재확인. |

예를 들어 백성의 맹세는 HP 304→645로 반영되었다(342 회복 후 적의 1 피해). FP는 5→2 차감 후 다음 턴 3으로 증가했고, 같은 회복기의 2턴 재사용 제한 및 남은 공용 횟수가 표시되었다. FP 부족 기술도 사용 불가 이유가 표시되었다.

## 검증 결과와 남은 확인 사항

- 첫 수동 세션: 고유 스킬 20개, 런타임 오류 0.
- 수정 후 재확인: 기억의 서고·봉화 재사용 및 기본 공격으로 승리, 런타임 오류 0.
- 최종 Unity 점검: PrologueTheater에서 Player/Grid/GameFlow 초기화 성공, Play Mode 12초 관찰, 런타임 오류 0, PASS.
- 원본 기준 5,593개 기존 파일의 해시를 비교했다. 이번 수정 6개 C# 파일 외 기존 파일 변경은 0개다. 원본 C# 293개와 검증 복사본의 대응 소스가 모두 일치한다.
- **추가 발견:** 이전 필드 씬의 상단 퀘스트 카드와 메뉴가 겹친다. 이번 전투 UI 수정 범위에는 포함하지 않았다.
- 키보드 Enter·에디터 단축키 주입에서는 응답을 확인하지 못했다. 키보드·게임패드 조작을 통과로 판정하지 않았으며, 원인 구분과 실기기 확인이 남아 있다.
- BGM/SFX의 실제 청음, 다른 화면 비율, 모바일 터치와 성능은 이번 수동 검증 범위에 포함하지 않았다.

## 증거

- [전투 화면](Generated/ManualSkillPlay/final-battle-hud.jpg)
- [수정 전 스킬 목록](Generated/ManualSkillPlay/before-skill-menu.jpg) / [수정 후](Generated/ManualSkillPlay/fixed-skill-menu.jpg)
- [페이지 경계 보정 후](Generated/ManualSkillPlay/fixed-memory-boundary.jpg)
- [도주 결과](Generated/ManualSkillPlay/escape-result.jpg) / [승리 결과](Generated/ManualSkillPlay/victory-result.jpg)
- [전투 입력과 HP/FP 기록](Generated/ManualSkillPlay/events.txt)
- [파일 보존·스킬 집계](Generated/ManualSkillPlay/verification.json)
- [최종 Unity 기동 결과](Generated/ManualSkillPlay/source-operation.txt)

전체 원본 캡처와 Unity 로그는 `D:/codex-validation/manual-skill-play-20261102`에 보존했다.
