# UI 시안 일치 작업 — 중단 및 재개 기록 (2026-10-21 KST)

사용자의 “일단 멈추고. 남은 작업은 다음에 하게 마무리해봐” 요청으로 구현을 중단했습니다. **완료·시안 완전 일치 상태가 아닙니다.** 변경분은 작업 폴더에 저장되어 있고 커밋하지 않았습니다.

## 고정할 사용자 요구

- HTML light 시안과 실제 Unity UI의 글자, 초상, 배치, 버튼/패널 질감을 맞춥니다. 시안을 바꿔서 맞춘 것으로 처리하지 않습니다.
- 기존 화면 프리팹을 수정합니다. 화면 복제본이나 대체 화면 루트는 추가하지 않습니다.
- **CharacterStage는 현재의 실제 3D 배경 그대로 둡니다.** 2026-10-21 사용자가 다시 명시했습니다. Stage 모델·카메라·배경을 이미지로 덮지 않습니다.
- 이후 답변도 존댓말을 사용합니다.

## 저장된 변경

| 대상 | 이유와 저장된 변경 | 주요 위치 |
|---|---|---|
| 공용 패널·버튼 | 기존 밝은 이중 프레임을 얇은 둥근 테두리와 보라색 면으로 교체. 선택 테두리와 키 안내도 조정 | `Assets/Art/UI/Prefabs/Parts/UISurface*`, `UITeamSlot`, 공용 머티리얼, `UIRoundedRect.shader` |
| 크루 선택 | 카드 이름바 돌출, 초상 크롭, 명단 외곽 누락, 설명 글자 배치와 팀 선택 표시 보정 | `UICharacterSelectScreen`, `UICharacterCard`, `UICrewRosterCard` |
| 타이틀·방 목록 | 메뉴 간격/글자, 선택 표시, 진입 버튼, Esc·새로고침·만들기 아이콘 보정 | `UITitleMenuScreen`, `UIRoomListScreen`, `UIRoomListRow` |
| 낮·밤 HUD | 시계·초상·가방·스킬 키 안내, 낮 순위 면, 가변 길이 상호작용 패널 보정 | `UIMatchHudScreen`, `UISkillSlot`, `UIStandingRowClock`, `UIMakingCard` |
| 이름표 | 팀 색이 루트 사각형 전체에 칠해지던 참조를 Dot으로 변경. 소유자 YOU와 화살표 분리 | `UIPlayerNameplate.cs`의 `Render`, 기존 파츠와 Player의 중첩 연결 |
| 월드 아이콘·주문 | 재료 마커를 작은 보라색 패널로, 주문을 메뉴+가격 가로형 패널로 재배치. 인내심 채움은 유지 | `UIIngredientBadge`, `UICustomerOrder`, `UIBubbleFill.shader`의 선택적 둥근 모드 |
| 재료 상자 | 재료 이름을 평소에도 표시. 다섯 칸 가로 배치와 작은 등급/하단 정보 위치 조정 | `UIBoxLootPopup`, `UIBoxLootSlot.cs`의 `itemCaption` |
| 정산 | 큰 오늘 매출, 보석 정보, 여섯 종족 초상과 실제 구성비 막대, 일차 제목과 자동 진행 안내 | `UIDaySettlementScreen.cs`의 `FillForecast`, 기존 정산 화면, `UIForecastRaceCard` |
| 최종 결과 | 실제 팀 픽 대표 초상 연결, 순위 행 재배치와 빈 막대 제거, 결과 제목·로비 버튼 복원 | `MatchFlow.cs` 결과 팝업 호출, `UIMatchResultPopup.cs`의 `FillRanks` |
| 설정 | 숫자 입력칸·슬라이더·녹색 체크박스·X·Esc 안내. 적용 전에는 설정을 바꾸지 않는 흐름 유지 | `UISettingsPopup.cs`, `UISettingsPopup.prefab`, `UIVolumeSlider` |

기존 프리팹 54개의 GUID는 모두 보존했습니다. 재사용용 `Parts/UIForecastRaceCard.prefab` 1개만 추가해 현재 UI 프리팹은 55개입니다. 새 화면 루트는 없습니다. 전체 수정 파일은 `light-ui-preview/native-captures/reported-modified-files.txt`에 기록했습니다. 이번 작업에서 CharacterStage와 씬 파일의 Git 차이는 없습니다.

