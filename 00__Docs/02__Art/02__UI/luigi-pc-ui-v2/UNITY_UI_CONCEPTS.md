# Unity 프로젝트 기준 루이지 UI 시안

> 새 기본 버전은 **루이지 콘셉트 강화 · 자유 배치**입니다. 원본 구조를 보존한 아래 초기 시안은 갤러리의 **이전 시안**으로 남겼습니다. 새 디자인과 서체 설명은 [LUIGI_CONCEPT_V2.md](LUIGI_CONCEPT_V2.md), PNG와 생성 기록은 `unity-concepts-v2/`에 있습니다. **현재 Unity 배치**는 기능·정보의 비교 기준이며 새 시안의 위치를 제한하지 않습니다.

2026-10-10 · 연결 프로젝트 `BloodAndBeans` · Unity 6000.5.9f1

`blood-beans-luigi-portable.html`의 **Unity 전체 UI**에서 선택합니다. 각 항목의 **Unity 원본 배치**는 현재 프리팹을 1920×1080 임시 프리뷰로 렌더링한 이미지입니다. **기존 캐릭터 시안**은 이전 4화면·4캐릭터·밤 가방 선택을 유지합니다.

## 구성

| 분류 | 시안 | 기준 프리팹·표시 |
|---|---|---|
| 입장 | 타이틀 | UITitleMenuScreen · 로고와 하단 버튼 3개 |
| 입장 | 방 목록 | UIRoomListScreen · 방 선택, 팀 수 조절, 생성·입장·새로고침 |
| 입장 | 캐릭터·팀 선택 | UICharacterSelectScreen · 좌측 정보, 8인 무대, 하단 5클래스 |
| 입장 | 보조 대기실 | UIRoomScreen · 현재 기본 진입 경로는 캐릭터 선택으로 바로 이동 |
| 낮 | 카페 HUD | UIMatchHudScreen · 순위 띠, 중앙 시계, 매출 목표, Q, 보석, F1 |
| 낮 | 조합법 | UIMatchHudScreen · 커피 7종·디저트 4종 |
| 낮 | 주문·인내심 | UICustomerOrder · 사용한 인내심의 말풍선 내부 채움 |
| 낮 | 제조·완성 판정 | UIMakingCard, UICompletionGauge · 제조 상태·침 게이지·판정 |
| 밤 | 탐색 HUD | UIMatchHudScreen · 적재량, Q·SPACE, 작은 가방 |
| 밤 | 상자·재료 루팅 | UIBoxLootPopup, UIBoxLootSlot · 작은 가로 5칸·순차 공개 |
| 밤 | 가방·스킬·귀환 안내 | UIBagStatus, UISkillSlot, UIKeyHint · 상태 비교 |
| 정산 | 귀환 결과 | UIReturnResultPopup · 성공·일부 소실·가방 분실 선택 |
| 정산 | 하루 정산·예보 | UIDaySettlementScreen · 2×2 정보 카드 |
| 정산 | 최종 결과 | UIMatchResultPopup · 누적 매출 순위·로비·재시작 |
| 공통 | 설정 | UISettingsPopup · 음량 3종·전체 화면·카메라 조건부 감도 |
| 공통 | 로딩·접속 대기 | UILoadingPopup · 불러오는 중 / 인원 대기 선택 |
| 공통 | 페이즈 전환 | UIPhaseCuePopup · 3·2·1·READY·GO!·마감! 선택 |
| 공통 | 공통 부품 | 선택 카드·팀 버튼·이름표·보석 턴·순위·슬라이더·탭 |

전체 화면 프리팹 6개, 팝업 프리팹 6개, 부품 프리팹 22개를 화면과 비교 시트에 연결했습니다. 부품 시트는 식별하기 쉽도록 확대했습니다.

## 표현 기준

- PC 기준 앵커와 패널 구조를 참고하고, 어두운 가지색 패널·얇은 라일락 가장자리·크림색 문자·연두 강조로 통일합니다.
- 캐릭터는 3D 흡혈귀·좀비·늑대인간·마녀 계열, 손님은 유령입니다. 모델 교체와 현재 클래스명·스킬 규칙은 별개입니다.
- 낮에는 가방이 없습니다. 밤의 가방은 작고 단순한 무광 아이콘입니다. 가방 상태·적재량은 별도로 구분합니다.
- 인내심은 말풍선 안이 아래부터 차오릅니다. 별도 수평 인내심 바를 사용하지 않습니다. 가격은 위쪽 코인 탭 하나입니다.
- 보석은 남은 턴 숫자, 스킬은 원형 쿨타임과 초 숫자로 구분합니다.
- 귀환 일부 소실의 예시 50%는 현재 `Assets/ReturnZone.prefab`의 `missedReturnLoss: 0.5`를 참조합니다. 실제 적용 단계에서는 런타임 값에 연결합니다.
- 최종 결과의 일차별 매출은 현재 집계되지 않으므로 그래프를 만들지 않았습니다. 정산 보석은 자동 적용이며 선택·구매 UI가 아닙니다.

## 파일

- `unity-concepts/`: 18개 생성 PNG와 생성·수정 프롬프트 `prompts.json`.
- `unity-reference/`: 현재 프리팹 캡처 37장과 RectTransform·문자 정보 `prefab-layouts.json`.
- `blood-beans-luigi-portable.html`: 이미지와 코드가 포함된 오프라인 갤러리.
- `claude-import.html`: 같은 갤러리 코드를 포함한 작은 HTML. 이미지는 기존 공개 갤러리에서 불러옵니다.
- `gallery-validation.json`: 갤러리 화면 선택·원본 비교·상태 전환·기존 캐릭터·오프라인 로딩 확인 결과.

이 파일들은 **시각 시안**입니다. 생성 이미지의 배경과 캐릭터는 콘셉트 표현이고 수치는 예시입니다. 게임의 씬·프리팹·스크립트는 수정하지 않았습니다. Unity 적용용 분리 아이콘·9-slice 패널·폰트·애니메이션 및 픽셀 정합성은 후속 최적화 단계에서 정리합니다.

기준 소스: `Assets/Art/UI/Prefabs/{Screen,Popup,Parts}`, `Assets/Scripts/Client/UI`, `Assets/Scripts/Night/ReturnZone.cs`, `Assets/ReturnZone.prefab`, 게임 기획서의 UI·귀환·정산 절.

사용 스킬: `unity-cli`, `ui-ugui`, `imagegen`, `sites:sites`.
