"""Rebuild ignored gallery deliverables from tracked local sources. Requires Pillow."""
from pathlib import Path
from io import BytesIO
import base64, json, re, sys, zipfile
from PIL import Image

root = Path(__file__).resolve().parent
manifest = json.loads((root / 'gallery-assets.json').read_text(encoding='utf-8'))
missing = [row['source'] for row in manifest if not (root / row['source']).is_file()]
if missing:
    raise SystemExit('Missing source files: ' + ', '.join(missing))
if '--check-inputs-only' in sys.argv:
    print(f'All {len(manifest)} source images exist; no output changed.')
    raise SystemExit(0)

images = {}
encoded_files = {}
for row in manifest:
    with Image.open(root / row['source']) as original:
        buffer = BytesIO()
        original.save(buffer, format='WEBP', lossless=True, method=6)
        encoded_files[row['url']] = buffer.getvalue()
        images[row['url']] = 'data:image/webp;base64,' + base64.b64encode(buffer.getvalue()).decode('ascii')

remote = 'https://blood-beans-luigi-ui.free-earth-2790.chatgpt.site/'
page = (root / 'claude-import.html').read_text(encoding='utf-8')
resolver = 'window.imageSource=path=>' + json.dumps(remote) + '+path;'
if resolver not in page:
    raise SystemExit('Unexpected claude-import.html resolver; update rebuild script before continuing.')
page = page.replace(resolver, 'const PORTABLE_IMAGES=' + json.dumps(images, separators=(',', ':')) + ';window.imageSource=path=>PORTABLE_IMAGES[path]||path;')
page = page.replace(remote + 'images/bags-light-v2.webp', images['images/bags-light-v2.webp'])
for font in (root / 'fonts').glob('*.ttf'):
    page = page.replace(remote + 'fonts/' + font.name, 'data:font/ttf;base64,' + base64.b64encode(font.read_bytes()).decode('ascii'))
(root / 'blood-beans-luigi-portable.html').write_text(page, encoding='utf-8')
with zipfile.ZipFile(root / 'assets.zip', 'w', compression=zipfile.ZIP_DEFLATED) as archive:
    for name, data in encoded_files.items():
        archive.writestr(name, data)
    for font in (root / 'fonts').iterdir():
        if font.is_file():
            archive.write(font, 'fonts/' + font.name)
print(f'Built portable HTML and assets.zip with {len(images)} lossless images.')
