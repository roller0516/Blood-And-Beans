# 귀환 소환진 리소스 QA

2026-09-28. 범위: 신규 아트 리소스 13개와 인수 문서. 실제 게임 통합은 미포함.

## 검수 역할

- resource_design_qa: 기획서 6.8, 판정 반경과 비주얼 크기, 파일 형식 설명 대조.
- resource_network_qa: 기존 ReturnZone/TeamVision/MatchDirector의 팀 격리와 후속 배선 위험 검수.
- resource_design_qa 추가 검수: 코어 루프, 진입/밤 종료 구분, 정산과 이동 순서 대조.
- resource_runtime_qa: 텍스처·프리뷰·리소스 사용성 검수. 주 작업자가 별도 Preview Scene에서 실제 Unity 렌더/참조 검사를 수행했다.

## 발견 사항과 조치

| 중요도 | 발견 사항 | 조치 |
|---|---|---|
| 높음(통합 시) | 스크립트 radius 기본값은 4지만 실제 ReturnZone.prefab의 값은 5. 기획 6.8의 소환 위치 표시와 판정 경계가 어긋날 수 있음 | Unity SerializedObject로 실제 값 5를 확인하고 문양의 시각 지름을 약 10m로 수정. 기존 판정은 변경하지 않음 |
| 높음(통합 시) | 기존 ReturnZone의 scale (10,1,6), y .5에 local scale 1로 자식 배치하면 거대한 타원/공중 배치가 됨 | 가이드에 현재 부모의 역스케일·높이 보정 명시. 실제 기존 프리팹에는 아직 배선하지 않음 |
| 높음(통합 시) | 새 VFX는 Default 레이어. 전역 씬 배치 시 상대팀 귀환 지점이 노출될 수 있음. 기획 3.4·6.8과 기존 팀 observer 구조 관련 | 팀 제한 ReturnZone 하위 및 TeamVision 적용 시점 명시. 새 NetworkObject나 RPC 없음 |
| 중간 | 초기 Burst 프리뷰의 귀환 효과가 미약함 | 별 크기를 키우고 별도 582정점 3D 나선 리본 메시 입자를 추가. 최종 프리뷰 재촬영 |
| 중간 | PNG를 투명 텍스처라고 설명하면 잘못된 머티리얼 사용으로 검정 사각형 발생 | 실제 파일은 1254² RGB 검정 바탕 가산합성 마스크라고 명시. 제공 URP Additive 재질에서 실제 렌더 확인 |
| 안내 | Burst는 자동 파괴/풀 반환·밤 종료 이벤트·캐릭터 디졸브를 수행하지 않음 | 후속 게임 통합 항목으로 명시. 밤 길이와 서버 정산을 효과 때문에 지연하지 않도록 안내 |

## 실제 수행한 확인

- Unity 6000.5.9f1, URP 17.5.0의 연결된 에디터에서 프리팹과 머티리얼 생성.
- 별도 Preview Scene에서 1280×960 대기/귀환 상태 렌더. 최종 이미지: ReturnPortal_Unity_Idle.png, ReturnPortal_Unity_Burst.png.
- 신규 프리팹 3개의 Renderer 머티리얼, 지원 셰이더, ShaderUtil.ShaderHasError, Missing Script 검사: 오류 목록 0건.
- 소환진 원본 1254², Unity 임포트 1024² 확인.
- .unitypackage ExportPackage 성공: 자체 에셋 13개, 1,797,954 bytes. tar 목록에서 13개 asset/meta/pathname 묶음 확인.
- SHA256: B5B3270C38B91D2D72CB1B9E1EFB8C701714C06CF57C72978187987A67A64D91
- 최종 에디터 active scene isDirty = false. 생성용 Preview Scene은 종료.

## 미검증

별도 프로젝트에 재임포트, 실제 숲 지형/풀과의 겹침, 카메라·Bloom 설정별 최종 외관, 애니메이션의 게임 내 타이밍, Host/Client 2인 플레이, 팀 간 비노출, 실제 동시 귀환, 성능 프로파일. 게임 통합 완료 또는 멀티플레이 QA 통과로 표시하지 않는다.
