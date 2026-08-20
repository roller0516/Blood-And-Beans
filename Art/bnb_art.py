"""BLOOD & BEAN HD 도트 아트 에셋 생성기 (Python 3.12 + Pillow 12).

실행: python Art/bnb_art.py
출력: Art/out/ 아래 PNG, GPL, Unity 슬라이싱 JSON
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw


# 아트 바이블 §3: 10개 램프 × 4단. 이 딕셔너리 밖의 RGB는 사용하지 않는다.
# ponytail: 이 40색은 Codex 생성기가 자체적으로 잡은 팔레트다. 프로젝트의 공식
# 팔레트는 Art/make_palette.py 의 46색(컨셉 아트 실측본)이고, 실측치가 컨셉 재현
# 오차에서 이긴다(낮 14.5 vs 19.1 / 밤 7.3 vs 11.6). 이 생성기를 실제로 쓰게 되면
# 아래 램프 이름(ink/coffee/olive/zombie/teal/red/night)을 공식 램프
# (soot/amber/wood/rot/glass/blood/dusk)로 매핑해 교체할 것.
PALETTE: dict[str, tuple[int, int, int]] = {
    "ink-1": (12, 14, 20), "ink-2": (26, 28, 38), "ink-3": (43, 45, 56), "ink-4": (65, 67, 78),
    "bone-1": (74, 68, 65), "bone-2": (132, 119, 106), "bone-3": (196, 178, 151), "bone-4": (244, 224, 184),
    "wood-1": (62, 32, 25), "wood-2": (105, 55, 34), "wood-3": (157, 91, 48), "wood-4": (213, 145, 73),
    "amber-1": (91, 49, 18), "amber-2": (151, 84, 22), "amber-3": (218, 143, 35), "amber-4": (255, 204, 78),
    "coffee-1": (43, 24, 23), "coffee-2": (77, 40, 31), "coffee-3": (123, 66, 43), "coffee-4": (178, 109, 65),
    "red-1": (70, 24, 30), "red-2": (118, 35, 42), "red-3": (174, 55, 55), "red-4": (226, 91, 70),
    "teal-1": (18, 49, 52), "teal-2": (27, 78, 78), "teal-3": (50, 118, 108), "teal-4": (91, 169, 143),
    "olive-1": (42, 49, 30), "olive-2": (70, 78, 38), "olive-3": (109, 116, 50), "olive-4": (164, 158, 70),
    "zombie-1": (35, 50, 36), "zombie-2": (55, 78, 48), "zombie-3": (86, 112, 64), "zombie-4": (137, 154, 86),
    "night-1": (19, 28, 43), "night-2": (31, 48, 69), "night-3": (52, 75, 99), "night-4": (89, 112, 132),
}
RGBA = {name: rgb + (255,) for name, rgb in PALETTE.items()}
TRANSPARENT = (0, 0, 0, 0)
OUT = Path(__file__).resolve().parent / "out"


def C(name: str) -> tuple[int, int, int, int]:
    return RGBA[name]


def canvas(size: tuple[int, int], bg: str | None = None) -> Image.Image:
    return Image.new("RGBA", size, C(bg) if bg else TRANSPARENT)


# 아트 바이블 §4(4): 2:1 아이소메트릭 다이아, 실제 외곽 64×32.
def diamond(draw: ImageDraw.ImageDraw, x: int, y: int, w: int = 64, h: int = 32,
            fill: str = "bone-2", edge: str | None = None) -> list[tuple[int, int]]:
    pts = [(x, y + h // 2), (x + w // 2, y), (x + w - 1, y + h // 2), (x + w // 2, y + h - 1)]
    draw.polygon(pts, fill=C(fill))
    if edge:
        draw.line(pts + [pts[0]], fill=C(edge), width=1)
    return pts


# 아트 바이블 §4(1): 램프 경계에만 폭 2~4px의 50% 체커 디더.
def dither_edge(img: Image.Image, box: tuple[int, int, int, int], dark: str, light: str,
                axis: str = "h", width: int = 3) -> None:
    x0, y0, x1, y1 = box
    px = img.load()
    for y in range(max(0, y0), min(img.height, y1)):
        for x in range(max(0, x0), min(img.width, x1)):
            depth = (y - y0) if axis == "h" else (x - x0)
            if depth < width and ((x + y) & 1) == 0:
                px[x, y] = C(light if depth == 0 else dark)


# 아트 바이블 §4(2): 알파 실루엣 바깥에만 1px 선택적 컬러 아웃라인.
def outline(img: Image.Image, color: str, omit_dark_side: bool = False, width: int = 1) -> Image.Image:
    src = img.copy()
    sp = src.load()
    out = img.copy()
    op = out.load()
    for y in range(img.height):
        for x in range(img.width):
            if sp[x, y][3] != 0:
                continue
            hit = False
            for dy in range(-width, width + 1):
                for dx in range(-width, width + 1):
                    if abs(dx) + abs(dy) > width or (omit_dark_side and dx > 0):
                        continue
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < img.width and 0 <= ny < img.height and sp[nx, ny][3]:
                        hit = True
            if hit:
                op[x, y] = C(color)
    return out


# 아트 바이블 §4(3): 목재는 1px 결을 면 방향과 평행하게 넣는다.
def wood_grain(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: str = "wood-1") -> None:
    x0, y0, x1, y1 = box
    for y in range(y0 + 3, y1 - 1, 5):
        off = ((y - y0) // 5) % 3
        draw.line((x0 + 3 + off, y, min(x1 - 3, x0 + 12 + off), y), fill=C(color), width=1)
        if x1 - x0 > 20:
            draw.line((x0 + 17, y, x1 - 4, y), fill=C(color), width=1)


# 아트 바이블 §4(3): 금속은 상단 하이라이트와 하단 반사광.
def metal_sheen(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], hi: str = "bone-4",
                reflection: str = "night-3") -> None:
    x0, y0, x1, y1 = box
    draw.line((x0 + 2, y0 + 1, x1 - 3, y0 + 1), fill=C(hi), width=1)
    draw.line((x0 + 3, y1 - 2, x1 - 2, y1 - 2), fill=C(reflection), width=1)


# 아트 바이블 §4(3): 유리는 대각 2px 하이라이트 한 줄만 사용한다.
def glass_sheen(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: str = "bone-4") -> None:
    x0, y0, x1, y1 = box
    draw.line((x0 + 3, y1 - 4, min(x1 - 3, x0 + 12), y0 + 3), fill=C(color), width=2)


# 아트 바이블 §4(4): 위=3단, 좌=2단, 우=1단 자동 셰이딩 아이소 박스.
def iso_box(draw: ImageDraw.ImageDraw, x: int, y: int, w: int, d: int, h: int, ramp: str = "wood",
            edge: str | None = None) -> None:
    top = [(x, y + d // 2), (x + w // 2, y), (x + w - 1, y + d // 2), (x + w // 2, y + d - 1)]
    left = [top[0], top[3], (top[3][0], top[3][1] + h), (top[0][0], top[0][1] + h)]
    right = [top[3], top[2], (top[2][0], top[2][1] + h), (top[3][0], top[3][1] + h)]
    draw.polygon(left, fill=C(f"{ramp}-2"))
    draw.polygon(right, fill=C(f"{ramp}-1"))
    draw.polygon(top, fill=C(f"{ramp}-3"))
    if edge:
        draw.line(top + [top[0]], fill=C(edge), width=1)
        draw.line((top[0], left[3], left[2], top[3], right[2], top[2]), fill=C(edge), width=1)


def paste(dst: Image.Image, src: Image.Image, xy: tuple[int, int]) -> None:
    dst.alpha_composite(src, xy)


def draw_cup(draw: ImageDraw.ImageDraw, x: int, y: int, scale: int = 1) -> None:
    draw.rectangle((x, y, x + 5 * scale, y + 5 * scale), fill=C("bone-3"))
    draw.line((x + scale, y, x + 4 * scale, y), fill=C("bone-4"), width=scale)
    draw.rectangle((x + 5 * scale, y + scale, x + 7 * scale, y + 3 * scale), outline=C("bone-2"), width=scale)
    draw.line((x + scale, y + scale, x + 4 * scale, y + scale), fill=C("coffee-2"), width=scale)


def make_palette() -> None:
    ramps = list(PALETTE)
    im = canvas((4 * 32, 10 * 32))
    d = ImageDraw.Draw(im)
    for row in range(10):
        for step in range(4):
            d.rectangle((step * 32, row * 32, step * 32 + 31, row * 32 + 31), fill=C(ramps[row * 4 + step]))
    im.save(OUT / "gen_palette.png")
    lines = ["GIMP Palette", "Name: BLOOD & BEAN 40", "Columns: 4", "#"]
    for name, rgb in PALETTE.items():
        lines.append(f"{rgb[0]:3d} {rgb[1]:3d} {rgb[2]:3d}\t{name}")
    (OUT / "gen_palette.gpl").write_text("\n".join(lines) + "\n", encoding="utf-8")


def make_tile(kind: int) -> Image.Image:
    im = canvas((64, 64))
    d = ImageDraw.Draw(im)
    ramps = [("bone-3", "wood-1"), ("wood-3", "wood-1"), ("night-3", "ink-2"),
             ("night-2", "ink-1"), ("bone-2", "night-2"), ("coffee-3", "coffee-1")]
    fill, edge = ramps[kind]
    diamond(d, 0, 16, fill=fill, edge=edge)
    dither_edge(im, (2, 30, 62, 34), edge, fill, "h", 3)
    if kind in (0, 1):
        for i in range(6, 58, 9):
            d.line((i, 27, i + 9, 23), fill=C("wood-2" if kind else "bone-2"), width=1)
    elif kind in (2, 3, 4):
        for x, y in ((13, 31), (30, 22), (44, 35), (51, 27)):
            d.rectangle((x, y, x + 2, y + 1), fill=C(edge))
    else:
        d.line((5, 31, 32, 18, 58, 31), fill=C("wood-4"), width=1)
        wood_grain(d, (10, 32, 55, 47), "coffee-1")
    return im


def make_tiles() -> None:
    sheet = canvas((64 * 6, 64))
    for i in range(6):
        paste(sheet, make_tile(i), (i * 64, 0))
    sheet.save(OUT / "tiles.png")


def prop_sprite(kind: int) -> Image.Image:
    im = canvas((96, 96))
    d = ImageDraw.Draw(im)
    if kind == 0:  # 카운터
        iso_box(d, 10, 30, 76, 26, 36, "wood", "wood-1")
        wood_grain(d, (18, 52, 48, 78))
    elif kind == 1:  # 그라인더
        iso_box(d, 24, 48, 46, 18, 20, "night", "ink-1")
        d.polygon([(35, 23), (59, 23), (55, 47), (39, 47)], fill=C("bone-2"))
        glass_sheen(d, (35, 23, 59, 47))
        d.rectangle((39, 51, 57, 55), fill=C("coffee-3"))
        metal_sheen(d, (24, 48, 70, 70))
    elif kind == 2:  # 에스프레소 머신
        iso_box(d, 12, 34, 72, 24, 34, "teal", "teal-1")
        d.rectangle((26, 43, 70, 58), fill=C("night-2"))
        d.rectangle((32, 58, 36, 69), fill=C("bone-2")); d.rectangle((59, 58, 63, 69), fill=C("bone-2"))
        metal_sheen(d, (17, 40, 79, 66))
        d.rectangle((27, 38, 30, 41), fill=C("red-4")); d.rectangle((34, 38, 37, 41), fill=C("amber-4"))
    elif kind == 3:  # 서빙대
        iso_box(d, 10, 46, 76, 24, 22, "wood", "wood-1")
        d.rectangle((22, 25, 74, 50), fill=C("night-2"), outline=C("bone-3"))
        d.rectangle((25, 28, 71, 48), fill=C("teal-1")); glass_sheen(d, (22, 25, 74, 50))
        draw_cup(d, 39, 39); draw_cup(d, 55, 37)
    elif kind == 4:  # 원두 선반
        d.rectangle((17, 18, 78, 76), fill=C("wood-2"), outline=C("wood-1"))
        for y in (37, 57): d.rectangle((19, y, 76, y + 3), fill=C("wood-4"))
        for x, y in ((24, 25), (42, 24), (59, 25), (27, 45), (48, 45)):
            d.rectangle((x, y, x + 11, y + 10), fill=C("bone-2"), outline=C("coffee-1"))
            d.ellipse((x + 4, y + 3, x + 7, y + 7), fill=C("coffee-3"))
    elif kind == 5:  # 테이블
        iso_box(d, 12, 32, 72, 34, 8, "wood", "wood-1")
        d.rectangle((44, 57, 51, 82), fill=C("night-2")); d.rectangle((30, 81, 65, 85), fill=C("night-1"))
        wood_grain(d, (19, 42, 73, 57))
    elif kind == 6:  # 서랍
        iso_box(d, 18, 31, 60, 22, 43, "wood", "wood-1")
        for y in (53, 65):
            d.line((31, y, 65, y), fill=C("wood-1")); d.rectangle((46, y + 3, 51, y + 4), fill=C("bone-3"))
    elif kind == 7:  # 컨테이너
        iso_box(d, 13, 37, 70, 30, 30, "night", "bone-3")
        d.rectangle((29, 54, 66, 74), fill=C("night-2"), outline=C("ink-1"))
        d.rectangle((44, 58, 52, 65), fill=C("bone-2"))
        metal_sheen(d, (18, 44, 79, 74))
    else:  # 랜덤 박스
        iso_box(d, 20, 35, 58, 28, 34, "amber", "amber-1")
        d.rectangle((42, 51, 54, 66), fill=C("amber-4"))
        d.rectangle((46, 55, 50, 58), fill=C("amber-1")); d.rectangle((46, 62, 50, 65), fill=C("amber-1"))
    return im


def make_props() -> None:
    sheet = canvas((96 * 9, 96))
    for i in range(9):
        paste(sheet, prop_sprite(i), (i * 96, 0))
    sheet.save(OUT / "props.png")


def character_frame(role: str, pose: str, phase: int = 0, variant: int = 0) -> Image.Image:
    """아트 바이블 §4·§8: 40×64 인물, 얼굴/입/주름/손/잔을 픽셀 단위로 표현."""
    im = canvas((40, 64))
    d = ImageDraw.Draw(im)
    bob = 1 if pose == "walk" and phase in (1, 3) else 0
    skin = "zombie-3" if role == "zombie" else ("bone-3" if variant % 2 == 0 else "wood-4")
    hair = ["coffee-1", "ink-2", "red-1", "olive-1"][variant % 4]
    shirt = ("red" if role == "player" else "olive" if role == "zombie" else ["teal", "red", "olive", "night"][variant % 4])
    leg = phase % 2 if pose == "walk" else 0
    # 발은 반드시 y=63 피벗선에 닿는다.
    d.rectangle((12 - leg * 2, 55 - bob, 19 - leg, 63), fill=C("ink-2"))
    d.rectangle((22 + leg, 55 - bob, 29 + leg * 2, 63), fill=C("ink-1"))
    d.rectangle((14, 43 - bob, 28, 56 - bob), fill=C(f"{shirt}-1"))
    d.rectangle((11, 31 - bob, 30, 47 - bob), fill=C(f"{shirt}-2"))
    d.rectangle((12, 32 - bob, 29, 35 - bob), fill=C(f"{shirt}-4"))  # 상단광
    d.line((15, 39 - bob, 20, 41 - bob, 25, 39 - bob), fill=C(f"{shirt}-1"), width=1)  # 옷 주름
    # 머리와 얼굴(남동 방향)
    d.rectangle((12, 12 - bob, 29, 30 - bob), fill=C(skin))
    d.rectangle((10, 10 - bob, 28, 18 - bob), fill=C(hair))
    d.rectangle((10, 16 - bob, 13, 25 - bob), fill=C(hair))
    d.rectangle((28, 14 - bob, 31, 23 - bob), fill=C(hair))
    d.rectangle((18, 20 - bob, 20, 22 - bob), fill=C("ink-1"))
    d.rectangle((25, 19 - bob, 27, 21 - bob), fill=C("ink-1"))
    if role == "zombie":
        d.rectangle((19, 20 - bob, 19, 20 - bob), fill=C("amber-4")); d.rectangle((24, 25 - bob, 29, 27 - bob), fill=C("red-2"))
    else:
        d.line((22, 26 - bob, 26, 26 - bob), fill=C("coffee-1"), width=1)  # 입
    # 팔/손. 운반·수색에는 잔/손 도구가 확실히 보인다.
    arm_shift = [0, -2, 1, -1][phase % 4] if pose == "walk" else 0
    d.rectangle((7, 34 - bob + arm_shift, 12, 45 - bob + arm_shift), fill=C(f"{shirt}-1"))
    d.rectangle((29, 34 - bob - arm_shift, 34, 44 - bob - arm_shift), fill=C(f"{shirt}-1"))
    d.rectangle((8, 44 - bob + arm_shift, 12, 48 - bob + arm_shift), fill=C(skin))
    d.rectangle((30, 43 - bob - arm_shift, 34, 47 - bob - arm_shift), fill=C(skin))
    if pose == "carry":
        draw_cup(d, 18, 40 - bob)
        d.line((12, 45 - bob, 18, 44 - bob), fill=C(skin), width=2)
        d.line((30, 44 - bob, 25, 44 - bob), fill=C(skin), width=2)
    elif pose == "search":
        d.line((30, 44 - bob, 38, 50 - bob), fill=C(skin), width=2)
        d.rectangle((35, 49 - bob, 38, 52 - bob), fill=C("amber-4"))
    im = outline(im, "ink-1" if role != "zombie" else "zombie-1", omit_dark_side=True)
    return im


def make_characters() -> dict[str, dict]:
    meta: dict[str, dict] = {}
    player_frames = [("idle_0", "idle", 0)] + [(f"walk_{i}", "walk", i) for i in range(4)] + [(f"carry_{i}", "carry", i) for i in range(4)] + [(f"search_{i}", "search", i) for i in range(2)]
    p = canvas((40 * 11, 64))
    for i, (_, pose, phase) in enumerate(player_frames): paste(p, character_frame("player", pose, phase), (i * 40, 0))
    p.save(OUT / "char_player.png")
    meta["char_player.png"] = {"cell": [40, 64], "frames": [{"name": n, "rect": [i * 40, 0, 40, 64]} for i, (n, _, _) in enumerate(player_frames)]}
    g = canvas((40 * 5, 64 * 4))
    gf = [("idle_0", "idle", 0)] + [(f"walk_{i}", "walk", i) for i in range(4)]
    grects = []
    for v in range(4):
        for i, (n, pose, phase) in enumerate(gf):
            paste(g, character_frame("guest", pose, phase, v), (i * 40, v * 64))
            grects.append({"name": f"guest{v}_{n}", "rect": [i * 40, v * 64, 40, 64]})
    g.save(OUT / "char_guest.png")
    meta["char_guest.png"] = {"cell": [40, 64], "frames": grects}
    z = canvas((40 * 5, 64)); zf = [("idle_0", "idle", 0)] + [(f"walk_{i}", "walk", i) for i in range(4)]
    for i, (_, pose, phase) in enumerate(zf): paste(z, character_frame("zombie", pose, phase), (i * 40, 0))
    z.save(OUT / "char_zombie.png")
    meta["char_zombie.png"] = {"cell": [40, 64], "frames": [{"name": n, "rect": [i * 40, 0, 40, 64]} for i, (n, _, _) in enumerate(zf)]}
    return meta


def taste_icon(kind: int) -> Image.Image:
    im = canvas((24, 24)); d = ImageDraw.Draw(im)
    cols = ["red-4", "amber-4", "teal-4", "olive-4", "bone-4"]
    c = cols[kind]
    if kind == 0:  # 단맛: 하트
        d.polygon([(4, 8), (7, 5), (11, 7), (15, 5), (20, 8), (19, 13), (12, 20), (5, 13)], fill=C(c))
    elif kind == 1:  # 쓴맛: 원두
        d.ellipse((5, 3, 18, 20), fill=C(c)); d.line((8, 17, 15, 6), fill=C("amber-1"), width=2)
    elif kind == 2:  # 산미: 번개
        d.polygon([(13, 2), (6, 13), (11, 13), (9, 22), (18, 10), (13, 10)], fill=C(c))
    elif kind == 3:  # 바디: 방패
        d.polygon([(5, 4), (19, 4), (18, 15), (12, 21), (6, 15)], fill=C(c))
    else:  # 향: 별
        d.polygon([(12, 2), (15, 9), (22, 10), (17, 15), (18, 22), (12, 18), (6, 22), (7, 15), (2, 10), (9, 9)], fill=C(c))
    return outline(im, "ink-1")


def status_icon(kind: int) -> Image.Image:
    im = canvas((20, 20)); d = ImageDraw.Draw(im)
    col = ["bone-3", "amber-4", "teal-4", "red-4"][kind]
    d.rectangle((3, 5, 16, 16), fill=C("night-2"), outline=C(col), width=2)
    if kind == 0: d.rectangle((7, 2, 12, 6), fill=C(col))
    elif kind == 1: d.polygon([(9, 7), (14, 10), (9, 14)], fill=C(col))
    elif kind == 2: d.line((6, 11, 9, 14, 15, 7), fill=C(col), width=2)
    else: d.line((6, 7, 14, 15), fill=C(col), width=2); d.line((14, 7, 6, 15), fill=C(col), width=2)
    return im


def make_icons() -> dict[str, dict]:
    im = canvas((120, 44)); frames = []
    for i in range(5):
        paste(im, taste_icon(i), (i * 24, 0)); frames.append({"name": f"taste_{i}", "rect": [i * 24, 0, 24, 24]})
    for i in range(4):
        paste(im, status_icon(i), (i * 20, 24)); frames.append({"name": f"station_{i}", "rect": [i * 20, 24, 20, 20]})
    im.save(OUT / "icons.png")
    return {"icons.png": {"cell": [0, 0], "frames": frames}}


def scene_floor(im: Image.Image, night: bool) -> None:
    d = ImageDraw.Draw(im)
    ox, oy, tw, th = 320, 84, 48, 24
    a, b = (("night-2", "night-3") if night else ("bone-2", "wood-2"))
    for row in range(8):
        for col in range(10):
            x = ox + (col - row) * tw // 2 - tw // 2
            y = oy + (col + row) * th // 2
            pts = [(x, y + th // 2), (x + tw // 2, y), (x + tw - 1, y + th // 2), (x + tw // 2, y + th - 1)]
            d.polygon(pts, fill=C(a if (row + col) % 2 == 0 else b))
            d.line(pts + [pts[0]], fill=C("ink-2" if night else "coffee-1"), width=1)


def scene_walls(im: Image.Image, night: bool) -> None:
    d = ImageDraw.Draw(im); wall = "night-2" if night else "coffee-2"; trim = "night-3" if night else "wood-3"
    d.polygon([(80, 82), (320, 16), (320, 84), (80, 150)], fill=C(wall))
    d.polygon([(320, 16), (560, 82), (560, 150), (320, 84)], fill=C("night-1" if night else "olive-2"))
    d.line((80, 82, 320, 16, 560, 82), fill=C(trim), width=3)
    # 창: 밖은 잿빛, 유리 대각 하이라이트
    d.polygon([(120, 83), (205, 60), (205, 106), (120, 129)], fill=C("night-1" if night else "night-3"), outline=C("wood-1"))
    d.line((129, 91, 161, 75), fill=C("bone-3"), width=2)
    d.line((136, 101, 174, 82), fill=C("bone-3"), width=2)


def scene_furniture(im: Image.Image, night: bool) -> None:
    # 낮/밤이 완전히 같은 배치 좌표를 공유한다.
    for i, x in enumerate((246, 316, 386)):
        p = prop_sprite(0); paste(im, p.resize((72, 72), Image.Resampling.NEAREST), (x, 110 + i * 14))
    for kind, pos, size in ((1, (250, 91), (58, 58)), (2, (318, 99), (68, 68)), (3, (392, 118), (70, 70)),
                            (4, (448, 61), (76, 76)), (5, (160, 205), (76, 76)), (5, (410, 232), (76, 76))):
        paste(im, prop_sprite(kind).resize(size, Image.Resampling.NEAREST), pos)
    # 수증기: 2px 계단 픽셀 덩어리
    d = ImageDraw.Draw(im)
    steam = "night-4" if night else "bone-4"
    for x, y in ((289, 96), (357, 102), (424, 124)):
        d.rectangle((x, y, x + 2, y + 7), fill=C(steam)); d.rectangle((x + 2, y - 4, x + 4, y + 1), fill=C(steam))


def bubble(im: Image.Image, x: int, y: int, icon: int) -> None:
    d = ImageDraw.Draw(im)
    d.rectangle((x, y, x + 29, y + 27), fill=C("bone-4"), outline=C("ink-1"))
    d.polygon([(x + 12, y + 27), (x + 17, y + 27), (x + 14, y + 32)], fill=C("bone-4"))
    paste(im, taste_icon(icon).resize((20, 20), Image.Resampling.NEAREST), (x + 5, y + 4))


def make_scene(night: bool) -> Image.Image:
    im = canvas((640, 360), "ink-1" if night else "night-1")
    scene_walls(im, night); scene_floor(im, night)
    d = ImageDraw.Draw(im)
    if not night:
        # 따뜻한 바닥색 계단: 불투명한 3개 평면, 그라디언트 없음.
        d.polygon([(256, 130), (384, 130), (468, 250), (172, 250)], fill=C("wood-2"))
        d.polygon([(276, 139), (364, 139), (422, 224), (218, 224)], fill=C("wood-3"))
        d.polygon([(296, 146), (344, 146), (380, 198), (260, 198)], fill=C("amber-2"))
    scene_furniture(im, night)
    if not night:
        paste(im, character_frame("player", "carry"), (337, 114))
        for i, (x, y) in enumerate(((278, 218), (315, 235), (352, 252))):
            paste(im, character_frame("guest", "idle", 0, i), (x, y)); bubble(im, x + 7, y - 29, i)
    else:
        # 손전등: 하드 컷 3단 알파 계단을 팔레트 불투명 면으로 치환.
        d.polygon([(112, 286), (322, 183), (350, 220)], fill=C("amber-1"))
        d.polygon([(112, 286), (292, 198), (318, 220)], fill=C("amber-2"))
        d.polygon([(112, 286), (250, 213), (278, 226)], fill=C("amber-3"))
        # 컨테이너 3상태: bone-3 2px / amber-4 2px / bone-1 1px.
        for i, (x, y, col, wid) in enumerate(((185, 176, "bone-3", 2), (365, 202, "amber-4", 2), (475, 255, "bone-1", 1))):
            box = prop_sprite(7).resize((58, 58), Image.Resampling.NEAREST)
            box = outline(box, col, width=wid); paste(im, box, (x, y))
        paste(im, character_frame("zombie", "walk", 1), (250, 211)); paste(im, character_frame("zombie", "walk", 3), (447, 175))
        # HOME 표식은 텍스트 없이 집 아이콘만.
        d.polygon([(555, 284), (579, 265), (603, 284), (598, 284), (598, 309), (560, 309), (560, 284)], fill=C("amber-4"))
        d.rectangle((574, 294, 584, 309), fill=C("amber-1"))
    return im


def validate_pngs(meta: dict[str, dict]) -> None:
    """아트 바이블 §4 및 납품 규격 자체 검증."""
    allowed = set(RGBA.values()) | {TRANSPARENT}
    pngs = sorted(OUT.glob("*.png"))
    assert pngs, "검증 실패: PNG가 하나도 생성되지 않았습니다."
    for path in pngs:
        im = Image.open(path).convert("RGBA")
        bad = set(im.getdata()) - allowed
        assert not bad, f"검증 실패: {path.name}에 팔레트 밖 색 {next(iter(bad))}가 있습니다."
    # diamond의 수학적 외곽과 실제 불투명 경계를 모두 검증한다.
    probe = canvas((64, 32)); diamond(ImageDraw.Draw(probe), 0, 0, 64, 32, "bone-2")
    bbox = probe.getbbox()
    assert bbox == (0, 0, 64, 32), f"검증 실패: 다이아몬드 실제 경계가 64×32가 아닙니다: {bbox}"
    for filename in ("char_player.png", "char_guest.png", "char_zombie.png"):
        im = Image.open(OUT / filename).convert("RGBA")
        assert meta[filename]["cell"] == [40, 64], f"검증 실패: {filename} 셀이 40×64가 아닙니다."
        for frame in meta[filename]["frames"]:
            x, y, w, h = frame["rect"]
            assert (w, h) == (40, 64), f"검증 실패: {filename}/{frame['name']} 프레임 크기 오류."
            crop = im.crop((x, y, x + w, y + h))
            assert any(crop.getpixel((px, 63))[3] for px in range(15, 26)), f"검증 실패: {filename}/{frame['name']}의 발이 하단 중앙 피벗에 닿지 않습니다."
    assert Image.open(OUT / "scene_day.png").size == (640, 360), "검증 실패: 낮 장면은 640×360이어야 합니다."
    assert Image.open(OUT / "scene_night.png").size == (640, 360), "검증 실패: 밤 장면은 640×360이어야 합니다."


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    make_palette(); make_tiles(); make_props()
    meta = make_characters(); meta.update(make_icons())
    meta["tiles.png"] = {"cell": [64, 64], "frames": [{"name": n, "rect": [i * 64, 0, 64, 64]} for i, n in enumerate(("cafe_a", "cafe_b", "zombie_a", "zombie_b", "street", "wall"))]}
    meta["props.png"] = {"cell": [96, 96], "frames": [{"name": n, "rect": [i * 96, 0, 96, 96]} for i, n in enumerate(("counter", "grinder", "espresso", "serving", "bean_shelf", "table", "drawer", "container", "random_box"))]}
    make_scene(False).save(OUT / "scene_day.png")
    make_scene(True).save(OUT / "scene_night.png")
    (OUT / "sheets.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
    validate_pngs(meta)


if __name__ == "__main__":
    main()
