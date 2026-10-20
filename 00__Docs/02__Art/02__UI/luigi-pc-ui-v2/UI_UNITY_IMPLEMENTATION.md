## 2026-10-20 — 방 대기실과 실제 서체 재수정

이전 결과의 큰 바깥 패널·중첩 슬롯·폰트가 승인된 `light-ui-preview/room.png`와 달라 기존 `UIRoomScreen`부터 다시 대조했다. 갤러리의 시안 이미지는 변경하지 않았다. 화면별 배치 재수정은 이번에 방 대기실을 대상으로 했고, 제목·본문 폰트 연결은 기존 UI 프리팹 전체에 적용했다. 다른 화면의 미술 배치까지 시안과 일치했다고 보고하는 것은 아니다.

- `UIRoomScreen.prefab/Panel`: 큰 전체 프레임을 숨기고 중앙 2×2 팀 카드, 좌상단 제목·안내, 우상단 준비 인원, 양쪽 하단 액션으로 배치했다. `UIRoomTeamCard`와 `UICrewRosterCard`의 GUID와 중첩 프리팹 연결을 유지했다. 팀 카드의 두 초상 아래 이름과 준비/대기 상태를 분리했다. 상태 표시의 원본은 기존 공용 `UICrewRosterCard.prefab` 안에 한 번만 두고 방 카드에서 활성화한다.
- `UIRoomTeamCard.cs`: 기존 팀 이름·선택·참가자 렌더링에 팀 번호와 색 띠 참조를 추가했다. `UICrewRosterCard.cs`: 선택적인 준비 상태 텍스트/배경 참조와 색·문구를 직렬화했다. 기존 작은 참가자 카드에서는 이 상태 행을 숨긴다. `UIRoomScreen.cs`의 준비 액션 문구도 직렬화했으며 기존 `Bind`·로비 콜백·시작 가능 조건은 유지한다.
- `Assets/Art/Fonts/LuigiLettering SDF.asset`: 공식 원본 ONE Mobile POP으로 만든 실제 TMP 서체다. 제목과 본문은 같은 정적 2048 아틀라스 1장/790자와 두 재질을 공유한다. 추가 이름은 원본 TTF가 연결된 1024 Dynamic 폴백으로 처리하고 Clear Dynamic Data On Build=true다. 기존 Lilita One 숫자 아틀라스는 유지하되 한글 폴백을 새 서체로 연결했다. 생성 시안의 글자와 동일한 원본 폰트를 찾아낸 것은 아니다.
- `UIRoundedRect.shader`: 기본값 0인 유리 하이라이트 옵션만 추가했다. 방 전용 공유 재질은 얇은 연보라 한 겹의 선과 낮은 반사광을 사용한다. 기존 다른 재질의 기본 동작은 유지한다.
- `Title/LuigiRoomBackdrop.png`: 승인된 방 시안에서 UI와 카드 캐릭터를 제거해 만든 배경 전용 그림이다. 화면 전체를 이미지로 덮어 구현한 것이 아니며 텍스트·초상·상태·입력은 개별 Unity UI다. AspectRatioFitter로 원본 비율을 유지한다. CharacterStage의 3D 배경·모델은 수정하지 않았다. 출처와 전체 프롬프트는 `ui-room-lettering-provenance.json`, 폰트 고지는 `Assets/Art/Fonts/ONE-Mobile-POP-NOTICE.txt`에 있다.

실행 확인: 저장된 실제 프리팹과 통제된 8명 명단으로 방 화면을 1920×1080, 2560×1080, 1600×1200에서 캡처했다. 방·방 목록·설정에서 TMP 잘림이 없었다. 공용 준비 표시를 정리한 뒤 1920×1080 결과가 동일한 것도 확인했다. 신규 게임 스크립트 컴파일 오류는 없으며 과거 도구 Refresh 타임아웃 로그는 남아 있다. 실제 Steam 방 생성·다중 PC 통신 확인이나 모든 화면의 최종 미술 검수는 이번 기록에 포함하지 않는다. `UIRoomScreen`은 기존 대기실/폴백 화면이며 기본 캐릭터 선택 흐름을 새로 끼워 넣지 않았다.

실제 캡처: `light-ui-preview/native-captures/room-lettering-final.png`, `room-lettering-wide.png`, `room-lettering-43.png`. 재현 도구 `RefineLuigiRoom.cs`의 실행 순서는 Main → PromoteFonts → ShareStatusPart다. 과거 전체 리스킨 스크립트를 일괄 실행하지 않는다.

사용 스킬: bb-art-direction, unity-cli, ui-ugui, optimize-text-mesh-pro, unity-architecture, sprite-editor, imagegen. Ponytail과 engineering 플러그인은 제공되지 않아 기존과 같이 파일 검색·Unity 컴파일·실행 화면으로 확인했다.

## 2026-10-19 — 단순화한 외형의 시안 대응 보정

사용자 피드백: 이전의 단색 둥근 판은 시안보다 지나치게 간소화됐다. 기준 이미지는 canonical portable HTML의 light 시안이며, 특히 `light-ui-preview/night.png`와 `shared.png`의 유리 표면·반사광·입체 키캡·원형 스킬·작은 갈색 배낭을 실제 부품으로 구현한다. 아래의 이전 단색 셰이더 적용 절을 현재 외형으로 간주하지 않는다. 시안 HTML/PNG는 변경하지 않았다.

