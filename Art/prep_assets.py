"""BLOOD & BEAN — Codex imagegen 산출물을 게임 에셋으로 가공한다.

  [주의] 현재 게임 에셋은 **PixelLab API(Art/pixellab_gen.py)** 로 생성한다.
  이 스크립트는 Codex imagegen 시절의 파이프라인이고, 두 스크립트가 같은
  Art/assets/ 에 쓰기 때문에 이걸 그냥 돌리면 PixelLab 산출물을 덮어쓴다.
  여기 있는 팔레트·트림·양자화·배포 함수는 pixellab_gen.py 가 그대로 재사용하므로
  지우지 말 것 — 진입점(main)만 쓰지 않는다.


Codex는 각 에셋을 "배경 + 오브젝트 하나"로 생성하고, 경로를 Art/assets/MANIFEST.md
표에 적어 둔다. (단색 마젠타 배경을 요청했지만 imagegen이 지키지 않아, 배경 제거는
색 키잉이 아니라 플러드 필로 한다.) 이 스크립트가 하는 일:

  1. MANIFEST.md에서 (에셋 이름 -> 임시 경로) 표를 읽는다
  2. 배경을 국소 허용치 플러드 필로 잘라 투명으로 만든다
  3. 내용물 바운딩 박스로 트림
  4. 에셋 종류별 목표 크기로 축소 (LANCZOS) 후 46색 팔레트로 양자화 — 다시 도트로 세운다
  5. Art/assets/<name>.png 로 저장하고, 게임이 읽는 Prototype/assets/ 로 복사한 뒤
     Prototype/game.js 의 ASSET_NAMES 목록을 갱신한다

실행: python Art/prep_assets.py
"""
import io as _io
import os
import re
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ASSETS = os.path.join(HERE, "assets")
MANIFESTS = [os.path.join(ASSETS, n) for n in
             ("MANIFEST.md", "MANIFEST_WALK.md", "MANIFEST_PROPS2.md", "MANIFEST_REST.md")]
GAME_JS = os.path.join(ROOT, "Prototype", "game.js")
GAME_ASSETS = os.path.join(ROOT, "Prototype", "assets")

# 아트컨셉 v2.1 §3 — 46색 11램프
RAMPS = {
    "soot":  ["#06060B", "#10101A", "#1C1C26", "#2B2B33", "#3C3F40"],
    "amber": ["#331109", "#562713", "#78381A", "#A95A2A", "#E09A45", "#FDCD6F"],
    "wood":  ["#2A2214", "#453E23", "#6B5733", "#8D7A58"],
    "bone":  ["#4A443B", "#6E6656", "#A2957E", "#E8DCC8"],
    "dusk":  ["#050710", "#08101A", "#0D1826", "#121D29", "#202D40", "#3A4A60"],
    "rot":   ["#24301C", "#4A5A2A", "#7A9A4A", "#A8C46A"],
    "toxic": ["#2A6B58", "#5FD3A6"],
    "blood": ["#2A0C0A", "#5E1A12", "#9B2E28", "#D9463C"],
    "skin":  ["#4E3828", "#7A5C42", "#C9B79A", "#E5D4B8"],
    "steel": ["#1C2024", "#343C44", "#5E6A74", "#9AA8B2"],
    "glass": ["#26343A", "#4A6A72", "#8FBFC4"],
}
PAL = [tuple(int(h[i:i + 2], 16) for i in (1, 3, 5)) for r in RAMPS.values() for h in r]

# 논리 해상도 1280x720 / 타일 128x64 기준 목표 크기 (높이 px). 폭은 비율 유지.
TARGET_H = {
    "player_idle": 150, "player_carry": 150, "guest_a": 146, "guest_b": 146,
    "guest_c": 146, "zombie_idle": 150, "raider_idle": 152,
    "counter": 108, "grinder": 120, "espresso_machine": 128, "serving_station": 112,
    "bean_shelf": 168, "cafe_table": 104, "metal_container": 96,
    "drawer_chest": 96, "random_box": 92,
    # 콜드탱크는 일부러 크게 — 가장 오래 걸리는 공정이라 멀리서도 구분돼야 한다
    "cold_brew_tank": 152, "steam_wand": 116,
    "guest_d": 146, "corpse": 74, "barrel": 104,
    # 건물은 타일 1칸이지만 존의 랜드마크다 — 인물보다 확실히 크게
    "cafe_home": 176, "cafe_rival": 176,
    "tile_cafe_floor": 64, "tile_zone_floor": 64,
    # 걷기 프레임 — 대응 idle과 높이가 같아야 한다. 다르면 재생 중 키가 튄다.
    "player_walk_0": 150, "player_walk_1": 150, "player_walk_2": 150, "player_walk_3": 150,
    "zombie_walk_0": 150, "zombie_walk_1": 150,
    "raider_walk_0": 152, "raider_walk_1": 152,
    "guest_a_walk_0": 146,
}
DEFAULT_H = 128
# 타일은 비율 유지가 아니라 정확한 크기여야 한다. 논리 64x32의 2배 = 128x64.
# 생성 결과가 2:1이 아니면 바닥 이음새가 어긋난다.
# 타일은 정확히 2:1 이어야 이음새가 맞는다. 벽은 인접 타일 간격(64 device px)의
# 2배 폭이라야 겹쳐 깔리면서 틈이 안 생긴다.
TARGET_WH = {"tile_cafe_floor": (128, 64), "tile_zone_floor": (128, 64),
             "wall_cafe": (128, 152), "wall_ruined": (128, 152)}


