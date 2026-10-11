# Blood & Beans · 루이지 절제형 UI 구현 가이드

2026-10-11 · 현재 프로젝트 소스 기준. 이번 결과는 시안과 구현 계획이며 Unity 게임 에셋을 수정한 결과가 아닙니다.

## 1. 시각 방향

루이지의 분위기는 귀여운 3D 유령·몬스터, 둥근 글자, 짙은 초록, 크림색, 따뜻한 조명으로 유지합니다. 금속 장식과 양피지 무늬, 반복 문양, 두꺼운 입체 테두리, 상시 네온과 반짝이는 장식은 줄입니다. 상태를 알리는 색과 표시에는 의미를 부여합니다.

| 역할 | 제안 색 | 사용 원칙 |
| --- | --- | --- |
| 패널 | #233C35 | 무광 짙은 초록, 패널 하나당 얇은 테두리 하나 |
| 밝은 면·글자 | #E9DFCA | 크림색, 종이 질감은 거의 보이지 않을 정도 |
| 작은 구분선 | #9D8B64 | 낮은 채도의 황동색, 장식 프레임 금지 |
| 선택·준비·성공 | #8DBA8A | 전체 발광 대신 색 면 + 체크/선택선 |
| 주의 | #D1AF68 | 상태 라벨과 게이지에 제한 |
| 위험·손실 | #A64E45 | 색 외에 글자·아이콘도 함께 표시 |

이는 아트 작업 시작값입니다. 실제 게임 배경에서 대비를 확인해 조정합니다. 팀 색은 기존 `TeamColors` 원본을 따르고, UI 전체를 팀 색으로 칠하지 않고 이름표·팀 번호 등으로 제한합니다.

## 2. 시안 이미지를 실제 UI로 나누는 방법

시안 PNG 전체를 화면 배경처럼 붙이지 않습니다. 장면, UI 패널, 아이콘, 글자, 선택 상태, 수치를 분리합니다. 생성 시안의 문자·숫자는 모양 참고이며 실제 값과 문구는 기존 데이터에서 가져옵니다.

| 시안 요소 | Unity 구현 | 준비할 것 |
| --- | --- | --- |
| 카페·숲·선택 무대 | 기존 3D 씬/프리팹 + 카메라 | 기존 조명·재질 조정, 시안 배경은 구도 참고 |
| 패널·행·버튼 | uGUI Image + 기존 `UIRoundImage` | 단색 면, 반지름, 얇은 가장자리 |
| 특수 모양의 말풍선 | 스프라이트 + 기존 `UIBubbleFill` | 문자 없는 말풍선 알파와 채움 영역 |
| 이름·매출·타이머 | TextMeshProUGUI | 실제 문자열, 폰트 애셋, 머티리얼 프리셋 |
| 클래스·스킬·보석·가방 | Sprite + Image | 투명 배경 아이콘, 기본/선택/비활성 상태 |
| 플레이어·음료·재료 | 기존 실제 3D 모델 | `CharacterVisualConfig` / `ItemVisualConfig` 참조 |
| UI 안의 제품 미리보기 | 실제 프리팹을 렌더링한 정적 Sprite | 같은 카메라·조명으로 찍은 제품 썸네일 |
| 체크·선택선·상태 | 별도 Image/문자 + 상태별 색 | 기본 패널을 복제하지 않고 표시 전환 |

프로젝트는 이미 `UIRoundImage`와 `BB/UI Rounded Rect`를 사용합니다. 단순 패널은 이를 먼저 재사용합니다. 외곽 그림이 꼭 필요한 부품만 9-slice Sprite를 사용합니다. 초상이나 아이콘의 `Image.sprite`를 라운드용 스프라이트로 덮지 않습니다. uGUI Image는 Sprite를 사용합니다. [Unity Image 공식 문서](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-Image.html)

## 3. PC 크기·레이아웃

아래 수치는 1920×1080 기준 최종 화면 크기의 시작값입니다. 시안에 보이는 큰 비교용 표 제목을 게임 HUD에 그대로 사용하지 않습니다.

| 요소 | 제안 최종 크기 |
| --- | --- |
| 메뉴 제목 | 48–64 |
| 메뉴 버튼 글자 / 버튼 높이 | 26–30 / 48–56 |
| 본문·설명 | 22–24 |
| 보조 라벨·키 힌트 | 18–20 |
| HUD 타이머 | 36–44 |
| 작은 아이콘 / 밤 가방 | 32–40 / 48–56 |
| 화면 가장자리 여백 | 24–32 |
| 라운드 반지름 / 그림자 이동 | 8–12 / 1–2 |