- `Assets/Art/UI/Sprites/Common/LuigiGlassKit.png`: 반사광과 부드러운 알파 발광을 가진 공용 유리 패널·버튼·카드·키캡·원형·달·게이지. 배경과 문자를 합친 화면 이미지가 아닌 12개 독립 Sprite다. 확장 부품은 9-slice이며 UI/Default 재질을 공유한다. 기존 `LuigiCommon.spriteatlasv2`에 추가했고 Standalone BC7 / 2048 / 회전·타이트 패킹·밉맵 없음 설정을 유지했다. 메모리·드로콜 개선치를 측정한 것은 아니다.
- `LuigiBagStates.png`: 기존 에셋 GUID와 3개 Sprite ID를 보존하고 닫힘/열림/내려놓은 그림을 시안의 작은 갈색 배낭으로 교체했다. HUD의 두 상태 참조는 서로 다른 Sprite다. 이 그림은 입체적으로 보이는 2D UI 에셋이며 월드 배낭 모델을 수정한 것이 아니다.
- `UISurfacePart` 계열과 기존 카드·행·슬롯·화면의 Sprite/재질/겹침 순서를 수정했다. 새 화면 프리팹은 없다. `UIRoundImage.OnValidate`는 비활성 컴포넌트가 새 Sprite의 기본 재질을 다시 둥근 판 재질로 덮어쓰지 않게 했다. 고유 주문 말풍선/쿨타임/판정 게이지 셰이더는 유지한다.
- 밤 `UIMatchHudScreen`: 좌상단 실제 선택 캐릭터 초상과 매출, 중앙 달 배지·날짜·시계, 우상단 실제 팀 색 점, 좌하단 스킬 원형·키캡·실제 스킬 이름, 우하단 배낭·퍼센트·가로 게이지로 구성했다. 기존 가방 미소지 X/도움말과 획득 연출 앵커는 보존했다. 상호작용 F 키캡과 진행 게이지는 별도 Graphic이며 실제 값으로 갱신된다.
- 설정 `UIVolumeSlider`/`UISettingsPopup`: 반투명 행, 입력 칸, 원형 손잡이, 우측 전체 화면 체크와 기존 적용 버튼을 수정했다. 슬라이더→숫자 37, 직접 입력 42→0.42 갱신을 실행 확인했고 Apply 전 영구 오디오/디스플레이 설정은 쓰지 않았다.
- 팀·캐릭터 선택: 선택 프레임이 이름을 덮던 순서를 수정하고 하단 이름 강조와 선택/비선택 글자 대비를 분리했다. `UITeamSlot`은 기존 팀 색 띠와 선택 상태를 사용한다. CharacterStage의 실제 3D 배경·카메라·모델은 이번 보정에서 수정하지 않았다.
- 낮 순위 행·시계, 제조 카드, 방 목록·준비/입장 버튼, 정산 등도 기존 공용 파츠를 통해 동일한 유리 표면을 사용한다. 실제 규칙, 로비, 네트워크 흐름을 변경하지 않았다. 버튼의 hover/focus/press는 공유 Sprite 또는 ColorTint로 표시하며 기존 클릭 콜백을 유지한다.
- 서체는 기존 Ssurround / DoHyeon / LilitaOne이며 UI 전용 `.mat` 프리셋의 흰 면·짧은 보라 음영·약한 베벨만 조정했다. 폰트 원본 내부의 기본 Material은 원래 값으로 복원했다. 이름·숫자는 이미지에 굽지 않는다.

기록: `ui-glass-fidelity-provenance.json`, `light-ui-preview/fidelity-validation.json`, `prefab-fidelity-before.json`/`prefab-fidelity-after.json`, `runtime-state-checks.json`의 `fidelity-` 항목. 저장 자산 검사 대상은 기존 53개이며 새로 들어온 `UIMessagePopup`은 직접 수정하지 않았다. 전후 54개 전체 인벤토리의 GUID가 같고 신규 화면 프리팹은 없다. 실행 캡처는 `native-captures/fidelity-*.png`다. HUD 캡처는 저장된 실제 UI와 샘플 값을 이전 Unity 카메라 캡처 위에서 표시한 결과이며 새 네트워크 매치 실행 기록이 아니다. 갤러리의 생성 배경과 실제 게임 배경의 미술 차이까지 픽셀 단위로 동일하게 만든 결과로 보고하지 않는다.

제작 스크립트 `ApplyLuigiGlassFidelity.cs`는 작업 이력이다. 과거 authoring 스크립트와 함께 일괄 재실행하지 않는다. 런타임에서 UI를 새로 생성하는 구조가 아니며 저장된 기존 프리팹과 현재 데이터 바인딩을 사용한다.

