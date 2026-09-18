# Battle_01 — Soft Stylized 라이팅 인계

적용 씬: Assets/Scenes/Battle_01.unity
전용 볼륨: Assets/Art/Lighting/Battle_01/Battle_01_SoftStylized.asset

## 적용 내용
- Directional Light: RGB (1, 0.925, 0.83), Intensity 1.65, Soft Shadows, Shadow Strength 0.85.
- Environment Lighting: Gradient(Trilight). Sky (0.48, 0.56, 0.68), Equator (0.34, 0.37, 0.43), Ground (0.23, 0.20, 0.18).
- Tonemapping ACES.
- Bloom: Threshold 1.15, Intensity 0.18, Scatter 0.55, High Quality Filtering.
- Color Adjustments: Exposure +0.15 EV, Contrast -5, Saturation -6.
- White Balance: Temperature +3, Tint +1.
- Vignette: Intensity 0.10, Smoothness 0.65.
- 기존 SSAO 및 FogOfWar 렌더러 기능 유지. 기존 카메라의 후처리 설정 유지.
- DOF, Motion Blur, Film Grain은 추가하지 않았다. 플레이 시 인식성과 선명도를 우선한다.

## 작업 범위 및 팀 협업
게임 C# 소스, 네트워크, 충돌체, 게임 오브젝트 배치, 프리팹, 공용 URP/Renderer, 패키지를 수정하지 않았다.
공용 SampleSceneProfile 대신 새 전용 프로필을 씬에 연결했다.
작업 시작 전 변경되어 있던 캐릭터 텍스처/애니메이터/모델 메타, 카페 재질 및 PainterlyCafe 셰이더는 유지했다.
Unity가 씬을 저장하면서 기존 GamePhase의 startTimeoutSeconds 기본값 60을 직렬화했다. 이 항목은 라이팅 수정이 아니며, 스크립트 기본값과 동일하다. 팀 리뷰에서 참고한다.
Temp 폴더의 eval 조각은 에디터 자동화용 임시 파일이며 게임에 포함되는 C# 스크립트가 아니다.

## 참고 이미지에 더 가까워지기 위한 다음 TA/아트 패스
1. 형태: 캐릭터의 둥근 실루엣을 유지하고 소품 모서리에 베벨과 부드러운 노멀을 준다.
   모델 수정은 시각용 메시만 대상으로 한다. 루트, 리깅 본, 소켓, 피벗, Collider는 개발자 합의 없이 바꾸지 않는다.
2. 재질: 천/몸통은 비금속, 넓고 약한 반사로 표현한다. URP Lit 기준 Smoothness 0.2~0.35부터 비교한다.
   눈은 Smoothness 0.65~0.8, 도자기는 0.35~0.5 정도로 재질 차이를 만든다.
   이는 시작값이며 현재 셰이더의 SurfaceMap 채널 정의를 확인한 뒤 적용한다.
3. 텍스처: 강한 명암을 BaseColor에 구워 넣지 말고 큰 색면 위에 낮은 대비의 색상 변화를 넣는다.
   기존 PainterlyCafe 재질은 이미 작업 중이므로 원본을 복제해 A/B 비교 후 교체한다.
4. 팔레트: 크림/살구색 벽, 따뜻한 목재, 차분한 청록 또는 청색 포인트로 정리한다.
   바닥과 배경 채도를 낮춰 캐릭터가 먼저 읽히게 한다.
5. 풀/숲: 현재 풀은 가늘고 반복이 많아 참고 이미지보다 시각적으로 복잡하다.
   큰 잎 덩어리와 성긴 군집으로 정리하되 숨기/시야 관련 게임 규칙과 Collider는 보존한다.
6. 조명: 카페는 런타임에 생성된다. 일반적인 씬 라이트맵 베이크만으로는 이 구조를 처리할 수 없다.
   정적 배경 베이크와 이동 캐릭터용 프로브는 실제 생성 위치, 팀 수, 낮/밤 구조를 개발자와 확인한 뒤 설계한다.
7. 털: Party Animals의 털 실루엣은 볼륨만으로 만들 수 없다. 별도 메시/퍼 셰이더 및 플랫폼 성능 검토가 필요하다.
8. FogOfWar: 검게 가려지는 배경은 현재 게임 규칙이다. 그래픽 개선을 이유로 렌더러 기능을 끄지 않는다.

## 검증과 한계
실제 Battle_01에서 Play 진입, 캐릭터/카페 생성, 전용 볼륨 연결을 확인했다.
HDR=True, 카메라 Post Processing=True, Volume enabled/global=True, weight=1, 레이어 마스크 일치 및 5개 활성 오버라이드를 확인했다.
Game View 캡처: Temp/ta-play-before.png, Temp/ta-play-after.png.
두 캡처는 플레이 세션과 시점이 달라 정밀한 동일 구도 A/B 비교는 아니다.
멀티플레이 전체, 카페 내부 모든 시점, 낮/밤 전체 사이클, 타깃 하드웨어 성능까지 검증한 것은 아니다.
기존 로그에는 Addressables 관련 실패 스택이 있어 콘솔 무오류를 보장하지 않는다. 이번 작업에서 게임 코드로 수정하지 않았다.

## 수동 복원
- Global Volume의 Profile을 Assets/Settings/SampleSceneProfile.asset으로 다시 연결.
- Directional Light: 흰색, Intensity 2, Shadow Strength 1.
- Environment Lighting: Skybox 모드.
- 원래 Ambient 색: Sky (0.212,0.227,0.259), Equator (0.114,0.125,0.133), Ground (0.047,0.043,0.035).
다른 사람의 새 변경이 생긴 상태에서 씬 전체를 Git restore하지 말고 해당 설정만 되돌린다.
