# Blood & Bean 귀환 소환진 리소스

2026-09-27 · Unity 6000.5.9f1 / URP 17.5.0

## 제공 범위

프로젝트 `Assets/Art/VFX/`에 텍스처 3개, 머티리얼 5개, 프리팹 3개를 추가했다. 기존 씬과 ReturnZone 프리팹·게임 코드는 수정하지 않았다. 이 파일은 아트 리소스 사용 안내이며 기획서의 귀환 규칙을 개정하지 않는다.

| 파일 | 용도 |
|---|---|
| `ReturnPortal_Visual.prefab` | 바닥 소환진 + 상시 별/안개 + 양초 묶음 3개. 씬에 드래그해 배치 |
| `ReturnPortal_Burst.prefab` | 플레이어 위치에서 한 번 재생하는 상승 별/안개 효과 |
| `ReturnCandleCluster.prefab` | 짧고 통통한 양초 3개와 불꽃. 독립적으로 사용 가능 |
| `ReturnSigil.png` | 평면 소환진 발광 마스크 |
| `ReturnStar.png` | 네 꼭짓점 별 입자 마스크 |
| `ReturnMist.png` | 부드러운 안개 입자 마스크 |
| `ReturnSigil/ReturnStar/ReturnMist.mat` | 각 텍스처용 URP Additive 머티리얼 |
| `ReturnCandleFlame/ReturnCandleWax.mat` | 촛불·왁스 머티리얼 |

양초는 Unity 기본 메시를 조합한 입체 소품이며 별도 FBX 모델은 아니다. 숲·지형·버섯·캐릭터·배낭은 콘셉트의 맥락이며 이 패키지에는 포함하지 않았다.

## 바로 보기

1. Project 창에서 `Assets/Art/VFX/ReturnPortal_Visual.prefab`을 평평한 지면에 드래그한다.
2. 루트 Scale은 `(1,1,1)`로 두고 Position Y를 실제 지면 높이에 맞춘다. 내부 GroundSigil은 지면에서 0.035m 위다.
3. 소환진은 대기 별과 안개를 반복 재생한다. 에디터에서는 Particle System을 선택하고 Simulate로 확인할 수 있다.
4. `ReturnPortal_Burst.prefab`은 테스트할 캐릭터의 발 위치에 따로 배치한다. 새 인스턴스의 Play On Awake로 1회 재생한다. 에디터에서는 파티클 Simulate로 확인한다.
5. 귀환 프리팹 재사용 시 하위 Particle System들을 Stop/Clear한 후 함께 Play한다. 풀 반환·파괴와 게임 이벤트 호출은 포함하지 않았다.

## 바닥 크기와 배치

현재 `ReturnZone.cs`의 radius 기본값은 4m다. 새 프리팹은 이 값에 맞춰 외곽선의 시각적 지름을 약 8m로 잡았다. 텍스처의 검정 여백 때문에 GroundSigil Quad 자체는 약 10.052 × 10.202m이며 중심도 소폭 보정했다. Quad 크기를 그대로 8m로 바꾸면 보이는 원이 작아진다.

AI로 그린 선이라 기하학적으로 완벽한 원은 아니다. 실제 판정은 기존 ReturnZone.Contains가 담당한다. 이 프리팹에는 Collider·Trigger·NetworkObject·귀환 판정 스크립트가 없다. 실제 반경이나 부모 스케일을 변경하면 표시 크기도 다시 맞춰야 한다.

Quad 방식이므로 평평한 땅에 쓴다. 경사·굴곡이 큰 지형에는 데칼 또는 지면에 맞춘 별도 메시가 필요하다. 나무·바위·풀에 문양이 가려지지 않도록 주변 배치를 조정한다.

## 텍스처 / 머티리얼

- 원본 PNG는 각 1254 × 1254 RGB. 투명 알파가 있는 PNG가 아니다.
- 검정 배경은 Additive 블렌딩에서 거의 보이지 않고 흰 영역이 빛난다. Opaque/일반 Alpha 머티리얼에 넣으면 검정 사각형이 생길 수 있다.
- 현재 임포트: Default, sRGB 켬, Alpha Source None, Clamp, Trilinear, Mip Maps 켬, NPOT To Nearest, 압축 없음, Read/Write 끔. 에디터 실제 임포트 크기는 검증 항목에 기록한다.
- URP `Universal Render Pipeline/Particles/Unlit`, Transparent / Additive / 양면 / ZWrite Off. Base Map과 Emission Map에 같은 마스크를 사용한다.
- 색을 바꿀 때 Base Color와 Emission Color를 함께 변경한다. 여러 팀이 같은 머티리얼을 공유하므로 공용 `.mat`를 런타임에 덮어쓰지 말고 팀별 인스턴스나 MaterialPropertyBlock을 사용한다.
- 이미지 생성 특성상 완전한 흑백 분리 대신 극소량의 어두운 잔여 픽셀이 있을 수 있다. 현재 Additive 렌더에서 검정 사각형이 드러나지 않는지 실제 프리뷰로 확인했다.
- 발광이 주변 지형을 비추는 실제 광원은 아니다. Bloom은 기존 씬 설정에 따라 달라진다. 이 리소스는 전역 Volume과 조명 설정을 변경하지 않는다.