## 확인된 사실과 한계

- 현재 C# 재컴파일 완료, 컴파일 오류 없음. CLI `run_script`의 내부 success도 확인했습니다.
- 설정: 슬라이더→숫자, 숫자→슬라이더, 100 초과 입력 클램프, 체크 상태에 따른 면 색, Apply 전 전체화면 미적용 확인. 감도 행까지 다섯 줄을 켰을 때 적용 버튼과 50px 간격 확인.
- 긴 `Tier 1 · 길게 눌러 열기`: 텍스트 폭 216.96px, 패널 폭 약 299.96px, 키와 본문 12px 간격, TMP overflow false.
- Player 원본의 중첩 이름표 참조: background=Dot, plate=LuigiSurface, team과 SelfArrow 연결 확인.
- 검토용 데이터로 실제 저장된 UI 프리팹을 Play Mode에서 렌더했습니다. HUD 아래 배경은 이전 Unity 카메라 캡처를 사용한 **검토용 샘플**입니다. 실제 게임 배경 교체가 아닙니다.
- Steam 멀티플레이 전체 흐름, 판매·루팅 RPC, Player 빌드, 프레임 성능은 이번에 검증하지 않았습니다. 주문이 실제 손님 간격에서 겹치지 않는지도 별도 확인해야 합니다.
- 컴파일·TMP overflow 결과는 시안 일치의 증거가 아닙니다. 현 구현은 시안보다 면이 평평하고 글자/광택의 차이가 남아 있으며 최종 시각 대조가 필요합니다.
- 정산은 실제 게임의 6종족, 인기 재료, 6종 보석을 유지했습니다. 시안의 4개 손님 그림과 3개 보석만 남기도록 게임 데이터를 삭제하지 않았습니다. 현재 예보는 숫자 인원수를 노출하지 않는 기존 규칙대로 비율 막대만 표시합니다.
- 최종 결과의 다시 시작은 실제 재시작 경로가 없어서 기존처럼 콜백이 없으면 숨깁니다. 구현된 기능으로 보고하면 안 됩니다.
- 동적 폰트 `LuigiLetteringFallback SDF.asset`, `Pretendard-Regular SDF.asset`의 에디터 글리프 캐시 변경이 남아 있습니다. 폰트 디자인을 새로 바꾼 변경으로 오해하지 말고 후속 정리 시 확인합니다.

## 다음 작업의 첫 순서

1. `reported-session-state.json`과 항목표를 읽고 현재 저장된 상태에서 시작합니다. 마지막 수정이 들어간 **SPACE 키캡 폭·YOU 화살표·최종 초상 크롭·방 새로고침 글리프·상자 슬롯·낮 HUD/제조 카드**부터 재캡처합니다. 아래 PNG 중 일부는 이 마지막 저장 이전의 중간 캡처입니다.
2. 빈 방 목록 상태도 별도로 재현합니다. `rooms-empty` 캡처는 아직 없습니다. 실제 손님 주문 간격과 월드 이름표/재료 마커의 겹침도 실제 플레이에서 확인합니다.
3. 승인 PNG와 같은 비율로 나란히 대조해 글꼴 무게/외곽선, 보라색 면의 광택·투명도, 아이콘 크기와 여백을 맞춥니다. “31개 모두 해결”이라고 단정하지 않습니다.
4. 마지막 변경 후 전체 관련 화면을 다시 확인하고 사용자에게 실제 Unity 캡처를 보여 줍니다. **CharacterStage 배경은 건드리지 않습니다.**

## 재현 도구와 주의점

프로젝트는 `BloodAndBeans`, Unity CLI Pipeline은 localhost:7800 입니다. 변경 전 `unity status --format json`과 실제 명령 목록을 확인합니다. 에디터가 실행 중일 때 YAML/meta를 직접 편집하지 않습니다.

