"""Package the font preview with unmodified TTF files, images and OFL notices."""
from pathlib import Path
import base64, hashlib, html, json, struct

root = Path(__file__).resolve().parent

def uri(path, mime):
    return 'data:' + mime + ';base64,' + base64.b64encode(path.read_bytes()).decode('ascii')

def font_names(data):
    count = struct.unpack_from('>H', data, 4)[0]
    offset = None
    for i in range(count):
        tag, _, base, length = struct.unpack_from('>4sIII', data, 12 + i * 16)
        if tag == b'name':
            offset = base
            break
    _, records, strings = struct.unpack_from('>HHH', data, offset)
    names = {}
    for i in range(records):
        platform, encoding, language, key, length, start = struct.unpack_from('>HHHHHH', data, offset + 6 + i * 12)
        if key not in (0, 1, 4, 13, 14):
            continue
        raw = data[offset + strings + start:offset + strings + start + length]
        value = raw.decode('utf-16-be' if platform in (0, 3) else 'mac_roman', errors='replace')
        names.setdefault(str(key), [])
        if value not in names[str(key)]:
            names[str(key)].append(value)
    return names

def cmap_glyph(data, point):
    count = struct.unpack_from('>H', data, 4)[0]
    tables = {}
    for i in range(count):
        tag, _, offset, length = struct.unpack_from('>4sIII', data, 12 + i * 16)
        tables[tag] = (offset, length)
    start, _ = tables[b'cmap']
    subtables = struct.unpack_from('>H', data, start + 2)[0]
    for i in range(subtables):
        platform, encoding, offset = struct.unpack_from('>HHI', data, start + 4 + i * 8)
        if platform not in (0, 3):
            continue
        base = start + offset
        fmt = struct.unpack_from('>H', data, base)[0]
        if fmt == 12:
            groups = struct.unpack_from('>I', data, base + 12)[0]
            for j in range(groups):
                lo, hi, glyph = struct.unpack_from('>III', data, base + 16 + j * 12)
                if lo <= point <= hi:
                    return glyph + point - lo
        elif fmt == 4 and point <= 65535:
            segments = struct.unpack_from('>H', data, base + 6)[0] // 2
            end = base + 14
            begin = end + 2 * segments + 2
            delta = begin + 2 * segments
            ranges = delta + 2 * segments
            for j in range(segments):
                hi = struct.unpack_from('>H', data, end + 2 * j)[0]
                lo = struct.unpack_from('>H', data, begin + 2 * j)[0]
                if lo <= point <= hi:
                    d = struct.unpack_from('>h', data, delta + 2 * j)[0]
                    r = struct.unpack_from('>H', data, ranges + 2 * j)[0]
                    if not r:
                        return (point + d) & 65535
                    glyph = struct.unpack_from('>H', data, ranges + 2 * j + r + 2 * (point - lo))[0]
                    return (glyph + d) & 65535 if glyph else 0
    return 0

source = (root / 'real-font-preview.source.html').read_text(encoding='utf-8')
font_specs = [('BAGEL', 'BagelFatOne', '가방 스킬 귀환 상태 예시 빈 적재 중 분실 회수 필요 일차 야간 탐색0123456789KGSPACE'), ('JUA', 'Jua', '밤이 끝나기 전에 돌아오세요 팀 매출 가방 준비 대기 상태 예시'), ('COINY', 'Coiny', '0123456789:/KGSPACEQ')]
report = []
licenses = []
for token, stem, sample in font_specs:
    font = root / 'fonts' / (stem + '-Regular.ttf')
    license_path = root / 'fonts' / (stem + '-OFL.txt')
    notice = license_path.read_text(encoding='utf-8')
    assert 'SIL OPEN FONT LICENSE Version 1.1' in notice
    data = font.read_bytes()
    missing = sorted(set(c for c in sample if not c.isspace() and not cmap_glyph(data, ord(c))))
    assert not missing, (stem, missing)
    metadata = font_names(data)
    expected_family = {'BagelFatOne': 'Bagel Fat One', 'Jua': 'Jua', 'Coiny': 'Coiny'}[stem]
    assert expected_family in metadata['1'], metadata
    report.append({'file': str(font), 'sha256': hashlib.sha256(data).hexdigest(), 'license': 'SIL OFL 1.1', 'copyright': notice.splitlines()[0], 'font_name_table': metadata, 'sample': sample, 'missing_glyphs': missing})
    source = source.replace('@@' + token + '@@', uri(font, 'font/ttf'))
    licenses.append('<h3>' + stem + '</h3><pre>' + html.escape(notice) + '</pre>')
images = {
    'NIGHT': root / 'unity-gameplay/real-font-night-template.png',
    'PARTS': root / 'unity-gameplay/real-font-parts-template.png',
    'ORIGINAL_NIGHT': root / 'unity-concepts-v2/unity-luigi-v2-night.png',
    'ORIGINAL_PARTS': root / 'unity-gameplay/night-components.png',
}
for token, image in images.items():
    source = source.replace('@@' + token + '@@', uri(image, 'image/png'))
source = source.replace('@@LICENSES@@', '\n'.join(licenses))
assert '@@' not in source
(root / 'real-font-preview.html').write_text(source, encoding='utf-8')
(root / 'real-font-validation.json').write_text(json.dumps({'font_files_unmodified': True, 'embedded_full_OFL_notices': True, 'fonts': report, 'images': {k: str(v) for k, v in images.items()}}, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'output': str(root / 'real-font-preview.html'), 'bytes': len(source.encode('utf-8')), 'all_font_samples_have_glyphs': True}, ensure_ascii=False))
