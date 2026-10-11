# Git에서 제외한 갤러리 파일 재생성

`assets.zip`과 `blood-beans-luigi-portable.html`은 배포용 생성물이라 Git에서 제외해도 됩니다. 다른 PC에는 자동으로 내려오지 않으므로 필요할 때 재생성하거나 별도로 전달합니다.

Git에는 `claude-import.html`(갤러리 코드), `gallery-assets.json`, `rebuild-portable.py`, `fonts/`(TTF와 OFL), PNG 원본 폴더들을 함께 보관합니다. `claude-import.html`은 공개 갤러리에서 이미지를 읽는 가벼운 버전이며, 독립형 HTML은 이미지와 폰트를 내장합니다.

Python과 Pillow가 있는 환경에서 이 폴더를 기준으로 실행합니다.

```powershell
python rebuild-portable.py --check-inputs-only
python rebuild-portable.py
```

첫 번째 명령은 원본 누락만 확인합니다. 두 번째는 이미지 내용 변경 없이 무손실 WebP로 변환한 뒤 두 배포용 파일을 다시 만듭니다. 개인 PC의 절대 경로나 `.codex/generated_images`, `output/` 폴더가 필요하지 않습니다.

시안을 추가하면 갤러리 코드와 원본 PNG, `gallery-assets.json`을 같이 갱신합니다. 생성 이미지에 들어간 글자는 콘셉트이며 실제 Unity UI 글자는 TMP로 별도 적용합니다. Unity 적용 순서는 `LUIGI_IMPLEMENTATION_GUIDE.md`를 참고합니다.
