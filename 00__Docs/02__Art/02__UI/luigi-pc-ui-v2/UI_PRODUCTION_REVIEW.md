> 이 문서는 초기 부품 검토 기록입니다. 후속 실제 게임 적용 내역은 UI_UNITY_IMPLEMENTATION.md와 갤러리의 Unity 실제 적용 탭을 확인해 주세요.

# 실제 제작 부품 검토

2026-10-18. 확인 위치: `blood-beans-luigi-portable.html` → **제작 부품 검토**.

이번 자료는 실제 Unity 에셋을 렌더링한 검토본입니다. 기존 갤러리의 콘셉트 이미지 전체가 구현된 것은 아닙니다. 검토본에 쓰인 부품을 실제 게임에 그대로 재사용하는 것을 기준으로 합니다. 게임 기능과 기존 화면에 대한 연결은 검토 후 진행합니다.

## 실제 저장된 구성

Unity 폴더: `BloodAndBeans/Assets/Art/UI/PrototypeKit`.

- `UIClipboardBase.prefab`: 판·종이·클립·내용 영역의 공통 기본형.
- `UIRecipeClipboardReview`, `UIMakingClipboardReview`, `UISettlementClipboardReview`: 기본형을 참조하는 실제 Prefab Variant 3개. 크기·문구·내용이 다릅니다.
- `UIActionButton.prefab`: TMP 라벨과 Sprite Swap 버튼. 기본·강조·선택·눌림·비활성 그래픽 참조를 가집니다. 선택과 눌림은 같은 녹색 Sprite를 사용합니다. 각 화면의 버튼은 이 프리팹의 중첩 인스턴스입니다.
- `UIHeaderPart`, `UISlotPart`, `UIHudPart`, `UIGaugePart`: 용도별 공통 부품 원형. 아직 모든 화면에 연결한 상태는 아닙니다.
- `UI*Model.prefab`: 10종 실제 Mesh 원형과 공유 Material.
- `UIClipboard3DAssembly.prefab`: 판·종이·클립 원형을 중첩한 3D 조립 프리팹.
- `UI_*.png`: 10종 문자 없는 실제 Sprite. 클립 외에는 9-slice Border를 저장했습니다. 현재 임포트는 무손실·밉맵 없음이며 최종 플랫폼 압축과 Atlas는 검토 후 확정합니다.
- 역할별 TMP SDF 3종과 검토 전용 정적 폴백. 원본 게임 폰트의 설정을 바꾸지 않습니다. 고정 검토 문구의 글리프를 저장했으며 게임 전체 문자열용 최종 폰트 아틀라스는 아닙니다.

## 같은 모습으로 적용하는 방식

3D 원형 → 정면 투명 Sprite 저장 → uGUI Image로 조립 → 실제 TMP 문자열 배치 → Unity에서 1920×1080 렌더.

`ui-production-assembled.png`, `parts.png`, `breakdown.png`, `board-3d.png`는 이 방식의 실제 출력입니다. HTML에는 이를 무손실 WebP로 내장했습니다. HTML의 CSS로 별도의 가상 UI를 그려 결과를 꾸미지 않습니다.

일반 화면은 저장한 Sprite를 표시하므로 클립보드마다 3D 카메라를 유지하거나 RenderTexture를 갱신하지 않습니다. 회전이 필요한 UI만 3D 원형 사용을 검토합니다. 월드 프랍·캐릭터는 제작 범위에 포함하지 않았습니다. 제품 표시는 기존 프로젝트의 메뉴 아이콘을 재사용했습니다.

실제 인게임 적용에서는 확정한 Sprite·TMP Material·프리팹을 유지한 상태로 기존 데이터 및 입력을 연결합니다. 기준 해상도·Canvas 배율·앵커를 맞추고 실제 플레이 캡처로 크기와 대비를 다시 대조합니다. Atlas 패킹, 상태 전환, 텍스트 갱신 빈도, Canvas 분리, 해상도 대응 및 프로파일링은 게임 연결 단계에서 검증합니다. 아직 성능 수치를 측정한 결과는 없습니다.

## 재현·검증

`BuildUIProductionReview.cs`는 Unity CLI의 `run_script`로 실행하는 생성 레시피입니다. Assets에 Editor 전용 코드를 상주시키지 않습니다. PreviewScene을 사용하며 게임 씬을 저장하지 않습니다. 렌더링 중 월드 전용 렌더러 효과를 잠시 제외하고 즉시 원복합니다.

`ValidateUIProductionReview.cs`로 실제 Variant의 기본형, 중첩 버튼 원본, Sprite·TMP 참조 및 독립 폴백을 검사했습니다. 결과: `ui-production-unity-validation.json`.

HTML의 네 가지 검토 탭과 개별 에셋 선택을 브라우저에서 확인했습니다. 파일 프로토콜의 자동화 제약 때문에 검증 시 같은 portable HTML을 임시 localhost로 읽었습니다.

이번 단계는 공통 부품의 외형·조립 방식 검토입니다. 나머지 UI 에셋 제작, 전체 게임 프리팹 교체, 데이터 연결, 성능 최적화 완료를 의미하지 않습니다.