최종 저장 검사: 53개 프리팹 / 공유 Surface 148개 / TMP 382개, 끊어진 참조·누락 폰트·이중 라운드 적용 오류 0. 전후 전체 54개 GUID 불일치 0. 실행 입력 11건의 해당 최종 상태에서 텍스트 overflow 0(밤 일반·230% 과적·미소지·쿨타임, 밤 4:3/울트라와이드, 낮·루팅·캐릭터 선택·설정·정산). 앞서 발견한 미소지 도움말 i와 밤 캡션 영역 초과는 수정 후 다시 확인했다. 33개 기존 버튼의 표시 타깃을 공용 Sprite로 연결했고 방 목록 새로고침 버튼에서 pointerEnter→HoverButton, pointerExit→GlassButton 복귀를 확인했다. 마지막 Console cursor 3050→3095 구간의 새 Error 0 / compilationFailed=false. 이는 샘플 UI 동작 확인이며 미술의 픽셀 일치 판정이나 전체 멀티플레이·Player 성능 검증을 뜻하지 않는다. Unity는 원래처럼 정지 상태로, Game View는 기존 선택으로 돌려 두었으며 캡처 임시 자산은 삭제했다.

사용 스킬: bb-art-direction, unity-cli, unity-architecture, ui-ugui, sprite-editor, optimize-text-mesh-pro, imagegen. Ponytail/engineering 스킬은 제공되지 않아 사용하지 않았다.

## 이전 적용 — 승인된 가벼운 시안의 Unity 적용

기준은 `light-ui-preview/`의 보라색 경량 시안입니다. 기존의 황동·종이·가죽 외형을 이 방향으로 수정했습니다. 이 절 아래의 이전 제작 기록과 구분합니다. 갤러리 HTML의 승인 시안 이미지를 이번 작업으로 교체하지는 않았습니다.

- **기존 프리팹 재사용**: `Assets/Art/UI/Prefabs/`의 53개 GUID를 유지했습니다. `UISurfacePart` 인스턴스 149개와 기존 카드·행·슬롯을 재사용합니다. 새 Screen 루트는 없습니다.
- **공용 외형**: `Assets/Art/Shaders/UIRoundedRect.shader`에 선택적인 얇은 외곽선·내부 투명도·상태 색 반영을 추가했습니다. 기본값은 기존 셰이더 동작입니다. `Assets/Art/Environment/Materials/UI_Luigi*.mat` 여섯 프리셋을 공유하며 위젯마다 머티리얼을 생성하지 않습니다.
- **서체**: 기존 Cafe24Ssurround / DoHyeon / LilitaOne TMP 리소스를 유지하고 금색 그라데이션·두꺼운 베벨을 흰 글자와 보라색 음영으로 바꿨습니다. AutoSize는 사용하지 않습니다. 완성 크기로 작성한 화면은 UIFontScale의 fixedSizeRoots로 중복 배율을 제외합니다.
- **배치**: 타이틀·방 목록·설정·캐릭터 선택·낮/밤 HUD·제조 카드·정산·결과 화면의 패널과 글자 영역을 조정했습니다. 낮 순위는 내 팀 + 다른 세 팀의 작은 행, 제조는 낮고 넓은 카드, 정산은 네 패널로 구성했습니다. 실제 여섯 보석은 2열·3행으로 표시합니다.
- **팀 선택**: `UITeamSlot.cs`의 선택 표시 참조를 추가했습니다. 배경 색 참조는 작은 TeamAccent로 옮기고, 내 선택은 공유 연두 외곽선으로 분리했습니다. 클릭·로비·팀 배정 로직은 유지합니다.
- **메뉴 배경**: 승인된 문자 없는 `light-ui-preview/background.png`를 기존 메뉴 배경 Sprite의 GUID를 유지해 적용했습니다. **CharacterStage는 기존 3D 카페·모델·카메라를 그대로 사용하며, 시안 이미지로 덮지 않았습니다.** 실제 낮/밤 카메라와 월드 모델도 변경하지 않았습니다.

### 확인 기록

사용 스킬: `bb-art-direction`, `unity-cli`, `ui-ugui`, `optimize-text-mesh-pro`, `sprite-editor`, `unity-architecture`.

`light-ui-preview/native-validation.json`: 저장 프리팹의 Missing Script, 끊어진 직렬화 참조, 폰트·공용 표면·셰이더 오류 확인입니다. 전체 프로젝트 QA나 네트워크 매치 검증이 아닙니다.

`light-ui-preview/reference-comparison.json`: 변경 전/후 GUID와 원래 UI 참조 대조입니다. 의도한 참조 변경은 팀 색 띠 다섯 곳과 정산 일차 제목의 부모 이동입니다. UIFontScale의 제외 루트는 별도로 변경했습니다.

`light-ui-preview/native-captures/`: 저장 프리팹과 통제된 샘플 값의 Editor 렌더입니다. 낮/밤 배경은 이전 Unity 카메라 참고 이미지이며 최신 실시간 매치 캡처로 해석하지 않습니다. 캐릭터 선택의 실제 3D 화면과 런타임 확인은 `runtime-captures/` 및 `runtime-state-checks.json`을 참조합니다. 생성 시안의 3D 배경과 음료 그림을 새 월드 모델로 구현한 결과는 아닙니다.

슬라이더와 숫자 입력의 양방향 연결 및 Apply 전 오디오/화면 설정이 유지되는 것을 Play Mode에서 확인했습니다. 성능 프레임 타임·빌드 메모리·다중 PC 매치는 측정하지 않았습니다.