현재 `UIThemeConfig.FontScale`과 `UIFontScale`이 프리팹 기본 글자 크기에 배율을 곱합니다. 위 수치를 그대로 기본 크기에 넣고 배율까지 다시 곱하면 UI가 커집니다. 기본 크기 = 목표 최종 크기 ÷ 실제 테마 배율로 맞추거나, 기존 배율 정책을 명시적으로 정리한 뒤 적용합니다. 제목·본문·HUD를 일괄 축소하는 방식은 피합니다.

CanvasScaler는 기존 설정을 먼저 확인하고 `Scale With Screen Size`, 기준 1920×1080, Match 0.5를 초기 후보로 사용합니다. 각 HUD는 화면 모서리·상단 중앙 앵커에 연결하고 중앙 메뉴는 가용 폭에 맞춥니다. 화면 크기와 앵커 조합이 해상도 대응의 기본입니다. [Unity 다중 해상도 공식 문서](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/HOWTO-UIMultiResolution.html)

21:9에서는 HUD만 가장자리로 이동하고 메뉴 폭은 제한합니다. 4:3에서 8명이 겹치면 캐릭터 배치를 4명씩 두 줄로 바꾸는 후보를 검토합니다. 전체 UI를 가로로 찌그러뜨리지 않습니다. 현재 시안은 16:9이며 이 두 해상도의 실제 Unity 적용 검증은 후속 작업입니다.

## 4. 서체

- 제목·주요 행동: 갤러리에 포함된 **Bagel Fat One**. 글자의 통통한 형태로 개성을 주고 과한 입체 효과는 줄입니다.
- 본문·설명·버튼: **Jua**. 설명문은 한 화면에서 필요한 길이만 표시합니다.
- 타이머·매출·판정 영문: **Coiny**는 숫자·영문 전용 후보입니다. 한글은 Bagel/Jua 폴백으로 표시합니다.

세 폰트는 갤러리 `fonts/`의 TTF와 OFL 고지를 같이 전달합니다. 브라우저 글꼴을 지정했다고 Unity TMP에 자동 적용되지는 않습니다. 프로젝트 폰트 폴더에 넣고 TMP Font Asset을 생성·배선해야 합니다.

고정 UI 문구·숫자·키 힌트는 Static SDF 애셋, 사용자 이름 등 예측 불가능한 한글은 Dynamic fallback으로 분리하는 후보를 사용합니다. 한글 전체를 무작정 작은 Static atlas 하나에 넣지 말고, 실제 표시 문자 집합으로 용량과 누락을 확인합니다. Dynamic fallback은 1024 atlas를 시작값으로 잡고 Clear Dynamic Data On Build를 켭니다. Font Asset Scale=1, Outline/Underlay는 공용 Material Preset, 상시 Glow는 끕니다. 타이머 AutoSize는 끄고 고정 크기로 맞춥니다. 설치된 uGUI 2.5.0의 TMP 소스에서 atlasPopulationMode, fallbackFontAssetTable, isMultiAtlasTexturesEnabled와 clearDynamicDataOnBuild를 확인했습니다.

## 5. 기존 코드에서 재사용할 연결

`BB.Client → BB.Game → BB.Rules`의 방향을 유지합니다. 화면은 프리팹의 배치와 표시만 소유하고 매출·인내심·판정·중복 선택 규칙을 다시 계산하지 않습니다. UI 리스킨 때문에 새로운 RPC나 서버 수치를 만들 필요는 없습니다.