- `MatchLuigiReportedScreens.cs`: 이번 변경의 Editor API authoring 기록. **현재 `Main`을 그대로 재실행하지 마세요.** 최종 순위 Portrait를 PortraitCrop 밑으로 옮긴 후에도 `Final()`은 기존 `row.Find("Portrait")`를 가정합니다. 재실행 전에 직렬화된 Portrait 참조를 사용하는 방식으로 멱등성을 보정해야 합니다. `Main`은 개별 Polish 배치를 되덮을 수도 있습니다.
- 실행했던 후속 메서드: `Forecast`, `PolishSettlement`, `PolishHud`, `PolishMenus`, `FinishDetails`, `PolishKeys`. 마지막 세부 저장은 `PolishKeys` → `PolishMenus` 순서였습니다.
- `../../output/ShowLuigiMenuPreview.cs`: title/settings/crew/rooms/final/settlement/loot 등의 실제 프리팹에 검토용 값을 바인딩합니다. 로비를 만들지 않습니다.
- `../../output/ShowLuigiHudPreview.cs`: 실제 HUD 파츠+이전 카메라 이미지로 렌더합니다. night/day/recipe/making 모드가 있습니다.
- `PreviewLuigiNightState.cs`: normal/buried/full/cooldown/long-prompt 상태.
- `PreviewLuigiReportedParts.cs`: orders/nameplates 파츠 샘플. 소유권 검증을 대신하지 않습니다.
- `VerifyLuigiReportedUI.cs`: Settings/Prompt/Connections 확인. `InspectLightLuigiRuntime.cs`는 TMP overflow 기록입니다.
- C# 변경 후 배경 AutoRefresh를 믿지 말고 `unity command recompile` 후 `recompile_status`가 completed인지 확인합니다. `run_script`는 바깥 success가 true여도 내부 `data.result.success`가 false일 수 있습니다.
- 캐시된 UIManager는 이전 프리팹을 재사용합니다. 프리팹 저장 후 검토할 때 Play를 껐다 켜서 새 인스턴스로 봅니다. 원래 사용자 실행 상태를 보존합니다.

## 중단 시 정리

검토용 Play Mode를 종료해 샘플 오브젝트를 제거했고, 원래 Game View 선택을 복원했습니다. 이후 원래처럼 Title 씬 Play Mode로 돌려 두었습니다. 작업용 `Assets/Screenshots/reported-*.png` 11개는 아래 문서 폴더로 복사한 뒤 AssetDatabase로 해당 임시 에셋만 삭제했습니다. 원본 31장과 중간 네이티브 캡처 11장은 모두 보관했습니다. 커밋·스테이징·푸시는 하지 않았습니다.

## 실제 사용 스킬

`bb-art-direction`, `unity-cli`, `ui-ugui`, `sprite-editor`, `optimize-text-mesh-pro`, `unity-architecture`, `imagegen`을 사용했습니다. Ponytail/engineering 스킬은 제공되지 않아 사용하지 않았으며 앞서 부재를 고지했습니다.

## 31개 항목별 상태

모든 행은 최종 시안 일치 승인이 아닌 저장/검토 진행 상태입니다. 이미지 링크는 현재 문서 폴더 기준입니다.