재현용 편집 스크립트는 `ApplyLightLuigiUI.cs`입니다. 실행은 Unity CLI의 `run_script`를 사용합니다. 사용자 작업이 없는 정지 상태에서만 실행하며, 프리팹을 YAML 문자열로 편집하지 않습니다.

---

# 루이지 콘셉트 UI · Unity 적용과 재사용 구조

2026-10-18 갱신. `36d045a`의 Common / Day / Night 분리를 유지하고 현재 저장된 UI 프리팹을 수정했습니다. 기존 화면 프리팹의 GUID와 Presenter 연결을 유지합니다.

결과는 이 폴더의 `blood-beans-luigi-portable.html` → **Unity 실제 적용**에서 확인합니다. 앞선 화면·상태 캡처 23장 갱신에 이어, 이번 보정으로 기존 항목 11장을 다시 교체했습니다. 새 비교 탭이나 온라인 사이트를 추가하지 않았습니다.

## 최신 수정 · 설정·방 목록과 기존 3D CharacterStage

설정·방 목록이 승인 시안보다 단순했던 차이를 기존 프리팹에서 수정했습니다. 실제 입력·데이터·GUID·게임 흐름을 유지했습니다. 구현 미리보기 9개 항목(귀환 상태 2개 포함)을 교체했으며 새 비교 탭은 추가하지 않았습니다.

- 설정: `UISettingsPopup/Panel`에 붉은 책등·커피콩 황동판·랜턴·크림 조작판을 적용했습니다. 공용 `UIVolumeSlider`와 기존 3개 음량 슬라이더/숫자 입력, 전체 화면 Toggle, Apply/Close를 재사용합니다. 회전 카메라가 있을 때만 감도 행이 보이는 조건도 유지합니다. 슬라이더 37→입력 37, 입력 42→슬라이더 0.42와 적용 전 실제 설정 불변을 확인했습니다.
- 방 목록: `UIRoomListRow`에 일반/선택 표면, 공통 카페 사진, 인원 아이콘과 큰 이름을 적용했습니다. 오른쪽 상세판·팀 수 ±·하단 작업 버튼을 정리하고 선택 불가 버튼은 어둡게 표시합니다. 실제 방이 없을 때만 안내 문구/엠블럼과 비활성 입장 버튼이 표시됩니다. 예시 방은 검토용 값이며 실제 온라인 방으로 추가하지 않았습니다.
- 보조 대기실: 기존 `UIRoomScreen`의 팀 영역을 2×2 팀 보드로 바꿨습니다. `UIRoomTeamCard` 하나에 기존 `UICrewRosterCard` 2개를 중첩해 재사용하며 팀·초상·이름·준비 상태는 실제 멤버 데이터에서 읽습니다. 팀원 수정의 현재 팀당 2인 구조를 따릅니다. 이 화면의 카페/뱀파이어 배경은 정적 일러스트이며 현재 기본 방 입장 흐름의 캐릭터 선택 무대와 구분됩니다.
- 타이틀·귀환: 공용 제목 재질과 버튼, 귀환 제목/설명의 여백을 보정했습니다. 귀환 3종의 배경은 이전 실제 밤 카메라 PNG이며 결과 팝업은 실제 프리팹입니다. 신규 네트워크 매치를 진행한 캡처로 설명하지 않습니다.
- 상자: `UIBoxLootPopup/Stage/Panel`의 HorizontalLayoutGroup과 ContentSizeFitter를 함께 활성화해 슬롯 수에 따라 패널이 맞춰지도록 복구했습니다. 기존 `UIBoxLootSlot`의 물음표·공개 안내·등급·툴팁 글자 영역을 확대했습니다. 툴팁은 슬롯 아래에 표시해 제목과 겹치지 않습니다. 실제 GridTitle과 남은 칸 안내는 각각 제목·상태 TMP에 연결했습니다. 공개·담기·희귀 효과·서버 호출은 변경하지 않았습니다.
- **CharacterStage는 사용자 요청에 따라 기존 3D 카페를 사용합니다.** 병합에서 없어진 이미지 Canvas를 잠시 복구해 확인했으나 최종 저장본에서는 `MenuBackdropCanvas`를 제거했습니다. 기존 `Cafe_Lobby`·`CrewDecor`와 무대 카메라의 후처리를 켰고, 새로 병합된 바닥/설비가 촬영되도록 CharacterStage 내부 레이어만 전용 카메라에 맞췄습니다. 새 모델·Material·조명 프로필·8자리·GUID는 보존했습니다. 기존 카메라 1대, 전체 viewport, 좌측 선택 캐릭터 구도를 유지합니다. 월드용 원본 모델 프리팹이나 공용 렌더러는 수정하지 않았습니다.

`LuigiSettingsKit.png` 9개 Sprite와 `LuigiRoomKit.png` 13개 Sprite가 기존 공용 Atlas를 공유합니다. 타이틀·보조 대기실의 정적 메뉴 그림과 실제 3D CharacterStage를 구별합니다. 제작 기록은 `RefineLuigiMenuReference.cs`, 무대 최종 복구 기록은 `RecoverLuigiCharacterStage.cs`입니다. 이전 `WireLuigiCrewPreview.cs`는 이미지 배경을 사용하던 시점의 기록이므로 재실행하지 않습니다.

