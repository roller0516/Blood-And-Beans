"""BLOOD & BEAN — 팔레트 v2.1 생성기

아트컨셉 v2.0 §3의 팔레트를, Codex imagegen이 만든 컨셉 아트 2장
(Art/concept/*.png)에서 실측한 색으로 교정한 결과다.

교정의 핵심 하나: **밤 환경은 파랑이고 좀비는 초록이다.**
v2.0은 둘 다 초록(night 램프) 하나로 묶었는데, 컨셉 아트를 실측하니
밤 지면의 지배색이 H≈213 파랑(#0D1826 · #202D40)이었고 좀비만 초록이었다.
같은 램프로 묶으면 "좀비가 있는 곳"과 "그냥 어두운 곳"이 구분되지 않는다.
그래서 dusk(밤 환경)와 rot/toxic(좀비·독성)을 분리했다.

실행: python Art/make_palette.py
산출: Art/out/palette.png, palette.gpl, palette.hex
"""
import json
import os
from PIL import Image

# ── 팔레트 46색 / 11램프 ────────────────────────────────────────────
# 램프는 0=최암부 → n=최명부. 아트컨셉 §3 규칙 4(광원은 위)와
# 규칙 5(램프를 건너뛰지 않는다)를 지키려면 단계가 고르게 있어야 한다.
RAMPS = {
    # 중립 암부 — 낮/밤 공통 배경. 무채색에 가깝게(§3 규칙 1)
    "soot":  ["#06060B", "#10101A", "#1C1C26", "#2B2B33", "#3C3F40"],
    # 낮의 온기 — 컨셉 낮 화면 지배색. amber-4가 주조색, 5는 조명 하이라이트
    "amber": ["#331109", "#562713", "#78381A", "#A95A2A", "#E09A45", "#FDCD6F"],
    # 목재 — 카운터, 선반, 테이블, 상자
    "wood":  ["#2A2214", "#453E23", "#6B5733", "#8D7A58"],
    # 뼈·종이 — 텍스트, 플레이어 머리. bone-3이 화면에서 가장 밝은 색
    "bone":  ["#4A443B", "#6E6656", "#A2957E", "#E8DCC8"],
    # 밤 환경 — 지면, 건물, 벽. 컨셉 밤 화면 실측(H≈213)
    "dusk":  ["#050710", "#08101A", "#0D1826", "#121D29", "#202D40", "#3A4A60"],
    # 부패 — 좀비 피부. 밤 환경(파랑)과 분리돼야 눈에 띈다
    "rot":   ["#24301C", "#4A5A2A", "#7A9A4A", "#A8C46A"],
    # 독성 신호 — 좀비 눈, 소음 반경, 안전 신호. 아껴 쓴다(§3 규칙 2)
    "toxic": ["#2A6B58", "#5FD3A6"],
    # 피·경고 — 남의 카페, 습격, 피격. 아껴 쓴다(§3 규칙 2)
    "blood": ["#2A0C0A", "#5E1A12", "#9B2E28", "#D9463C"],
    # 살
    "skin":  ["#4E3828", "#7A5C42", "#C9B79A", "#E5D4B8"],
    # 금속 — 에스프레소 머신, 그라인더, 자물쇠
    "steel": ["#1C2024", "#343C44", "#5E6A74", "#9AA8B2"],
    # 유리 — 창, 진열장, 원두 자루 병
    "glass": ["#26343A", "#4A6A72", "#8FBFC4"],
}

# 플레이어가 반응해야 하는 것에만 붙는 색(§3 규칙 2). 장식 금지.
SIGNAL = {"amber-5": "낮 · 조명/적중/HOME", "toxic-1": "밤 · 좀비/소음", "blood-3": "위험 · 습격/남의 카페"}

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")


def hex2rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def flat():
    """[(name, hex), ...] 순서 고정."""
    return [("%s-%d" % (name, i), c) for name, cols in RAMPS.items() for i, c in enumerate(cols)]


def draw_strip(cell=48, pad=2):
    """램프당 한 줄. 신호색은 위에 1px amber 마커를 얹어 구분한다."""
    rows = len(RAMPS)
    cols = max(len(c) for c in RAMPS.values())
    w = cols * cell + pad * 2
    h = rows * cell + pad * 2
    im = Image.new("RGB", (w, h), hex2rgb("#06060B"))
    px = im.load()
    for r, (name, colors) in enumerate(RAMPS.items()):
        for c, hexcol in enumerate(colors):
            rgb = hex2rgb(hexcol)
            x0, y0 = pad + c * cell, pad + r * cell
            for y in range(y0, y0 + cell - 1):
                for x in range(x0, x0 + cell - 1):
                    px[x, y] = rgb
            if "%s-%d" % (name, c) in SIGNAL:      # 신호색 마커
                for x in range(x0, x0 + cell - 1):
                    px[x, y0] = hex2rgb("#FDCD6F")
                    px[x, y0 + 1] = hex2rgb("#FDCD6F")
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    entries = flat()

    draw_strip().save(os.path.join(OUT, "palette.png"))

    with open(os.path.join(OUT, "palette.gpl"), "w", encoding="utf-8") as f:
        f.write("GIMP Palette\nName: BLOOD AND BEAN v2.1\nColumns: 6\n#\n")
        for name, hexcol in entries:
            r, g, b = hex2rgb(hexcol)
            f.write("%3d %3d %3d\t%s\n" % (r, g, b, name))

    with open(os.path.join(OUT, "palette.hex"), "w", encoding="utf-8") as f:
        f.write("\n".join(h.lstrip("#") for _, h in entries) + "\n")

    with open(os.path.join(OUT, "palette.json"), "w", encoding="utf-8") as f:
        json.dump({"ramps": RAMPS, "signal": SIGNAL}, f, ensure_ascii=False, indent=2)

    # ── 자체 검증 ────────────────────────────────────────────────
    assert len(entries) == 46, "색 수가 46이 아님: %d" % len(entries)
    assert len({h.upper() for _, h in entries}) == 46, "중복 색이 있음"
    for name, cols in RAMPS.items():
        lum = [sum(hex2rgb(c)) for c in cols]
        assert lum == sorted(lum), "%s 램프가 어두움→밝음 순이 아님: %s" % (name, lum)
    for key in SIGNAL:
        assert key in dict(entries), "신호색 %s 가 팔레트에 없음" % key
    print("OK  46색 / %d램프 -> %s" % (len(RAMPS), OUT))
    for name, cols in RAMPS.items():
        print("  %-6s %s" % (name, " ".join(cols)))


if __name__ == "__main__":
    main()