| 번호 | 지적 항목 | 원본 | 네이티브 중간 캡처 | 중단 상태 |
|---|---|---|---|---|
| 1 | 선택 카드 이름표 | [원본](light-ui-preview/native-captures/reported-source-01.png) | [캡처](light-ui-preview/native-captures/reported-crew.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 2 | 크루 제목과 부제 | [원본](light-ui-preview/native-captures/reported-source-02.png) | [캡처](light-ui-preview/native-captures/reported-crew.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 3 | 참가자 초상 크롭 | [원본](light-ui-preview/native-captures/reported-source-03.png) | [캡처](light-ui-preview/native-captures/reported-crew.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 4 | 8인 명단과 준비 수 | [원본](light-ui-preview/native-captures/reported-source-04.png) | [캡처](light-ui-preview/native-captures/reported-crew.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 5 | 크루 뒤로 키 안내 | [원본](light-ui-preview/native-captures/reported-source-05.png) | [캡처](light-ui-preview/native-captures/reported-crew.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 6 | 스킬 설명과 팀 선택 | [원본](light-ui-preview/native-captures/reported-source-06.png) | [캡처](light-ui-preview/native-captures/reported-crew.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 7 | F1 및 준비 버튼 | [원본](light-ui-preview/native-captures/reported-source-07.png) | [캡처](light-ui-preview/native-captures/reported-crew.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 8 | 타이틀 메뉴 | [원본](light-ui-preview/native-captures/reported-source-08.png) | [캡처](light-ui-preview/native-captures/reported-title.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 9 | 방 목록 하단 버튼 | [원본](light-ui-preview/native-captures/reported-source-09.png) | [캡처](light-ui-preview/native-captures/reported-rooms.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 10 | 방 들어가기 | [원본](light-ui-preview/native-captures/reported-source-10.png) | [캡처](light-ui-preview/native-captures/reported-rooms.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 11 | 방 목록 제목과 빈 상태 | [원본](light-ui-preview/native-captures/reported-source-11.png) | 아직 없음 | 빈 목록 상태 별도 재현·캡처 필요 |
| 12 | 밤 초상과 매출 | [원본](light-ui-preview/native-captures/reported-source-12.png) | [캡처](light-ui-preview/native-captures/reported-night.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 13 | 밤 시계 | [원본](light-ui-preview/native-captures/reported-source-13.png) | [캡처](light-ui-preview/native-captures/reported-night.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 14 | 팀 표식 | [원본](light-ui-preview/native-captures/reported-source-14.png) | [캡처](light-ui-preview/native-captures/reported-night.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 15 | 밤 스킬 및 대시 | [원본](light-ui-preview/native-captures/reported-source-15.png) | [캡처](light-ui-preview/native-captures/reported-night.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 16 | YOU 이름표 | [원본](light-ui-preview/native-captures/reported-source-16.png) | [캡처](light-ui-preview/native-captures/reported-nameplates.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 17 | 가방 도크 | [원본](light-ui-preview/native-captures/reported-source-17.png) | [캡처](light-ui-preview/native-captures/reported-night.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 18 | 가방 회수 안내 | [원본](light-ui-preview/native-captures/reported-source-18.png) | [캡처](light-ui-preview/native-captures/reported-night.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 19 | 긴 상자 상호작용 문구 | [원본](light-ui-preview/native-captures/reported-source-19.png) | [캡처](light-ui-preview/native-captures/reported-night-long.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 20 | 루팅 문구 | [원본](light-ui-preview/native-captures/reported-source-20.png) | [캡처](light-ui-preview/native-captures/reported-night-long.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 21 | 재료 상자와 슬롯 이름 | [원본](light-ui-preview/native-captures/reported-source-21.png) | [캡처](light-ui-preview/native-captures/reported-loot.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 22 | 손님 주문과 가격 | [원본](light-ui-preview/native-captures/reported-source-22.png) | [캡처](light-ui-preview/native-captures/reported-day.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 23 | 낮 상단 HUD | [원본](light-ui-preview/native-captures/reported-source-23.png) | [캡처](light-ui-preview/native-captures/reported-day.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 24 | 낮 스킬 | [원본](light-ui-preview/native-captures/reported-source-24.png) | [캡처](light-ui-preview/native-captures/reported-day.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 25 | F1 조합법 안내 | [원본](light-ui-preview/native-captures/reported-source-25.png) | [캡처](light-ui-preview/native-captures/reported-day.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 26 | 재료대 말풍선 | [원본](light-ui-preview/native-captures/reported-source-26.png) | [캡처](light-ui-preview/native-captures/reported-nameplates.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 27 | YOU와 재료 말풍선 | [원본](light-ui-preview/native-captures/reported-source-27.png) | [캡처](light-ui-preview/native-captures/reported-nameplates.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 28 | 일일 정산 | [원본](light-ui-preview/native-captures/reported-source-28.png) | [캡처](light-ui-preview/native-captures/reported-settlement.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 29 | 정산 자동 진행 | [원본](light-ui-preview/native-captures/reported-source-29.png) | [캡처](light-ui-preview/native-captures/reported-settlement.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |
| 30 | 최종 결과 | [원본](light-ui-preview/native-captures/reported-source-30.png) | [캡처](light-ui-preview/native-captures/reported-final.png) | 수정 저장 · 마지막 수정 후 재캡처 필요 |
| 31 | 설정 | [원본](light-ui-preview/native-captures/reported-source-31.png) | [캡처](light-ui-preview/native-captures/reported-settings.png) | 수정 저장 · 중간 네이티브 캡처 확인 · 시안 최종 승인 전 |

## 생성 에셋 출처

`light-ui-preview/native-captures/reported-art-provenance.json`에 두 이미지의 정확한 프롬프트, 참조 시안, 생성 원본, 실제 적용 경로와 슬라이싱 방법을 보관했습니다. CharacterStage에 생성 이미지를 적용하지 않았습니다.
