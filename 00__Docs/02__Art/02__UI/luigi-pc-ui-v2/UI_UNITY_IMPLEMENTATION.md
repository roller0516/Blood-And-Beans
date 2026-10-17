# 루이지 콘셉트 UI · 실제 Unity 적용

2026-10-18. 기존 게임 프로젝트에 화면 프리팹, 공통 외형, 실제 TMP와 상태 표시를 적용했습니다.

`blood-beans-luigi-portable.html`의 **Unity 실제 적용**에서 결과를 확인합니다. 기존 콘셉트 탭은 비교 자료로 유지합니다. 온라인 사이트와 Claude 아티팩트는 이번 단계에서 갱신하지 않습니다.

## 적용 기준

- 캐릭터·팀 선택: 기본 루이지 시안. 큰 실제 3D 선택 캐릭터, 우측 참가자 8칸, 상세 정보, 하단 캐릭터 카드와 팀 선택입니다.
- 나머지: 현재 시안의 초록 가죽·크림 종이·황동 프레임·입체 글자 방향입니다. 게임 데이터와 조작은 기존 구현을 사용합니다.
- 낮·밤의 게임 카메라 리그, 게임 씬과 월드 프랍은 교체하지 않았습니다. 캡처를 위해 기존 마우스 시점과 서버 이동 기능을 사용했습니다.
- 캐릭터 종류는 현재 `CharacterCatalog`의 뱀파이어·좀비·늑대인간·마녀 4종입니다. 초상은 현재 3D 모델을 Unity에서 렌더링했습니다.
- 시안 이미지의 가격이나 버튼을 게임 규칙으로 복사하지 않았습니다. 레시피와 판매 예상가는 기존 데이터 표를 사용하며, 연결할 기능이 없는 재대전 버튼은 감춥니다.

## Unity에서 확인

같은 프로젝트의 `Assets/Scenes/Title.unity`를 실행하면 적용된 화면을 사용합니다. 별도 설치나 런타임 UI 생성 도구는 필요하지 않습니다. 리소스 주소와 기존 화면 프리팹의 GUID를 유지했습니다.

| 갤러리 항목 | 실제 자산 / 연결 |
| --- | --- |
| 타이틀 | `Screen/UITitleMenuScreen` · 기존 시작/설정/종료 이벤트 |
| 방 목록 | `Screen/UIRoomListScreen` · 실제 방 목록, 스크롤, 팀 수, 생성/참가 |
| 캐릭터·팀 | `Screen/UICharacterSelectScreen` + `UICrewRosterCard` + 기존 `CharacterStage` |
| 대기실 | `Screen/UIRoomScreen` · 실제 팀/참가자/준비 상태 |
| 낮 HUD | `Screen/UIMatchHudScreen` · 순위, 시계, 매출/목표, 스킬 |
| 조합법 | 같은 HUD의 RecipePanel + `UIRecipeRow` · 메뉴/가격/재고/인기 재료 |
| 주문·인내심 | `UICustomerOrder` · 기존 말풍선 전체의 아래부터 채움, 가격 탭 |
| 제조·판정 | `UIMakingCard`, `UICompletionGauge`, `UICraftingJudgement` |
| 밤 HUD | 같은 HUD · 가벼운 가방 Sprite, 무게, 쿨타임, 안내 |
| 상자 | `Popup/UIBoxLootPopup` + `UIBoxLootSlot` · 공개/가림/빈 슬롯 |
| 밤 부품 | 기존 HUD의 캐스팅/귀환 표식/가방 상태 |
| 귀환 | `Popup/UIReturnResultPopup` · 성공/일부 손실/가방 분실 |
| 정산 | `Screen/UIDaySettlementScreen` · 실제 매출/임대료/순위/보석/예보 |
| 최종 결과 | `Popup/UIMatchResultPopup` · 실제 순위와 로비 복귀 |
| 설정 | `Popup/UISettingsPopup` · 기존 음량/감도/전체 화면/닫기 |
| 로딩 | `Popup/UILoadingPopup` · 실제 대기 인원 상태 |
| 페이즈 안내 | `Popup/UIPhaseCuePopup` · 기존 전환/카운트다운 |
| 공통 부품 | `Assets/Art/UI/Prefabs/Parts` |

표의 프리팹 경로는 `Assets/Art/UI/Prefabs/` 아래입니다. 낮에는 가방이 표시되지 않습니다. 주문 말풍선에는 제품 이름을 추가하지 않았으며, 기존 인내심 셰이더와 판정을 유지했습니다.