## 제공된 이펙트 값

아래는 아트 조정용 초기값이며 게임 규칙이 아니다. Particle System Inspector에서 조절한다.

| 효과 | 발생 | 수명 | 움직임 |
|---|---|---|---|
| IdleStars | 2개/초, 반복 | 2.1~2.8초 | 반경 3.5m에서 위로 0.24m/초 |
| IdleMist | 0.75개/초, 반복 | 3~4초 | 반경 3m에서 위로 0.12m/초 |
| ReturnStars | 30개, 1회 | 1.275~1.7초 | 반경 0.65m에서 위로 1.1m/초 |
| ReturnVapor | 10개, 1회 | 1.125~1.5초 | 반경 0.4m, 위로 상승하며 Y축 공전 |

콘셉트의 넓게 감기는 리본은 여기서 안개 입자의 회전으로 간략화했다. 프리팹은 캐릭터를 실제로 디졸브시키거나 순간이동시키지 않는다. 진입 시 원의 밝기 변화·귀환 시 캐릭터 디졸브는 후속 연결 작업이다. 비교 시 AI 콘셉트보다 `ReturnPortal_Unity_Idle.png`, `ReturnPortal_Unity_Burst.png`가 실제 제공 리소스의 모습이다.

## 기존 귀환 구역과 연결할 때

- 기획서 6.8: 들어가는 즉시 귀환하는 것이 아니라 밤 종료 시 소환 위치와 가방 소지 여부를 판정한다. 진입 반응과 실제 귀환 발동을 분리한다.
- `ReturnPortal_Visual`을 기존 ReturnZone의 시각적 자식으로 연결하고 이전 표시용 Renderer만 정리한다. 기존 네트워크/판정 컴포넌트를 제거하지 않는다.
- ReturnZone은 자기 팀에만 복제되며 TeamVision이 계층에 팀 레이어를 적용한다. 늦게 생성하는 효과에도 해당 팀의 레이어·가시성 규칙이 필요하다. 전역 공유 오브젝트로 모든 팀에게 표시하지 않는다.
- `ReturnZone.Settle`은 판정 직후 플레이어를 낮 위치로 이동시킨다. 이펙트 1.7초를 이유로 서버 정산·밤 길이를 임의로 지연하지 않는다. 이동 전 위치의 시각 연출을 클라이언트에서 따로 처리할 연결이 필요하다.
- 기존 `PlayerDissolve`는 밤 진입 시 나타나는 연출이다. 현재 리소스는 그 스크립트를 변경하거나 귀환 소멸에 연결하지 않았다.

## 검증과 파일

- 내장 image_gen으로 텍스처 생성 후 Unity CLI를 통해 실제 에디터에서 임포트·머티리얼·파티클·프리팹을 생성했다. 새로운 C# 런타임 스크립트와 패키지 의존성을 추가하지 않았다.
- 분리된 Preview Scene에서 대기/귀환 중간 상태를 1280 × 960으로 실제 렌더링했다. 작업 후 Preview Scene을 닫았다.
- 프리팹의 Renderer에 연결된 머티리얼·지원 셰이더·셰이더 컴파일 오류·Missing Script를 검사했고 오류 목록은 비어 있었다.
- 게임 씬 배치, 두 명의 실제 귀환 동시 연출, 팀 간 가시성, Host/Client 동기화, 프레임 성능은 미검증이다. 제공 범위는 아트 리소스이며 게임 통합 완료를 의미하지 않는다.
- `ReturnPortal_Resources.unitypackage`: 동일 Unity/URP 프로젝트에 옮기는 리소스 묶음. 현재 프로젝트에는 이미 추가되어 있으므로 다시 임포트할 필요가 없다.
- `ReturnPortal_Prompts.md`: 최종 텍스처 3종의 생성 프롬프트.
- `ReturnPortal_Concept.png`, `ReturnPortal_VFX_Storyboard.png`: 승인받은 콘셉트 자료.

사용 스킬: imagegen, unity-cli, unity-architecture(배치 경계 확인). Ponytail은 찾지 못했으며 사용하지 않았다. Graphify 연결도 없어 실제 소스 검색과 Unity 에디터 확인으로 대체했다.
