# AssetStore

외부에서 받아온 서드파티 에셋만 두는 폴더. 프로젝트가 직접 만든 코드·머티리얼·아트는 `Assets/Art`, `Assets/Scripts`에 둔다.
여기 있는 파일은 원본 그대로다. 수정하지 않고, 필요한 변형은 프로젝트 쪽 머티리얼·프리팹으로 만든다.

기준 기획서: `Plan_eatyourmeat/1_기획서/BLOOD & BEAN — 게임 기획서.md` (v2.6)

## Kenney (CC0 1.0 — 상업 사용·수정·재배포 자유, 출처 표기 불필요)

출처: https://kenney.nl/assets — 각 팩 폴더의 `License.txt`가 원본 라이선스 전문이다.

| 팩 | 기획서 근거 | 용도 |
|---|---|---|
| `cafe-selection` | 5.4 카페 레이아웃, 7.1 낮 재료 실물 | Food Kit · Furniture Kit · Mini Characters에서 골라 쓰던 기존 모델. 커피머신·오븐·조리대·싱크대·서빙대·손님 |
| `nature-kit` | 1.2 어둠의 숲, 6.3 숲의 구조 | 밤 맵 본체 — 나무·바위·그루터기·통로·절벽 블록 |
| `graveyard-kit` | 1.2 어둠의 숲, 1.3 언데드 손님 | 숲의 언데드 톤 — 울타리·비석·고사목·폐허 |
| `survival-kit` | 6.5.2 박스 등급 | 1등급 나무 상자 · 2등급 철제 궤 · 임시 더미. 7.1이 밤의 3D 오브젝트를 박스 3종 + 더미 1종으로 못 박았다 |
| `fantasy-town-kit` | 1.2 "숲 가장자리에 낡은 카페 몇 채" | 카페 건물 외관, 귀환 지점 표식. 12장 톤(캐주얼·판타지)에 맞춰 현대 도시 킷 대신 골랐다 |
| `prototype-textures` | 6.3 "지형·통로 배치는 레벨 디자인 영역" | 동심원 링 그레이박스용 격자 텍스처 |
| `ui-pack` | 6.5.5 BoxLootPopup, 6.7 무게 게이지 | 슬롯 그리드·패널·게이지 9-slice 스프라이트 |

각 3D 팩은 FBX와 텍스처만 받아 두었다. 원본 zip의 OBJ/GLTF/DAE/STL 중복 포맷과 프리뷰 이미지는 제외했다.

## 아직 없는 것

**언데드 손님 6종 (기획서 1.3 / 5.5 — 좀비·뱀파이어·유령·해골·늑대인간·마녀).**
Kenney에는 언데드 3D 캐릭터 팩이 없다. 무료 CC0 대안은 KayKit Character Pack: Skeletons
(https://kaylousberg.itch.io/kaykit-skeletons — 애니메이션 포함)이지만 itch.io가 다운로드
버튼 클릭을 요구해서 자동으로 받지 못했다. 받으면 이 폴더에 `kaykit-skeletons/`로 넣는다.
그때까지는 `cafe-selection`의 Mini Characters를 손님으로 쓴다.
