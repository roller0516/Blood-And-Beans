# Twilight sky & CharacterStage — TA 인계

## Lighting Settings Asset
Battle_01은 현재 실시간 조명과 런타임 생성 카페를 사용한다. 별도 Lighting Settings Asset은 만들지 않았다.
이 에셋은 주로 GI/라이트맵 베이크 설정이며, 생성만으로 예쁜 조명이 생기는 것은 아니다.
베이크할 정적 배경과 동적 캐릭터용 프로브 작업이 확정될 때 별도로 설계한다.
현재 Gradient 환경광은 스카이박스 색과 독립적이다.
공식 설명: https://docs.unity.cn/6000.1/Documentation/Manual/lighting-window.html

## Battle_01
- Environment Skybox: Battle_01/M_Battle_TwilightSky.mat.
- Shader: Assets/Art/Shaders/SoftTwilightSky.shader, BloodAndBeans/Sky/Soft Twilight.
- 청보라 그라데이션, 작은 달, 부드러운 달무리, 약한 별. 텍스처나 런타임 스크립트 불필요.
- Inspector에서 Zenith/Horizon/Ground, Moon Direction/Radius, Halo, Stars, Exposure 조절 가능.
- 밤의 검은 영역은 기존 FogOfWar가 하늘까지 덮기 때문이었다. Skybox만 바꾸면 해결되지 않는다.
- 기존 FogController를 복제한 Battle_01/FogController_Twilight.prefab을 씬의 기존 fogPlanePrefab 슬롯에 연결.
- 복제본의 fogColor RGB만 (0.035,0.052,0.095)로 변경. 원래 alpha 0.9607843, blurLevel 1.5, edgeSoftness 0.35 유지.
- 공개 범위/마스크/밤 판정/게임 규칙 및 기존 FogController 원본은 그대로다.
- 밤에는 이 가림 효과 때문에 새 달과 별도 대부분 덮인다. 배경을 드러내는 게임 규칙 변경은 하지 않았다.
- 하늘은 시간에 따라 자동 전환되지 않는 고정 twilight 아트 설정이다.

## CharacterStage
Assets/Art/Environment/Prefabs/CharacterStage.prefab 자체를 수정했다.
UICharacterSelectScreen의 stagePrefab GUID와 동일한 에셋임을 확인했다.
Title 씬, TwoPlayerScenario 설정, UI 이벤트, 자리/카메라 추적 코드는 수정하지 않았다.
- StageCamera: Post Processing ON, Volume Layer Mask=CharacterStage(12), Clear Flags=Skybox.
- 카메라 Skybox 컴포넌트에 무대 전용 M_Stage_TwilightSky 연결. 무대는 달/별/달무리 없이 차분한 배경.
- StageVolume에 CharacterStage_SoftStylized.asset 연결. Battle 프로필에서 복사한 ACES/약한 Bloom/색 보정.
- 기존 0.45의 강한 비네트를 0.16으로 완화. 기존 꺼진 카메라 후처리를 켰으므로 실제 적용 경로가 달라졌다.
- KeyLight 1.35, FillLight 0.55, StageSpot 140→55, Spot Angle 46→58.
- 보조광: Cool Rim 0.45, 무대 내부 Warm/Cool Point 각 22, Portrait Softbox Point 16.
- 새 보조광은 12번 레이어만 비추며 그림자를 추가 계산하지 않는다.
- 장식용 Sage/Walnut/Brass 및 Window 재질을 전용 폴더에 만들고 해당 무대 렌더러에만 연결.
- 작업 중인 캐릭터 재질과 PainterlyCafe 공용 재질은 수정하지 않았다.
- 새 조명은 캐릭터 선택 화면용이다. 실제 타깃 기기에서 추가광 비용을 프로파일링해야 한다.

## 검증
- Sky shader import/컴파일 오류 없음(ShaderUtil.ShaderHasError=False).
- 별도 프리뷰 공간에서 하늘과 실제 무대 프리팹+캐릭터 2개 렌더링을 확인.
- 캡처: Temp/ta-sky-preview.png, Temp/ta-stage-after.png.
- 무대 캡처는 고정 카메라 프리뷰이며, 실제 UI/이름표/네트워크 연결을 포함한 TwoPlayerScenario 실행 화면은 아니다.
- 현재 열린 씬에 저장되지 않은 변경이 있어 이를 버리거나 강제 저장하지 않았다.
  Title로 씬을 교체하지 않고 프리뷰 검증으로 제한했다.
- Assets/Scripts, Packages, ProjectSettings의 Git diff 없음.
- 공유 C# 또는 기존 안개 셰이더의 구현 수정 없음.

## 확인 방법
현재 편집 중인 작업을 저장한 다음 평소처럼 TwoPlayerScenario를 실행한다.
Title의 캐릭터 선택 화면에서 StageCamera/StageVolume 연결, 캐릭터 2명 밝기, 이름표 위치를 확인한다.
Battle_01의 밤에는 이전 검정 대신 남청색 가림 영역이 보여야 한다.
낮에는 가림이 해제되어 원래 조명과 새로운 하늘이 보인다.

## 복원
Battle_01의 Skybox를 Default-Skybox로, fogPlanePrefab을 기존 Assets/Art/Environment/Prefabs/FogController.prefab으로 연결한다.
CharacterStage 변경은 해당 프리팹의 라이팅/카메라/재질 변경만 되돌린다.
다른 팀원의 변경이 섞여 있으면 프리팹 전체를 Git restore하지 않는다.
## 안개 후속 조정
- FogController_Twilight: fogColor=(0.009,0.016,0.032,0.86).
- blurLevel: 1.5 → 3.0. 밉 샘플링 범위를 넓혀 격자 경계를 더 넓게 혼합.
- edgeSoftness: 0.35 → 0.49. 전환 구간 확대.
- Battle 스카이박스 Exposure: 1 → 0.55.
- 가림 알파가 낮아져 미탐색 배경 실루엣이 이전보다 더 비칠 수 있다. 요청한 반투명 표현의 결과다.
- 탐색 셀/네트워크/안개 셰이더 코드는 변경하지 않음. 낮 및 CharacterStage 프로필 미변경.
