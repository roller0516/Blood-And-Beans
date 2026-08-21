"""에셋이 아이소 타일 위에 제대로 앉는지 검증한다.

게임과 똑같은 규칙으로 배치한다:
  - 타일은 128 × 64 (2:1) 마름모
  - 스프라이트는 **하단 중앙**이 타일 중심에 오도록 그린다
그 위에 타일 마름모 외곽선을 겹쳐 그려서, 발/바닥 면이 마름모와 맞는지 눈으로 본다.

같이 뽑는 것:
  out/_fit_props.png   소품이 타일 위에 앉은 모습 + 마름모 외곽선
  out/_fit_chars.png   인물이 타일 중앙에 서 있는 모습
  out/_tile2x2_*.png   바닥 타일을 2×2로 이어 붙인 것 (이음새 확인)

실행: python Art/verify_fit.py
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
A = os.path.join(HERE, "assets")
OUT = os.path.join(HERE, "out")
TW, TH = 128, 64                       # 타일 크기 (device px)

BG = (18, 18, 24)
LINE = (255, 205, 111)                 # amber-5 — 마름모 외곽선
LINE2 = (95, 211, 166)                 # toxic-1 — 스프라이트 바운딩 박스

BLDGS = ["cafe_home", "cafe_rival"]      # 건물은 일부러 타일보다 크다 — 존의 랜드마크
PROPS = ["counter", "grinder", "espresso_machine", "steam_wand", "cold_brew_tank",
         "bean_shelf", "cafe_table", "metal_container", "drawer_chest",
         "random_box", "barrel", "corpse"]
CHARS = ["player_idle", "guest_a", "guest_b", "guest_c", "guest_d",
         "zombie_walk_0", "raider_walk_0"]
TILES = ["tile_cafe_floor", "tile_zone_floor"]


def load(name):
    p = os.path.join(A, name + ".png")
    return Image.open(p).convert("RGBA") if os.path.exists(p) else None


def diamond_outline(dr_img, cx, cy, w=TW, h=TH, col=LINE):
    """2:1 마름모 외곽선을 1px로. 게임의 diamond() 와 같은 계단이다."""
    px = dr_img.load()
    rows = h
    half = rows // 2
    for r in range(rows):
        hw = (r + 1) if r < half else (rows - r)
        hw = hw * (w // h) * 2 // 2                      # 2:1 이므로 행당 폭 = (r+1)*2*(w/h)/2
        hw = int((r + 1 if r < half else rows - r) * (w / h))
        y = cy - half + r
        for x in (cx - hw, cx - hw + 1, cx + hw - 2, cx + hw - 1):
            if 0 <= x < dr_img.width and 0 <= y < dr_img.height:
                px[x, y] = col + (255,)


def place(sheet, im, cx, cy, anchor="base"):
    """게임의 drawSpr() 과 같은 규칙.
    base = 밑면 마름모가 타일 마름모에 겹치게 (소품)
    foot = 발이 타일 중심에 (인물)"""
    dy = TH // 2 if anchor == "base" else 0
    sheet.alpha_composite(im, (cx - im.width // 2, cy + dy - im.height))


def sheet_for(names, cols, cell_w, cell_h, title_h=0):
    rows = (len(names) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell_w, rows * cell_h + title_h), BG + (255,))
    return sheet, rows


def build(names, fname, cols=4, cell_w=300, cell_h=300, box=True, anchor='base'):
    sheet, rows = sheet_for(names, cols, cell_w, cell_h)
    report = []
    for i, n in enumerate(names):
        im = load(n)
        if im is None:
            report.append((n, None))
            continue
        cx = (i % cols) * cell_w + cell_w // 2
        cy = (i // cols) * cell_h + cell_h - 70
        # 타일 3개를 깔아 기준을 만든다 (가운데 + 좌우)
        for dx in (-TW // 2, 0, TW // 2):
            diamond_outline(sheet, cx + dx, cy + (TH // 4 if dx else 0),
                            col=(60, 60, 74) if dx else LINE)
        place(sheet, im, cx, cy, anchor)
        diamond_outline(sheet, cx, cy, col=LINE)          # 스프라이트 위에 다시 — 겹침을 본다
        if box:
            px = sheet.load()
            x0, y0 = cx - im.width // 2, cy - im.height
            for x in range(max(0, x0), min(sheet.width, x0 + im.width)):
                for y in (y0, y0 + im.height - 1):
                    if 0 <= y < sheet.height:
                        px[x, y] = LINE2 + (255,)
        report.append((n, (im.width, im.height)))
    sheet.convert("RGB").save(os.path.join(OUT, fname))
    return report


def tile2x2(name):
    im = load(name)
    if im is None:
        return None
    w, h = im.size
    # 아이소 마름모는 반 칸씩 어긋나게 물린다 — 실제 배치와 같게 이어 본다
    t = Image.new("RGBA", (w * 2, h * 2), (0, 0, 0, 255))
    for r in range(4):
        for c in range(4):
            x = (c - r) * (w // 2) + w // 2
            y = (c + r) * (h // 2) - h // 2
            t.alpha_composite(im, (x, y))
    t.convert("RGB").save(os.path.join(OUT, "_tile2x2_%s.png" % name))
    return t.size


def main():
    os.makedirs(OUT, exist_ok=True)
    rp = build(PROPS, "_fit_props.png", cols=4, cell_w=300, cell_h=300)
    rb = build(BLDGS, "_fit_bldgs.png", cols=2, cell_w=420, cell_h=400)
    rc = build(CHARS, "_fit_chars.png", cols=4, cell_w=260, cell_h=280, anchor="foot")
    print("타일 기준: %d x %d (2:1)" % (TW, TH))
    print("\n[소품] 폭이 %d 를 크게 넘으면 이웃 타일을 침범한다" % TW)
    for n, s in rp:
        if not s:
            print("  없음 %s" % n); continue
        flag = "  <-- 타일보다 넓음" if s[0] > TW * 1.35 else ""
        print("  %-18s %3d x %3d%s" % (n, s[0], s[1], flag))
    print("\n[인물] 폭은 타일 절반(%d) 안쪽이 정상" % (TW // 2))
    for n, s in rc:
        if not s:
            print("  없음 %s" % n); continue
        flag = "  <-- 넓음" if s[0] > TW * 0.7 else ""
        print("  %-18s %3d x %3d%s" % (n, s[0], s[1], flag))
    print("\n[건물] 타일보다 큰 건 정상. 밑면 받침이 마름모에 물리는지만 본다")
    for n, s in rb:
        print("  %-18s %s" % (n, s))
    print("\n[타일 이음새]")
    for t in TILES:
        z = tile2x2(t)
        print("  %-18s %s" % (t, z))
    print("\n-> Art/out/_fit_props.png, _fit_chars.png, _tile2x2_*.png 를 눈으로 확인")


if __name__ == "__main__":
    main()