현재 검사: 프리팹 53개, 공유 Surface 참조 149개, 누락 참조 0. 대상 화면과 툴팁의 TMP 글자 검사 13개 상태에서 overflow/글자 경계 초과 0건을 확인했습니다. 3D 무대는 1920×1080 / 2560×1080 / 1600×1200에서 캡처했습니다. 이는 해당 샘플 값의 화면·컨트롤 확인이며 모든 이름/실제 다중 PC 매치/Player 성능 검증은 아닙니다. 검증 기록은 `ui-menu-reference-validation.json`입니다. file:// 브라우저 자동 접근은 이전 차단 이후 재시도하지 않았으며 portable 내장 이미지·폰트 검사를 사용합니다.

이번 사용 스킬: unity-cli, ui-ugui, sprite-editor, optimize-text-mesh-pro, imagegen, urp-postprocessing. 설치 작업에는 skill-installer, skill-creator, plugin-management를 사용했습니다. Ponytail/engineering 플러그인은 제공되지 않아 사용하지 않았으며 실제 파일·참조·에디터 검사로 대신했습니다.

## 이전 수정 · 캐릭터·팀 선택의 글자와 승인 시안 대응

첨부된 실제 화면을 기준으로 `UICharacterCard`, `UICrewRosterCard`, `UICharacterSelectScreen`, 선택 UI 전용 `CharacterStage`를 수정했습니다. 기존 GUID·선택/준비/팀 콜백과 4종 CharacterCatalog는 유지합니다. `selectedStageName`은 기존 선택 이름을 캐릭터 옆에도 표시하는 선택적 TMP 참조이며, 같은 `Select`에서 갱신합니다.

- 하단 카드의 표면을 글자 뒤로 보내고 이름 영역을 높이 46 / 좌우 여백 12로 확보했습니다. 전체 카드를 팀색으로 덮던 표현은 작은 팀색 표시로 바꾸고, 선택한 카드만 녹색 테두리로 강조합니다. 캐릭터 카드를 새로 복제하지 않았습니다.
- 참가자 카드는 얇은 어두운 녹색 프레임과 상반신 초상으로, 상세 영역은 종이 등록 카드·낮/밤 아이콘으로 수정했습니다. 실제 스킬 이름/설명은 현재 카탈로그 값을 그대로 표시합니다. 긴 설명은 고정 영역 안에서 줄바꿈하며 런타임 AutoSize는 사용하지 않습니다.
- ESC 글자 영역의 높이와 기준선을 수정하고, F1 정보 힌트를 준비 버튼 왼쪽으로 옮겼습니다. 이전 `RosterBoard`, `ClassTray` 장식이 새 부품 뒤에 중복 표시되지 않도록 비활성화했습니다.
- `LuigiCrewKit.png`의 9개 Sprite를 기존 공용 Atlas에 추가했습니다. 선택 프레임은 9-slice의 `fillCenter=false`로 가운데를 비웁니다. 배경은 `LuigiCrewBackdrop.png` 정적 카페 그림이고, 선택 캐릭터는 기존 카메라의 실제 3D 모델입니다. 새 카메라·RT·폰트 Atlas·패키지를 만들지 않았습니다. `LuigiCrewTitle.mat` 하나가 기존 제목 폰트 Atlas를 공유하며 과한 외곽선을 줄입니다.

네 캐릭터를 실제 선택해서 각각 표시 중인 TMP 42개의 경계/overflow를 확인했고 모두 초과 0건입니다. 긴 좀비 설명은 1600×1200, 2560×1080에서도 확인했습니다. 이는 **캐릭터 선택 화면의 해당 표시 상태**를 확인한 결과이며 모든 UI·모든 닉네임·네트워크 매치 전체의 검증이 아닙니다. 최종 힌트 위치를 반영한 대표 실행 캡처로 기존 `implemented-crew.png`를 교체했습니다. 상세 수치는 `ui-crew-fit-validation.json`에 있습니다.

현재 공통 검사: 프리팹 52개 / Surface 참조 146개, 누락 참조 0. 마지막 실행 확인의 현재 Console Error/Warning 0, compilationFailed=false. 이전 절의 143개는 정산 보정 시점 기록입니다. 이번에 사용한 스킬은 unity-cli, ui-ugui, optimize-text-mesh-pro, sprite-editor, imagegen이며, 기존 게임 규칙·월드 프랍은 변경하지 않았습니다.

## 추가 보정 · 영수증과 결과의 시안 대응

기존 결과가 작은 글자와 반복된 목재판 위주로 보이던 차이를 수정했습니다. 승인 이미지 자체를 바꾸지 않고 저장된 프리팹을 수정했습니다.

