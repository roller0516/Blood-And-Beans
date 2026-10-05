import json
from pathlib import Path
from shutil import copy2
from PIL import Image

root = Path(__file__).resolve().parent
target = root.parent / 'luigi-ui-gallery' / 'dist' / 'images'
items = json.loads((root / 'assets.json').read_text(encoding='utf-8-sig'))
for item in items:
    source = Path(item['source'])
    original = root / (item['name'] + '.png')
    copy2(source, original)
    with Image.open(source) as img:
        destination = target / (item['name'] + '.webp')
        img.save(destination, format='WEBP', lossless=True, method=6)
        with Image.open(destination) as encoded:
            assert encoded.size == img.size
            assert encoded.convert('RGBA').tobytes() == img.convert('RGBA').tobytes()
        print(item['name'], img.size, source.stat().st_size, destination.stat().st_size)