## 재사용 구조

```text
기존 Screen / Popup 프리팹
├ 기존 UIView · Presenter · 버튼 이벤트 · 데이터 참조
├ UISurfacePart 변형의 중첩 인스턴스
│  └ Paper / Green / Selected / Red / Cream / Disabled / Header
├ 실제 TMP: Title / Body / Number + 공유 Material
└ 기존 용도별 부품
   ├ UIRecipeRow · 메뉴마다 내용만 변경
   ├ UIMakingCard · 제조/완성/쓰레기/탄 상태
   ├ UIStandingRow · 순위와 팀 정보
   └ UICrewRosterCard · 참가자/준비/자기 표시
```

공통 프레임과 버튼은 `UISurfacePart.prefab`을 기본으로 한 실제 Prefab Variant입니다. 원래 화면과 부품의 입력·데이터 컴포넌트를 유지하고 외형을 중첩했습니다. 값이 바뀌는 배경은 원래 Image를 유지하며 테두리를 별도로 겹칩니다.

외형 PNG는 `Assets/Art/UI/Sprites/Common/Luigi*.png`, 타이틀 배경은 `Sprites/Title/LuigiTitleBackdrop.png`입니다. 캐릭터 초상은 `Sprites/CharacterSelect/LuigiCrew*.png`입니다.

## 3D를 사용하는 범위

큰 선택 캐릭터는 현재 프로젝트의 실제 3D 모델입니다. 선택한 캐릭터 한 명을 무대에 표시하고, 참가자 카드에는 512px 투명 초상을 사용합니다. 이전 3D 클립보드 원형은 `PrototypeKit/UIClipboard3DAssembly.prefab`에 보존했습니다.

현재 고정된 HUD와 클립보드는 실제 Sprite + 9-slice Image + TMP로 표시합니다. 클립보드마다 실시간 카메라나 RenderTexture를 두지 않습니다. 현재 PNG 외형을 자유 회전하는 3D 모델로 만들어 둔 상태는 아니며, 그런 연출을 추가할 때는 별도 모델 작업이 필요합니다.

## 최적화 반영

- 같은 Sprite·Material·중첩 프리팹을 공유합니다. 반복 행은 기존 생성/재사용 경로를 사용합니다.
- `LuigiCommon` Atlas: PC BC7 압축, 최대 2048, 회전/타이트 패킹/밉맵 없음. 기존 UI Addressables 그룹에 등록했습니다.
- 자주 쓰는 제목·본문·숫자는 정적 TMP Atlas입니다. 미리 포함하지 않은 닉네임/문구는 역할별 동적 폴백으로 보완합니다.
- 실제 폰트는 써라운드, 도현, Lilita One입니다. 원본 TTF와 OFL 문서는 `Assets/Art/UI/PrototypeKit`에 있습니다.
- 원래의 전역 1.9배 글자 확대는 적용본에서 1배로 설정하고, 프리팹에 최종 표시 크기를 저장했습니다.
- 새 장식 Image는 레이캐스트를 받지 않습니다. 새 공통 부품과 참가자 카드에 매 프레임 탐색/갱신 루프를 추가하지 않았습니다.
- 기존 CanvasScaler의 1920×1080 / Expand 기준과 화면 가장자리 앵커를 사용합니다.

FPS나 CPU/GPU 개선 수치를 측정해 확정한 결과는 아닙니다. 최종 플레이어 빌드, Addressables 콘텐츠 빌드, 다중 PC 네트워크와 장시간 프로파일링은 이번 확인 범위 밖입니다.

## 확인 결과와 갤러리 이미지

- Unity 컴파일 및 최종 로컬 플레이의 새 오류 없음.
- 실제 버튼으로 설정 열기/닫기, 방 목록, 로컬 방 생성, 캐릭터/팀 변경, 매치 시작을 확인했습니다.
- 실제 매치에서 낮 가방 숨김, 밤 가방 표시, 조합법 열기를 확인했습니다.
- 프리팹 43개, 공통 Surface 인스턴스 108개를 참조·TMP·Atlas 설정 검사했습니다. 결과는 `ui-unity-implementation-validation.json`입니다.
- 실제 타이틀을 1920×1080, 2560×1080, 1600×1200에서 확인했습니다. 모든 화면의 모든 해상도 조합을 검증했다는 뜻은 아닙니다.
- 현재 갤러리의 `unity-implementation/implemented-title`, `crew`는 실제 Game View 캡처입니다. 나머지는 새 폰트·프레임을 반영한 저장 프리팹에 검토용 상태를 입력해 Unity에서 렌더링했습니다. 일부 검토 렌더의 배경은 기존 환경 참고 이미지이며 게임 카메라를 변경한 결과가 아닙니다.