| 화면/부품 | 현재 연결 | 적용할 내용 |
| --- | --- | --- |
| 타이틀 | UITitleMenuScreen | 세로 메뉴, 로고·배경 분리, 선택 표시 |
| 방 목록 | UIRoomListScreen | 방 행·선택 상세·2–4팀 조절·입장 버튼 |
| 캐릭터·팀 선택 | UICharacterSelectScreen / CharacterStage | 8인 무대·팀 라벨·하단 클래스·준비 표시 |
| 보조 대기실 | UIRoomScreen | 팀별 2인 카드, 현재 보조 흐름 유지 |
| 낮 HUD | UIMatchHudScreen / MatchHudPresenter | 순위·시계·매출·스킬·보석, 낮 가방 숨김 |
| 조합법 | UIMatchHudScreen / UIRecipeRow | 커피·디저트 열, 행 재사용, F1 열기/닫기 |
| 주문·인내심 | UICustomerOrder / UIBubbleFill | 말풍선 내부 채움·가격 탭·경고 상태 |
| 제조·완성 판정 | UIMakingCard / UICompletionGauge / UICraftingJudgement | 식기 추적 카드·판정 구간·판정 피드백 |
| 밤 HUD | UIMatchHudScreen / MatchHudPresenter | 무게·적재 경고·밤 가방·스킬·상호작용 |
| 상자 루팅 | UIBoxLootPopup / UIBoxLootSlot | 기존 공개 순서와 다섯 칸, 툴팁 |
| 가방·스킬·귀환 부품 | UIBagStatus / UISkillSlot / UIKeyHint | 상태별 아이콘·쿨타임·키 표시 |
| 귀환 결과 | UIReturnResultPopup | 성공·일부 소실·분실 토스트 |
| 하루 정산·예보 | UIDaySettlementScreen | 정산·순위·자동 보석·손님 예보 |
| 최종 결과 | UIMatchResultPopup | 현재 집계하는 순위·매출·버튼 |
| 설정 | UISettingsPopup / UIVolumeSlider | 음량·전체 화면·조건부 감도·닫기 |
| 로딩 | UILoadingPopup | 불러오는 중 / 확인된 접속 인원 |
| 페이즈 전환 | UIPhaseCuePopup | 3·2·1 / READY / GO / 마감 |
| 공통 부품 | UICharacterCard / UITeamSlot / UIStandingRow 등 | 공통 색·서체·선택·비활성 규칙 |

이 목록의 실제 프리팹 경로는 `Assets/Art/UI/Prefabs/`입니다. 일반 지침의 예시 폴더로 이동하지 않습니다. 스크립트는 `Assets/Scripts/Client/UI/`에 있습니다. 기존 SerializeField 참조를 보존한 채 배치와 시각 부품을 변경합니다.

## 6. 다인 캐릭터 선택의 추가 작업

현재 `CharacterStage`는 이미 최대 8개의 실제 모델과 이름표 좌표를 지원합니다. 무대는 Canvas 자식이 아니라 별도 3D 객체와 전용 카메라로 둡니다. 화면은 `SetRoster`, `SetLobby`, `SetClaims`와 `UICharacterNameplate`를 재사용합니다.

다만 현재 배치는 **SelfSeat=0 중심으로 좌우 번갈아** 놓이고 카메라가 내 자리를 따라갑니다. 새 시안의 4팀×2명 그룹은 색만 바꿔서는 나오지 않습니다. 표현 전용 좌석 매핑을 만들고 팀·사용자 ID로 안정된 표시 순서를 정한 뒤, 전체 8인을 담는 카메라 구도와 팀 라벨을 적용해야 합니다. 서버 좌석 ID와 화면 표시 위치는 구분합니다. 입장·퇴장 때 내 캐릭터와 선택 상태가 다른 사람에게 옮겨가지 않는지 확인합니다.

4개 몬스터 외형과 5개 클래스는 서로 다른 기준입니다. 현재 `CharacterVisualConfig`는 CharacterId에 연결된 모델을 고릅니다. 외형을 클래스별로 교체하는 것과 플레이어가 클래스와 별개로 4종 스킨을 선택하는 기능은 다릅니다. 후자는 별도 외형 선택 데이터/복제 검토가 필요하며, 이번 시안만으로 구현됐다고 보지 않습니다.

## 7. 주문·제조 오브젝트

`UICustomerOrder`는 `patience.Show(1 - PatienceRatio, color)`로 이미 사용한 인내심을 말풍선 안에 아래부터 채웁니다. `UIBubbleFill`의 공용 머티리얼과 정점 데이터를 재사용합니다. 별도 인내심 게이지를 새로 만들지 않습니다. 말풍선 Sprite가 바뀌면 투명 여백과 채움 range도 다시 맞춥니다. 가격은 기존 합계 탭 하나를 사용합니다.

실제 음료·컵·재료는 `ItemVisualConfig`와 `ItemSlotPresenter`의 기존 메시/재질을 사용합니다. UI 아이콘도 같은 프리팹을 일정한 카메라와 조명으로 오프라인 렌더링해 만들면 장면 오브젝트와 같은 형태가 됩니다. 각 주문·아이콘에 상시 Camera/RenderTexture를 붙이는 방식은 초기안에서 피합니다. 상세 회전 미리보기가 실제 요구된 곳에만 제한적으로 사용합니다.