- 정산: 얇고 긴 영수증, 작은 커피콩 황동 클립, 큰 매출·임대료, 완납/미납 도장, 금·은·동·백금색 메달과 가는 구분선을 사용합니다. 예보와 자동 적용은 별도 클립 없는 녹색 보드입니다. 실제 보석 6종과 남은 턴을 같은 `UIGemRow`로 표시합니다.
- 최종 결과: 좌측 큰 결과/승리 문구, 우측 메달 순위·큰 매출·둥근 비율 막대, 하단 기존 로비 복귀 버튼으로 정리했습니다. 순위의 0 접두사를 제거했습니다.
- 공통 글자: `LuigiTitleDisplay.mat`, `LuigiNumberDisplay.mat` 두 재질이 기존 SDF 텍스처를 공유합니다. 큰 글자에만 어두운 외곽선·짧은 갈색 그림자·크림 그라데이션을 적용했습니다. 작은 본문은 기존 Ink/Light 재질을 유지합니다. 새 폰트 Atlas를 만들지 않았습니다.
- 제조: 기존 `UIMakingCard` 도크와 `UICompletionGauge`를 우측 끝에서 안쪽으로 옮겼습니다. 상태판, 둥근 판정 바, 바늘, 제목 위치를 보정했습니다. 실제 메뉴 Sprite와 서버 판정값·구간·입력 연결은 유지합니다. 카드에 조작 대상과 무관한 F 키를 추가하지 않았습니다.
- 정산/최종 결과의 카페와 캐릭터는 **정적 메뉴 배경 일러스트**입니다. 금액·팀명·도장·순위·막대는 실제 uGUI/TMP입니다. 해당 배경을 실시간 승자 모델이나 실제 3D 렌더라고 설명하지 않습니다. 캐릭터 선택 화면은 기존 실제 3D 무대를 사용합니다.

이번 런타임 변경은 `UIDaySettlementScreen`의 표시색/카운트다운 원, `UISettlementLine`의 도장 테두리색, `UIGemRow`의 두 줄 서식, `UIMatchResultPopup`의 순위/매출 표시 서식입니다. 정산·임대료·순위의 계산 규칙과 네트워크 경로는 바꾸지 않았습니다.

제작 기록은 `MatchLuigiSettlementReference.cs`, `PolishLuigiMaking.cs`입니다. 수동 변경을 덮을 수 있으므로 일괄 재실행하지 않습니다. 새 `LuigiReceiptKit.png`의 영수증·보드·메달·동전 8개 Sprite는 기존 공용 Atlas를 공유하며, 안정 Sprite ID와 9-slice 테두리를 Unity Sprite Data Provider로 저장했습니다. 배경 2개는 별도 Sprite입니다. 생성 근거와 요약 브리프는 `ui-assets-refinement-provenance.json`에 기록했습니다.

추가 확인: 저장된 프리팹 52개 / Surface 참조 143개 검사 통과, HUD 세 Root/8개 Surface Variant/Atlas 배선 검사 통과. 완납과 미납 2일을 각각 실행해 도장·연속 미납 색을 확인했습니다. 정산의 2560×1080 및 1600×1200 캡처에서 주요 패널의 겹침이 없는지 확인했습니다. 기록은 `unity-implementation/ui-match-check-*.png`이며 갤러리 항목으로 추가하지 않았습니다. 마지막 캡처 구간의 Console 현재 오류/경고는 0, compilationFailed=false였습니다. 원격/전체 매치/Player 빌드 성능 검증은 아닙니다.

portable 파일은 이미지 125개와 폰트 8개를 내장하며 최신 배포 원본과 모든 이미지 바이트가 일치하는 것을 검사했습니다. 이번 직접 `file://` 브라우저 자동 확인은 브라우저 URL 정책으로 차단되어 수행하지 못했습니다. Unity 실제 캡처 및 내장 데이터 검사를 수행한 결과이며 브라우저 클릭 검증으로 표기하지 않습니다. 검증용 Play Mode와 임시 Game View 해상도는 종료·복원했습니다.

## 적용 내용

