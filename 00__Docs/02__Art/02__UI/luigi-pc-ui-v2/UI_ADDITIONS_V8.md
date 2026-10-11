# 2026-10-11 · 실제 게임 카메라 위의 루이지 UI

낮 HUD·조합법·주문/인내심·제조/판정에 「실제 인게임 · 루이지 UI」와 「실제 인게임 원본」을 추가했습니다. Battle_01의 실행 중인 Main Camera에서 직접 확보한 화면입니다. 카메라, 배경, 캐릭터, 설비와 배치를 그대로 유지하고 HTML/CSS UI 레이어를 별도로 얹었습니다. 이전 생성 시안도 비교 선택지로 보관했습니다.

`unity-gameplay/day-base.png`는 기존 Canvas를 잠시 숨긴 상태에서 같은 게임 카메라로 렌더한 1920×1080 배경입니다. `day-original.png`는 기존 UI가 있는 같은 상태의 화면입니다. 원본 PNG와 배포용 WebP 사이의 RGBA 픽셀 일치는 `unity-gameplay/encoding-validation.json`에 기록했습니다.

현재 매출·임대료·시간·팀 순위·주문·인내심·재고·조합법은 `capture.json`의 플레이 데이터와 월드 UI 위치를 사용합니다. 음료와 재료 아이콘은 프로젝트의 ItemVisualConfig 프리팹을 Unity에서 렌더했습니다. 제조 게이지는 동일한 게임 구도에서 배치를 비교하는 표현 예시입니다.

## Unity 적용 방법

게임 카메라와 월드 배치는 유지합니다. 상단 순위·시간·매출과 조합법 팝업은 Screen Space Canvas에 분리하고, 손님 주문은 기존 UICustomerOrder 위치를 이용합니다. 인내심 Fill은 주문 말풍선 안에 배치합니다. 실제 제조 시에는 기존 제조 상태에 게이지 값을 연결합니다. 낮에는 가방 UI를 넣지 않습니다.

폰트는 Bagel Fat One(제목), Jua(본문), Coiny(숫자)를 TMP Font Asset으로 준비합니다. 패널은 9-slice 스프라이트, 인내심/제조 게이지는 Image Fill로 구현하고, 숫자와 이름은 TMP로 갱신합니다. 음료 아이콘은 실제 프리팹을 Orthographic Camera에서 투명 배경으로 렌더한 결과를 사용합니다.

## 파일과 확인

지정된 `luigi-pc-ui-v2`의 독립형 HTML·Claude 가져오기 HTML·이미지 묶음·재구성 입력을 갱신했습니다. 배포용 HTML과 zip의 Git 제외 설정은 유지합니다. 구현 코드는 사이트 소스의 `gameplay-template.js`, `gameplay.css`, `dist/project.js`에 있습니다. 캡처 스크립트는 `unity-gameplay/capture-gameplay.cs`입니다.

브라우저에서 네 종류의 실제 인게임 UI와 원본 전환, 이미지 로딩, 콘솔 오류를 확인했습니다. 캡처 중 잠시 바꾼 Canvas·카메라 렌더 설정과 일시정지는 복구했습니다. 씬 dirty 상태는 false였으며 프로젝트 UI 프리팹이나 씬을 저장하지 않았습니다. 게임 QA나 빌드는 수행하지 않았습니다.

사용 스킬: unity-cli, ui-ugui, sites. Ponytail은 설치된 스킬 목록에서 찾을 수 없어 기존 코드와 Unity API를 사용했습니다.