def read_manifest():
    """MANIFEST*.md 의 | asset | temp_path | 표를 전부 읽는다."""
    files = [f for f in MANIFESTS if os.path.exists(f)]
    if not files:
        sys.exit("MANIFEST 파일이 없습니다: " + ", ".join(MANIFESTS))
    rows = []
    lines = []
    for f in files:
        lines.extend(_io.open(f, encoding="utf-8").readlines())
    for line in lines:
        if not line.strip().startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 2:
            continue
        name, path = cells[0], cells[1]
        if not name or name.lower() in ("asset", "---") or set(name) <= set("-: "):
            continue
        path = path.strip("`").replace("\\\\", "\\")   # MANIFEST가 역슬래시를 이스케이프해 적는다
        if path.lower().endswith(".png"):
            rows.append((name, path))
    # 같은 이름이 여러 매니페스트에 있으면 **나중 것이 이긴다** (재생성본이 최신이다).
    # 이걸 안 하면 캐시가 옛 항목을 먼저 구워버려 재생성이 반영되지 않는다.
    latest = {}
    for name, path in rows:
        latest[name] = path
    return list(latest.items())


def key_background(im, tol=26):
    """배경을 투명으로.

    imagegen은 '단색 마젠타 배경' 지시를 무시하고 글로우가 깔린 어두운 배경을
    그려 준다. 그래서 색으로 자르지 않고 **국소 허용치 플러드 필**로 자른다 —
    배경 그라디언트는 이웃 간 색차가 미미하고, 캐릭터에는 하드 아웃라인이 있어
    거기서 번짐이 멈춘다. 씨앗 색과 비교하면 그라디언트를 따라가지 못한다.
    """
    from collections import deque
    im = im.convert("RGB")
    w, h = im.size
    px = im.load()
    bg = bytearray(w * h)
    dq = deque()
    def seed(x, y):
        i = y * w + x
        if not bg[i]:
            bg[i] = 1
            dq.append((x, y))
    for x in range(w):
        seed(x, 0); seed(x, h - 1)
    for y in range(h):
        seed(0, y); seed(w - 1, y)
    while dq:
        x, y = dq.popleft()
        r0, g0, b0 = px[x, y]
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if nx < 0 or ny < 0 or nx >= w or ny >= h:
                continue
            i = ny * w + nx
            if bg[i]:
                continue
            r, g, b = px[nx, ny]
            if abs(r - r0) + abs(g - g0) + abs(b - b0) < tol:
                bg[i] = 1
                dq.append((nx, ny))
    out = Image.new("RGBA", (w, h))
    op = out.load()
    cut = 0
    for y in range(h):
        base = y * w
        for x in range(w):
            r, g, b = px[x, y]
            # 플러드 필은 '둘러싸인' 배경에 못 닿는다 (예: 탱크 기둥 사이).
            # 마젠타는 오브젝트에 쓰지 말라고 지시했으므로 색으로도 한 번 더 자른다.
            magenta = r > 150 and b > 150 and g < 110 and abs(r - b) < 80
            if bg[base + x] or magenta:
                op[x, y] = (0, 0, 0, 0)
                cut += 1
            else:
                op[x, y] = (r, g, b, 255)
    if cut < w * h * 0.25:
        raise ValueError("배경이 %.0f%%밖에 안 잘렸습니다 — 오브젝트가 테두리에 붙었을 수 있음" % (cut / (w * h) * 100))
    return out


def trim(im):
    bbox = im.getbbox()
    return im.crop(bbox) if bbox else im


def drop_specks(im, min_alpha=40):
    """키잉 후 남는 반투명 먼지 제거 — 알파가 낮으면 완전 투명으로."""
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < min_alpha:
                px[x, y] = (0, 0, 0, 0)
            elif a < 255:
                px[x, y] = (r, g, b, 255)      # 반투명 금지 — 도트는 이진 알파
    return im


