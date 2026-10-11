# 실제 폰트 — 조합과 상업용 이용 조건

확인일: 2026-10-12. 기존 갤러리 상단의 공통 폰트 설정으로 전체 화면에 적용합니다.

| 조합 | 제목·상태 | 본문·안내 | 숫자·영문 |
| --- | --- | --- | --- |
| 간판형 · 추천 | 카페24 써라운드 | 배달의민족 도현 | Lilita One |
| 괴짜 호텔형 | 카페24 슈퍼매직 Bold | 배달의민족 도현 | Lilita One |
| 힘 있는 형 | 검은고딕 · Black Han Sans | 배달의민족 도현 | Lilita One |
| 이전 조합 | Bagel Fat One | 주아 · Jua | Coiny |

제목과 숫자는 크림색 면, 짙은 초록 외곽선, 짧은 황동색 하단 음영으로 입체감을 표현합니다. 작은 본문은 획이 뭉치지 않도록 효과를 줄였습니다. 서체는 이미지에 구운 글자가 아니라 실제 TTF에서 렌더링하며 상단에서 역할별로 바꿀 수 있습니다. 기존 시안 글자도 비교 모드로 유지합니다. 브랜드 로고는 고정 아트입니다.

## 상업적 사용과 배포

8개 폰트는 모두 SIL OFL 1.1로 상업용 게임에 포함해 사용할 수 있습니다. 개인·기업 모두 사용할 수 있고 폰트 자체를 단독 판매할 수 없습니다. 게임 소스를 공개할 의무는 없습니다. 게임 배포에 폰트를 포함할 때 해당 저작권 고지와 OFL 전문을 함께 제공하세요. 현재 `fonts/*-OFL.txt`에 포함되어 있으며 HTML의 라이선스 안내에서도 읽을 수 있습니다.

카페24 두 폰트는 공식 ZIP에 동봉된 `License-Ssurround.pdf`와 `License-Supermagic.pdf`에서 OFL 1.1, 무료 사용, 수정·재배포, 소프트웨어 번들·임베딩 허용을 확인했습니다. 해당 안내 전문과 저작권, OFL 전문은 각 `Cafe24*-OFL.txt`에도 보관했습니다. 동봉 안내에는 사용된 인쇄물·광고물 이미지의 카페24 홍보 활용 및 원치 않을 때 요청할 수 있다는 문구도 있습니다. 공식 안내 PDF를 함께 참고하세요.

폰트 파일은 수정하지 않았습니다. 개작·재배포할 경우 OFL 유지 및 해당 파일의 Reserved Font Name 조건을 확인하세요. 이번 외곽선과 그림자는 렌더링 효과입니다.

## 공식 출처

- [카페24 공식 폰트](https://fonts.cafe24.com/)
- [카페24 써라운드 공식 ZIP](https://img.cafe24.com/csdstatic/freefonts/download/kr/Cafe24Ssurround-v2.0.zip)
- [카페24 슈퍼매직 Bold 공식 ZIP](https://img.cafe24.com/csdstatic/freefonts/download/kr/Cafe24Supermagic-Bold-v1.0.zip)
- [검은고딕 OFL](https://github.com/google/fonts/blob/main/ofl/blackhansans/OFL.txt)
- [도현 OFL](https://github.com/google/fonts/blob/main/ofl/dohyeon/OFL.txt)
- [Lilita One OFL](https://github.com/google/fonts/blob/main/ofl/lilitaone/OFL.txt)
- [Bagel Fat One OFL](https://github.com/google/fonts/blob/main/ofl/bagelfatone/OFL.txt)
- [주아 OFL](https://github.com/google/fonts/blob/main/ofl/jua/OFL.txt)
- [Coiny OFL](https://github.com/google/fonts/blob/main/ofl/coiny/OFL.txt)
- [OFL 공식 FAQ](https://openfontlicense.org/ofl-faq/)

## 글리프·파일·Unity 연결

42개 템플릿의 792개 문구 및 동적 상태 문구를 확인합니다. 선택한 서체에 없는 글리프는 포함된 Bagel Fat One으로 폴백합니다. Coiny와 Lilita One은 숫자·영문 역할용이며 한글은 폴백으로 표시됩니다. `global-font-validation.json`에 각 TTF의 SHA-256과 폴백 문자가 기록됩니다.

`blood-beans-luigi-portable.html`에는 기존 85개 이미지, 8개 TTF와 고지가 포함됩니다. `claude-import.html`이 인라인 소스이며 `rebuild-portable.py`로 단일 HTML과 `assets.zip`을 재생성합니다. 공통 설정은 Unity 전체 18개 화면·7개 절제형·다인 팀 선택·4종 캐릭터의 4개 화면에 적용됩니다. 별도 미리보기 페이지를 새로 만들지 않습니다.

Unity 적용 시 제목·본문·숫자별 TMP Font Asset을 만들고 Bagel Fat One 폴백과 공용 Material Preset을 연결합니다. SDF 패딩, Outline, Underlay와 최종 픽셀 크기는 실제 게임에서 확인해야 합니다. 이번 작업은 브라우저 미리보기이며 Unity 프로젝트의 에셋이나 프리팹은 수정하지 않았습니다.