| 대상 | 바꾼 내용과 이유 |
|---|---|
| `Assets/Art/UI/Prefabs/Screen/UITitleMenuScreen.prefab` | 시안의 투명 로고와 녹색 시작 버튼을 분리해 저장. 텍스트와 버튼의 경계가 겹치지 않도록 수정 |
| `Screen/UIRoomListScreen.prefab` | 왼쪽 목재 목록판, 오른쪽 등록 정보판, 하단 작업 버튼. 실제 선택 방 이름/인원을 표시 |
| `Screen/UICharacterSelectScreen.prefab`, `Parts/UICharacterCard`, `Parts/UICrewRosterCard` | 큰 실제 3D 캐릭터, 우측 8명, 하단 4종 선택. 큰 프레임이 초상과 이름을 가리던 부분을 얇은 테두리로 교체 |
| `Assets/Art/Environment/Prefabs/CharacterStage.prefab` | 선택 UI 전용 무대에서 기존 캐릭터 카메라 1대와 카메라 뒤쪽 배경 Canvas를 사용. 월드 게임 씬/카메라 리그는 변경하지 않음 |
| `Screen/UIMatchHudScreen.prefab` | 팀원 변경의 CommonRoot / DayRoot / NightRoot와 `day`, `night` 배선을 유지. 낮 순위·시계·목표, 밤 매출·시계·가벼운 가방의 위치와 TMP를 조정 |
| `Parts/UIMakingCard.prefab` | 좁은 목재 클립보드, 실제 제품 Sprite, 별도 상태판. 상태판이 글자를 가리던 순서와 기존 넓은 클립의 중복 표시 수정 |
| `Parts/UICompletionGauge.prefab` | 공용 녹색/황동 판정판에 실제 구간·바늘·남은 시간 배치. 판정 계산은 기존 구현 유지 |
| `Parts/UIRecipeRow`, `Parts/UINamedItemIcon`, `Parts/UIStandingRow` | 같은 행/아이콘/순위 부품을 재사용. 종이 위 글자 대비, 재료 폭, 얇은 구분선 정리 |
| `Screen/UIDaySettlementScreen.prefab` | 목재 영수증판과 녹색 정보판으로 구분. 겹친 장식 Surface를 비활성화해 종이가 가려지는 문제 수정 |
| `Popup/UIMatchResultPopup.prefab` | 크림 순위판과 큰 승리 글자. 승리 제목색과 순위 글자/그래프색을 분리해 밝은 배경의 글자 대비 확보 |
| `Popup/UISettingsPopup.prefab` | 붉은 책등/크림 종이, 실제 Slider·InputField를 사용. 기존 음량/감도/전체 화면 연결 유지 |
| `Screen/UIRoomScreen`, `Popup/UIBoxLootPopup`, `UIReturnResultPopup`, `UILoadingPopup`, `UIPhaseCuePopup` | 같은 공용 판·버튼·폰트 재질 사용. 루팅 중첩 제목판을 비활성화하고 종이 슬롯의 등급 글자를 잉크색으로, 귀환 상태 문구를 대비가 높은 색으로 조정. 데이터·결과·인원·카운트다운은 기존 컴포넌트가 표시 |

프리팹 약식 경로는 `Assets/Art/UI/Prefabs/` 아래입니다. 캐릭터는 현재 CharacterCatalog의 뱀파이어·좀비·늑대인간·마녀입니다. 게임 규칙·서버/RPC·월드 프랍은 이번 수정 범위에 포함하지 않았습니다.

## 새 화면 복제 대신 공유하는 구조

```text
기존 Screen / Popup 프리팹 · 기존 GUID / UIView / Presenter
├ UISurfacePart.prefab
│  ├ 기존 Paper / Green / Header / Selected / Red / Cream / Disabled
│  └ 새 MenuClipboard / Board / Book / Registration / Green / Red / Brown / Cream
├ UIRoomListRow · 1회 생성 후 목록 변경/선택 때 재사용
├ UICrewRosterCard · 초상 / 닉네임 / 팀색 / 준비 상태
└ UIMatchHudScreen
   ├ CommonRoot · 상호작용 / 스킬 / 대시 / 캐스팅 / 팀·핑
   ├ DayRoot + UIMatchHudDay · 조합법 / 순위 / 시계 / 제조 / 판정
   └ NightRoot + UIMatchHudNight · 매출 / 시계 / 가방 / 귀환

UIMakingCard · 데이터와 상태만 바꿔 사용
UIRecipeRow / UIStandingRow / UINamedItemIcon · 기존 반복 부품
TMP Title / Body / Number · 같은 Font Atlas와 공유 Material
```

`UISurfaceMenu*.prefab` 8개는 UISurfacePart의 실제 Prefab Variant입니다. 새 Screen 루트는 만들지 않았습니다. 화면은 기존 컴포넌트·Button 참조를 사용하고 장식 Surface를 중첩합니다. 새 방 행은 `UIRoomListRow.prefab` 하나이며, `UIRoomListScreen.cs`는 생성한 행과 파츠 참조를 저장하고 남는 행을 숨깁니다.

현재 정적인 클립보드는 투명 Sprite와 TMP입니다. 부품마다 실시간 카메라/RenderTexture를 추가하지 않았습니다. 선택 캐릭터만 기존 실제 3D 카메라로 표시하며, 참가자 카드에는 기존 실제 모델 초상을 사용합니다. 3D 원형 `PrototypeKit/UIClipboard3DAssembly.prefab`은 검토 기록으로 남아 있습니다.

## 자산·폰트·비용

- 새 제작 PNG: `Common/LuigiMenuKit.png`, `LuigiMenuBook.png`, `LuigiPlayKit.png`, `Title/LuigiGameLogo.png`. 동적 글자·수치·가격은 패널에 굽지 않았습니다.
- `Assets/Art/Shaders/UICrewBackdrop.shader`와 `UI_CrewBackdrop.mat`는 카메라 뒤쪽 그림이 실제 선택 캐릭터를 덮지 않도록 깊이 비교를 사용합니다. 다른 씬의 Renderer 설정은 바꾸지 않습니다.