`UIMakingCard.Render(CarryView)`는 현재 상태와 아이콘을 표시합니다. `glowAlpha`는 절제형에서 낮추되 블러드 빈의 공개 정보와 준비/실패 구분은 작은 상태 면·라벨로 유지합니다. 제조 시안의 다섯 카드는 상태 비교표이며 실제 게임에 다섯 장을 동시에 띄우는 제안이 아닙니다. `UICompletionGauge`의 Good/Perfect 폭과 침 위치는 기존 CompletionGauge 값을 그대로 받아야 합니다. 그림의 녹색 영역 비율로 판정 수치를 새로 정하지 않습니다.

## 8. 공통 에셋 제작 목록

먼저 공용 기본 면·버튼·선택선·말풍선·상태 라벨·키 힌트·클래스 카드·행을 확정합니다. 기본/선택/호버/눌림/비활성은 색·작은 위치/스케일 변화로 만들고 상태마다 큰 PNG를 별도로 복제하지 않습니다. 반복 조각은 기존 Parts 프리팹과 해당 컴포넌트를 사용합니다.

| 산출물 | 분리 조건 |
| --- | --- |
| 로고 | 투명 PNG, 글자/배경 분리, 메뉴 문구 제외 |
| 말풍선 | 문자·컵 없는 알파 Sprite, 가격 탭 별도 |
| 가방 | 닫힘/열림 2종, 묻음은 기존 상태 표시, 그림자 과장 없음 |
| 클래스·스킬·보석 | 같은 실루엣/명암 규칙, 투명 배경 |
| 제품 썸네일 | 실제 모델 동일 형태, 동일 앵글·크기·조명 |
| 폰트 | TTF + OFL + TMP main/fallback + 공용 Material Preset |
| 프리팹 | 기존 참조를 보존한 Parts/Screen/Popup 리스킨 |

텍스처는 최종 표시 크기 기준으로 제작합니다. 큰 시안에서 억지로 잘라 작은 아이콘으로 쓰기보다 글자 없는 개별 원본을 뽑습니다. Mipmap·필터·압축은 실제 UI 크기에서 비교하며, 텍스트는 이미지에 굽지 않습니다.

## 9. 적용 순서와 확인

1. 공통 부품·서체·팔레트 확정 → 기존 UIThemeConfig와 공용 머티리얼/Parts 적용.
2. 낮 HUD·주문·제조 → 실제 플레이에서 가장 작은 글자와 겹침 먼저 확인.
3. 밤 HUD·루팅 → 밤에만 가방 표시, 적재·묻음·쿨타임 모든 상태 연결.
4. 8인 캐릭터 선택 → 1/2/4/8인, 준비/퇴장/중복 픽·카메라·이름표 연결.
5. 메뉴·정산·공통 팝업 → 공통 부품 재사용, 화면 스택과 입력 소유 유지.
6. 후속 적용 검증 → 1920×1080, 2560×1080, 1600×1200; 긴 한글 이름·누락 글리프·부족 재고·손실·탄 것·로딩 상태; 실제 빌드에서 Canvas rebuild, 배칭, 오버드로 확인.

성능 목표 숫자는 측정 전 확정하지 않습니다. 동적 텍스트/표식은 갱신 범위를 좁히고 변화가 없으면 재설정하지 않습니다. 장식 Image의 Raycast Target은 끄고 실제 입력 요소만 켭니다. 많은 개별 Material, 중첩 Mask와 상시 UI 파티클을 줄입니다.

## 10. 확인한 근거와 남은 작업

현재 Unity 6000.5.9f1, uGUI 2.5.0, URP 17.5.0, Input System 1.20.0, NGO 2.13.1입니다. `UIThemeConfig`, `UITheme`, `UIFontScale`, `UIRoundImage`, `UIBubbleFill`, `UICustomerOrder`, `UIMakingCard`, `UICompletionGauge`, `UIMatchHudScreen`, `MatchHudPresenter`, `CharacterStage`, `UICharacterSelectScreen`, `CharacterVisualConfig`, `ItemVisualConfig`와 설치 패키지 소스를 확인했습니다.

새 시안은 이미지 기반 콘셉트입니다. 제품/캐릭터는 3D 표현을 유지하지만 실제 프로젝트 모델과 완전히 동일한 픽셀은 아닙니다. 실제 프리팹 비교는 갤러리의 「현재 구현 · 샘플 화면」과 「실제 3D 오브젝트」를 기준으로 삼습니다. 이번에 Unity 에셋 적용·빌드·게임 QA·다중 해상도 검증은 수행하지 않았습니다. 실제 적용 시 위 순서와 확인 항목으로 진행합니다.