def quantize(im):
    """46색 팔레트로 양자화(디더 없음). 축소로 뭉개진 색을 다시 도트로 세운다."""
    pal_img = Image.new("P", (1, 1))
    flat = []
    for c in PAL:
        flat.extend(c)
    # 남는 팔레트 칸을 검정으로 채우면 양자화가 거기로 매핑돼 팔레트 밖 색이 생긴다.
    # 마지막 색을 반복해 46색 밖으로 나갈 수 없게 만든다.
    flat.extend(list(PAL[-1]) * (256 - len(PAL)))
    pal_img.putpalette(flat)
    rgb = im.convert("RGB").quantize(palette=pal_img, dither=Image.Dither.NONE)
    out = rgb.convert("RGBA")
    out.putalpha(im.getchannel("A"))
    return out


def process(name, path):
    im = Image.open(path)
    im = key_background(im)
    im = trim(im)
    im = drop_specks(im)
    im = trim(im)
    if im.width == 0 or im.height == 0:
        raise ValueError("키잉 후 남은 픽셀이 없습니다 — 배경이 마젠타가 아닌 듯")
    if name in TARGET_WH:
        tw, th = TARGET_WH[name]
    else:
        th = TARGET_H.get(name, DEFAULT_H)
        tw = max(1, round(im.width * th / im.height))
    im = im.resize((tw, th), Image.LANCZOS)
    im = drop_specks(im, 110)
    im = quantize(im)
    return im


def publish(assets):
    """PNG를 게임 폴더로 복사하고 game.js 의 ASSET_NAMES 목록을 맞춘다."""
    os.makedirs(GAME_ASSETS, exist_ok=True)
    for name, im in assets:
        im.save(os.path.join(GAME_ASSETS, name + ".png"))
    if not os.path.exists(GAME_JS):
        print("  (game.js 가 없어 목록 갱신은 건너뜁니다)")
        return
    src = _io.open(GAME_JS, encoding="utf-8").read()
    m = re.search(r"const ASSET_NAMES=\[.*?\];", src, re.S)
    if not m:
        print("  (ASSET_NAMES 를 못 찾아 목록 갱신은 건너뜁니다)")
        return
    names = sorted(n for n, _ in assets)
    decl = "const ASSET_NAMES=[\n  " + ",\n  ".join("'" + n + "'" for n in names) + "\n];"
    _io.open(GAME_JS, "w", encoding="utf-8").write(src[:m.start()] + decl + src[m.end():])
    print("  Prototype/assets/ 에 %d개 복사 + game.js 목록 갱신" % len(assets))


def main():
    rows = read_manifest()
    if not rows:
        sys.exit("MANIFEST.md 에서 에셋 표를 못 찾았습니다.")
    os.makedirs(ASSETS, exist_ok=True)
    done, failed = [], []
    for name, path in rows:
        out = os.path.join(ASSETS, name + ".png")
        # 플러드 필이 장당 수 초 걸린다. 원본이 그대로면 다시 하지 않는다.
        # 다시 굽고 싶으면 Art/assets/<name>.png 를 지우면 된다.
        if os.path.exists(out) and os.path.getmtime(out) >= os.path.getmtime(path):
            im = Image.open(out).convert("RGBA")
            done.append((name, im))
            print("  --   %-18s %dx%d (캐시)" % (name, im.width, im.height))
            continue
        try:
            im = process(name, path)
        except Exception as ex:
            failed.append((name, str(ex)))
            print("  FAIL %-18s %s" % (name, ex))
            continue
        im.save(out)
        done.append((name, im))
        print("  ok   %-18s %dx%d" % (name, im.width, im.height))

    # ── 자체 검증 ───────────────────────────────────────────
    palset = set(PAL)
    for name, im in done:
        cols = {c[:3] for c in im.getdata() if c[3] > 0}
        bad = cols - palset
        assert not bad, "%s: 팔레트 밖 색 %d개 (%s)" % (name, len(bad), list(bad)[:3])
        assert {c[3] for c in im.getdata()} <= {0, 255}, "%s: 반투명 픽셀이 남음" % name
        assert im.width > 4 and im.height > 4, "%s: 너무 작음" % name
        if name in TARGET_WH:
            assert (im.width, im.height) == TARGET_WH[name],                 "%s: 타일이 %dx%d (2:1 아님)" % (name, im.width, im.height)
    print("\n%d개 성공 / %d개 실패" % (len(done), len(failed)))
    if done:
        publish(done)
    if failed:
        print("실패:", ", ".join(n for n, _ in failed))


if __name__ == "__main__":
    main()