HTML에 표시되는 결과도 같은 실제 에셋을 사용합니다. 전체 화면을 한 장의 배경 이미지로 게임에 붙이는 방식이 아닙니다. PNG → 무손실 WebP 변환 후 portable HTML에 내장합니다.

## 제작 레시피

Unity CLI `run_script`의 클래스 진입점입니다. 에디터 모드에서 순서대로 실행합니다. 기존 프리팹을 갱신하므로 Inspector에서 수동 조정한 뒤에는 전체 레시피를 무심코 재실행하지 않습니다.

1. `BuildLuigiProductionAssets.Main` · Sprite 슬라이싱, TMP, 공통 Surface, 참가자 카드, Atlas.
2. `BuildLuigiCrewPortraits.Main` · 현재 모델의 초상과 ResourceManager 키.
3. `ApplyLuigiProductionUI.Main` · 기존 화면/팝업/부품의 배치와 참조.
4. `RefineLuigiTypography.Main` · 공유 TMP의 크림 그라데이션·갈색 외곽·베벨·아래 그림자, 짧은 HUD 표제 서체. 타이틀 영문 로고는 Lilita One을 사용합니다.
5. `RefineLuigiHudFrames.Main` · `LuigiHudTrim.png` 슬라이싱, 시계·매출·순위·스킬의 실제 프레임과 배치.
6. `ValidateLuigiProductionUI.Main` · 제작 범위 리소스 검사.
7. `CaptureLuigiProductionScreens.Main` · 상태 비교 렌더. 실제 Play 캡처는 별도로 보존합니다.

프레임/버튼/가방/타이틀 배경은 문자 없는 실제 제작용 래스터 에셋으로 생성했습니다. 기존 시안의 초록 가죽, 황동, 크림 종이와 가벼운 천 가방을 제작 브리프로 사용했습니다. 동적인 글자·아이콘·상태는 PNG에 굽지 않았습니다.

Git에는 실제 Unity 에셋의 `.meta`와 새 스크립트, Addressables UI 그룹 변경도 함께 포함해야 합니다. portable HTML과 assets.zip만 ignore하는 것은 게임 실행에 영향을 주지 않습니다.

사용한 스킬: unity-cli, unity-architecture, ui-ugui, optimize-text-mesh-pro, sprite-editor, imagegen. Graphify/Ponytail 연결은 사용할 수 없어 파일 검색과 Unity의 실제 직렬화 참조로 확인했습니다.

## 폰트·HUD 외형 보강

실제 로컬 실행에서 TMP의 패밀리 이름은 Cafe24 Ssurround / Do Hyeon / Lilita One으로 확인했습니다. 기본 폰트로 바뀌는 문제는 확인되지 않았고, 짧은 HUD 문구에 본문 서체를 쓰며 깊이 표현이 약했던 차이를 수정했습니다. 공용 머티리얼과 343개 프리팹 TMP 설정을 갱신했습니다. 긴 설명에는 본문 서체를 유지합니다.

보강한 HUD는 종전의 단순한 사각 테두리 대신 초록 알람시계가 붙은 가로 시계판, 동전이 붙은 매출판, 순위판, 파란 광점의 원형 스킬 테두리를 사용합니다. 스킬·키캡의 스트레치 및 중첩 프리팹 오버라이드를 함께 정리했습니다. 새 카메라/RenderTexture/매 프레임 효과 코드는 추가하지 않았습니다. TMP의 공유 Distance Field 셰이더를 사용하므로 최종 GPU 비용은 프로파일링으로 확인해야 합니다.

제작용 이미지 경로: `Assets/Art/UI/Sprites/Common/LuigiHudTrim.png`. 내장 imagegen으로 생성한 투명 원본을 그대로 복사했고 Sprite Editor가 4종으로 슬라이싱합니다. 생성 브리프: 기존 낮 시안을 스타일 참고로, 문자 없는 정면 3D 황동·검게 산화된 청동·짙은 초록 에나멜 프레임 4종(알람시계 / 순위 행 / 동전 / 투명 구멍의 원형 스킬 베젤), 투명 배경과 분리된 셀. 실제 글자·수치는 이미지에 넣지 않았습니다.
