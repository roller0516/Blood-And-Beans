# 현재 시안: 실제 구도 · 루이지 UI

2026-10-11. 기존 로비·제조·루팅 시안과 최근 낮·밤 UI가 서로 다른 재질·서체·입체감으로 보이는 문제를 수정했다. 부분별 CSS 장식을 추가하는 대신 기존 제조와 캐릭터 선택을 스타일 기준으로 삼아 낮·제조·밤 화면을 내장 image_gen으로 다시 그렸다. 새 갤러리 항목이나 비교 버전은 추가하지 않았다.

## 화면과 부품

- 낮 HUD: 실제 낮의 후방 카메라와 작업대 배치를 구도 참고로 사용했다. 입체 크림 글자, 초록 접수판, 황동 모서리, 크림 제조 카드, 귀여운 유령 손님을 기존 콘셉트와 맞췄다. 플레이어는 선택한 뱀파이어 외형이며 낮 가방은 없다.
- 제조: 이전 제조 시안의 클립보드와 판정 게이지를 플레이 구도에 적용했다. 아메리카노는 현재 모델의 손잡이 없는 컵 형태를 참고했다. 고객 인내심의 별도 수평 막대는 제거하고 말풍선 내부 채움으로 수정했다.
- 밤 HUD: 실제 숲·귀환 원·플레이어의 후방 구도를 참고했다. 기존 시안과 같은 크림 숫자와 초록 패널, 두 개의 Q/SPACE 스킬, 작은 모래색 가방을 적용했다. 새 건물·상자·달을 배경에 추가하지 않았다.
- 조합법/주문 비교: 낮 HUD 이미지 위에 투명 메뉴/비교판을 겹친다. 카메라·플레이어가 메뉴를 열었다고 이동하지 않는다.
- 상자/밤 부품: 같은 밤 HUD 위에 투명 패널을 겹친다. 다섯 슬롯과 순차 공개, 선택 툴팁을 유지한다. 상태 비교판의 크기를 실제 HUD 크기로 보지 않는다.

카메라 구도를 참고한 **생성 콘셉트**이며 배경 픽셀이 원본 캡처와 동일한 합성이 아니다. 조명·재질과 캐릭터 외형을 콘셉트로 표현했다. Unity 씬/프리팹/코드는 이번에 수정하지 않았다. 음료·재료는 현재 모델 형태를 참고한 시안 렌더이며, 모든 아이콘이 원본 프리팹 픽셀과 같다는 의미는 아니다.

## 산출물과 재현

- `unity-concepts-v2/unity-luigi-v2-day.png`, `unity-luigi-v2-making.png`, `unity-luigi-v2-night.png`: 기존 파일을 대체한 현재 콘셉트.
- `unity-gameplay/menu-board.png`, `orders-board.png`, `loot-board.png`, `night-components.png`: 같은 아트 스타일로 만든 투명 패널.
- `unity-gameplay/coherent-generation.json`: 내장 image_gen의 모든 정확한 프롬프트, 입력 역할, 출력/최종 경로. 전체 조합법 생성은 구도가 움직여 폐기하고 메뉴판만 추출해 원래 낮 이미지에 겹쳤다.
- `unity-gameplay/style-making-reference.png`: 다시 그리기 전 제조 스타일 기준. 로컬 참고용이며 HTML/사이트/zip에 포함하지 않는다.
- `unity-gameplay/day-base.png`, `night-base.png`, `*-original.png`, `capture.json`, `night-capture.json`: 이전 Unity 캡처와 카메라 자료. 로컬 참고용이며 현재 갤러리에 포함하지 않는다.
- `gallery-assets.json`, `assets.zip`, `blood-beans-luigi-portable.html`, `claude-import.html`: 실제 사용되는 36개 이미지와 3개 폰트만 전달한다. portable HTML 경로는 사용자가 지정한 현재 Docs 폴더이다.
- `proof-v10-day.png`, `proof-v10-recipe.png`, `proof-v10-night.png`, `proof-loot.png`: 기존 브라우저 미리보기 기록을 현재 결과로 갱신한다.

사이트 코드: `output/luigi-ui-gallery/dist/project.js`, `dist/gameplay.css`, `encode-gameplay.py`, `export-portable.cjs`. 기존 generic HTML HUD 대신 완성된 같은 스타일의 비트맵과 투명 UI 패널을 표시한다. WEBP는 무손실 인코딩 후 RGBA 디코딩 비교로 확인한다. 원본 게임 캡처와 이전 UI 레이어는 활성 화면/내장 HTML에 포함하지 않는다. 예전 `view=current` 등 링크는 루이지 시안으로 연결된다. 기존 8인 팀 무대와 캐릭터 시안은 유지한다.

## Unity 적용

갤러리 PNG를 통째로 Unity 화면에 붙이지 않는다. 이번 투명 메뉴/비교판에도 문자가 있으므로 실제 동적 UI에 그대로 사용하는 파일은 아니다. 아트 승인 후 문자 없는 프레임, 클립, 아이콘, 채움 마스크로 나눈다. 프레임은 uGUI Sprite/9-slice, 값은 TMP, 판정/인내심/가방 상태는 기존 Presenter 데이터에 연결한다. 제목/숫자는 Bagel·Coiny의 Outline/Underlay와 크림색 밝은 면으로 입체감을 재현한다. 제품 아이콘은 실제 모델을 같은 조명과 카메라에서 렌더링한 Sprite로 교체한다. 기존 HTML 구현 가이드의 화면별 연결을 유지한다.

실제 사용한 스킬: sites, imagegen. 내장 image_gen 사용. 이번에는 Unity 에디터 조작을 하지 않았다. 캡처 당시의 unity-cli/ui-ugui 사용 기록과 구분한다.


## 추가 비교: 조금 절제된 버전

현재 시안 원본 7개는 SHA256 비교로 변경되지 않았음을 확인했다. 낮·조합법·주문·제조·밤·루팅·밤 부품에서 현재 시안 / 절제형을 선택한다. 절제형은 입체 서체와 재질을 유지하며 금속 반사·주변 발광·장식 효과를 줄였다. 별도 화면 항목은 추가하지 않았다.

`unity-gameplay/restrained-*.png` 7개와 `restrained-generation.json`에 내장 image_gen 출력·정확한 프롬프트·입력 역할을 기록했다. 활성 이미지는 43개이며 portable HTML과 assets.zip도 함께 갱신했다. 구현 시 절제형은 공용 Sprite/TMP 머티리얼의 광택과 Underlay/Glow 강도를 낮춘 프리셋으로 관리하고, 선택·실패·판정 등 기능 색은 유지한다. 게임 규칙·카메라·글자 크기를 장식 강도와 함께 변경하지 않는다.


## 밤 카메라 구도 수정
기존 밤 이미지에서 플레이어가 작아지고 배경 나무·귀환 원 원근이 달라진 문제를 수정했다. 실제 night-base.png를 편집 대상으로 다시 사용해 플레이어 머리 높이 약63%, 하단 잘림, 오른쪽 큰 나무 꼭대기 약19%, 귀환 원 먼 경계 약73%를 기준으로 맞췄다. 기존 밤 현재 시안·절제형 두 파일만 교체했으며, 루팅·밤 부품도 같은 배경을 참조한다. 새 갤러리 항목과 원본 캡처는 게시하지 않았다. 생성 프롬프트와 입력 역할은 unity-gameplay/night-camera-correction.json에 기록했다. 실제 게임 캡처의 모든 픽셀과 동일하다는 의미가 아닌 구도를 유지한 생성 콘셉트다.
