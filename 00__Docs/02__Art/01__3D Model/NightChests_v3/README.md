# 밤 상자 v3 — 프로젝트 배치 안내

## Unity 프로젝트 내 위치

- 모델 3개: `BloodAndBeans/Assets/Art/Environment/Models/NightChests/`
- 모델별 텍스처 7개씩: `BloodAndBeans/Assets/Art/Environment/Textures/NightChests/T1_Wood/`, `T2_Iron/`, `T3_Rune/`

T1_Wood는 나무, T2_Iron은 철제, T3_Rune은 룬 상자다. FBX에는 Body와 Lid, 루트와 OpenClose 개봉 애니메이션이 포함된다.

이 폴더의 `night-chests-sculpt-v3.zip`에는 편집용 BLEND, GLB, FBX, 텍스처, 제작 스크립트, 정투영 도면, 최초 컨셉과 비교 페이지가 들어 있다. 압축을 풀어 `comparison.html`과 원본 README를 볼 수 있다. `preview.png`는 실제 모델의 렌더다.

## 다음 적용 작업

Unity에서 프로젝트를 열고 새 FBX와 PNG를 임포트한다. `.meta` 파일은 Unity가 생성한다. URP/Lit 재질을 만들고 아래 맵을 연결한다.

| 슬롯 | 텍스처 / 설정 |
|---|---|
| Base Map | Albedo, Base Color 흰색 |
| Normal Map | Normal, Texture Type=Normal Map, 강도 0.25부터 조정 |
| Occlusion | AO, sRGB 끄기 |
| Metallic Map | UnityMetallicSmoothness, sRGB 끄기, Smoothness source=Metallic Alpha, 배율 1 |
| Emission | T3_Rune의 Emission 맵, HDR 강도 조정 |

Metallic과 Roughness 개별 맵도 제공한다. UnityMetallicSmoothness는 R=Metallic, A=1−Roughness로 묶은 맵이다.

Collider와 Animator, 기존 상자 로직을 연결하고 프리팹을 만든다. Blender는 미터 단위이며 FBX는 Y-up으로 내보냈다. 실제 캐릭터에 맞는 크기, 경첩 회전축, 밤 조명과 발광을 에디터에서 확인한다.

## 현재 상태

모델·텍스처 파일을 배치했고 `copy-manifest.json`에 원본과 복사본이 일치하는 SHA256을 기록했다. Unity CLI와 실행 중인 에디터가 없어 실제 임포트·URP 표시·애니메이션 재생은 이번 작업에서 확인하지 않았다. 재질·프리팹 생성이나 기존 ItemBox 프리팹 교체는 하지 않았다.

원본 패키지의 BLEND·FBX·GLB 재임포트 검사 결과는 ZIP 안의 `validation.json`에 있다. 세 상자의 게임 메시 삼각형 수는 7,924 / 16,644 / 34,942이며 LOD는 없다. 컨셉의 세부 곡선과 손그림 질감에는 차이가 남는다.