- 새 공용 시트 3개를 기존 `Assets/Art/UI/Atlases/LuigiCommon.spriteatlasv2`에 추가했습니다. 기존 항목과 PC BC7 / 최대 2048 / 회전·타이트 패킹·밉맵 없음 설정을 유지했습니다. 실제 Atlas 페이지 수와 GPU 메모리는 콘텐츠 빌드·프로파일러로 별도 측정해야 합니다.
- 제목 Cafe24 Ssurround, 본문 Do Hyeon, 숫자 Lilita One. 실제 TMP Font Asset과 원본 TTF/OFL 파일을 유지합니다. 주요 글리프는 정적 Atlas, 추가 닉네임 등은 동적 폴백입니다.
- `LuigiMenu{Title,Body,Number}{Ink,Light}.mat` 6개는 같은 폰트 텍스처를 공유하는 프리셋입니다. 작은 글자의 과한 베벨/그라데이션을 끄고 종이 위 잉크색과 어두운 판 위 크림색을 분리했습니다.
- 새 장식 Image는 Raycast를 받지 않습니다. 패널 Surface의 LayoutElement.ignoreLayout을 설정해 실제 행 크기 계산에 끼어들지 않도록 했습니다.
- CanvasScaler는 1920×1080 / Expand입니다. 반복 행은 부모 LayoutGroup이 크기를 정합니다.

이는 재사용·메모리 구조를 정리한 결과입니다. FPS·Draw Call·GC 개선 수치를 측정한 결과는 아닙니다.

## 이번에 확인한 범위

- `ui-unity-implementation-validation.json`: 프리팹 **52개**, 직렬화된 Surface 참조 **143개** 검사 통과. 누락 스크립트, 끊어진 UI 참조, TMP 리소스, CanvasScaler, 초상 등록, Atlas 설정을 확인했습니다. 143은 활성 화면의 동시 렌더 개수가 아닙니다.
- `ui-prefab-reuse-validation.json`: HUD의 세 Root와 Day/Night 연결, 새 Surface Variant 8개, Atlas의 새 시트 3개, 선택 무대 배경 Shader의 컴파일 메시지 검사 통과.
- 방 목록 4→2→4행 갱신에서 같은 오브젝트 ID 유지, 남는 행 숨김/복원, 세 번째 행의 로컬 선택 콜백 인덱스 2 전달 확인.
- 실제 저장 프리팹을 Play Mode에서 실행해 상태별 화면 23장을 캡처했습니다. 목록·참가자·매출 등은 검토용 입력값입니다. 낮/밤 HUD 배경은 기존 실제 플레이 카메라 캡처이며 전체 네트워크 매치를 새로 실행한 캡처는 아닙니다.
- 캐릭터 선택/밤 HUD를 2560×1080 및 1600×1200에서 추가 확인했습니다. 기록은 `unity-implementation/ui-check-*.png` 4장입니다. 모든 화면·모든 해상도의 조합을 검증한 것은 아닙니다.
- 마지막 로컬 캡처 확인 구간에서 새 Console Error가 없었고 compilationFailed=false였습니다. 이전 진단/Play 종료 오류 이력까지 모두 해결했다는 뜻은 아닙니다.
- 갤러리 PNG는 크기를 줄이지 않고 무손실 WebP로 변환했습니다. portable 파일 안의 이미지 125개와 폰트 8개 내장을 검사했습니다. 브라우저 확인 기록은 별도 검증 JSON에 기록합니다.

최종 Player/Addressables 빌드, Steam 방 생성, 다중 PC 통신, 실제 매치 전체 흐름, 장시간 성능 측정은 이번 재확인 범위 밖입니다. 승인 이미지와 픽셀 단위로 완전히 같다는 미술 검수 결과도 아닙니다.

## 후속 수정 시 주의

저장된 프리팹을 기준으로 수정합니다. 이 폴더의 과거 `ApplyLuigiProductionUI`, `RefineLuigiHudFrames`, `MatchApprovedNightHud` 등은 팀원의 Day/Night 분리 이전 구조를 전제로 합니다. **전체 레시피를 순서대로 다시 실행하면 현재 배치·수동 수정·배선을 덮을 수 있으므로 실행하지 않습니다.** 초기 43개/120개 검사 및 네트워크 플레이 기록은 이전 단계 이력이며 현재 검증을 대신하지 않습니다.

현재 구조의 읽기 전용 검사는 `ValidateLuigiProductionUI.Main`과 `ValidateLuigiReuse.Main`입니다. `RefineLuigiPlayUI.cs`, `WireLuigiCrewPreview.cs`, `RefineLuigiLootAndToast.cs`는 이번 특정 자산에 사용한 제작 기록이며 범용 런타임 생성기가 아닙니다. 현재 에셋만으로 실행되며 이 문서 폴더의 스크립트는 게임 어셈블리에 포함되지 않습니다.

Git에는 실제 Unity 자산의 `.meta`, 새 Material/Shader/Sprite, 공용 프리팹과 런타임 스크립트를 함께 포함해야 합니다. portable HTML과 assets.zip을 ignore해도 게임 실행에는 영향이 없습니다.

사용한 스킬: unity-cli, unity-architecture, ui-ugui, sprite-editor, optimize-text-mesh-pro, imagegen. Ponytail과 engineering 플러그인 스킬은 제공되지 않아 파일 검색·직렬화 참조·Unity 실행으로 대체 확인했습니다.

런타임 코드 변경 위치: `Assets/Scripts/Client/UI/UIRoomListScreen.cs:87`의 행 생성/갱신 경로, 새 `UIRoomListRow.cs:20`의 데이터·선택 표시 바인딩, `UIMatchResultPopup.cs:142`의 순위 글자/막대 색 분리입니다. 순위 계산과 네트워크 경로는 유지합니다.
