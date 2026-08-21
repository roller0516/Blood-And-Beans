"""BLOOD & BEAN — PixelLab API로 게임 에셋 전량을 생성한다.

Codex imagegen 파이프라인(prep_assets.py)을 대체한다. PixelLab 쪽이 나은 점:
  - no_background=True 로 **진짜 알파**가 나온다 → 플러드 필 배경 키잉이 통째로 불필요
  - color_image 로 **46색 팔레트를 강제**한다 → 양자화 드리프트가 없다
  - isometric / view / direction 으로 **시점을 고정**한다 → 에셋 간 각도가 안 흔들린다

후처리(트림·축소·팔레트 양자화·게임 폴더 배포)는 prep_assets.py 것을 그대로 쓴다.
새로 짠 건 diamond_mask 하나뿐이다.

  트라이얼 잔액이 40회다. 생성 결과는 Art/pl_raw/ 에 캐시하고, 캐시가 있으면
  절대 다시 호출하지 않는다. 후처리만 다시 돌리는 건 공짜다.

실행:
  python Art/pixellab_gen.py --check          # 잔액·계획만 보고 생성 안 함
  python Art/pixellab_gen.py --only tiles     # 타일부터 (지시된 순서)
  python Art/pixellab_gen.py                  # 캐시에 없는 것 전부
  python Art/pixellab_gen.py --force player_idle   # 특정 에셋만 재생성(1회 소모)

키: 환경변수 PIXELLAB_API_KEY, 없으면 Art/.pixellab_key 파일.
"""
import argparse
import base64
import io as _io
import json
import os
import sys
import time
import urllib.error
import urllib.request

from PIL import Image

import prep_assets as P                      # 팔레트·트림·양자화·배포를 재사용한다

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "pl_raw")
PALETTE_PNG = os.path.join(HERE, "out", "palette.png")
API = "https://api.pixellab.ai/v1"

# ── 공통 스타일 ────────────────────────────────────────────────────────────
# 모든 프롬프트 뒤에 붙는다. 에셋 31종의 톤과 시점을 하나로 묶는 유일한 장치다.
STYLE = ("post-apocalyptic coffee shop survival game, HD pixel art, "
         "quarter view 2:1 isometric projection, single object centered, "
         "muted amber and soot palette, crisp dark outline")

# 바닥 타일 전용. 오브젝트·벽·그림자를 명시적으로 금지한다 — 하나라도 들어오면
# 타일이 반복될 때 그 물건이 격자마다 복제되어 바닥이 아니라 무늬가 된다.
TILE_STYLE = ("seamless flat ground texture filling the entire frame, straight top-down view, "
              "post-apocalyptic pixel art, muted amber and soot palette, "
              "no objects, no walls, no furniture, no border, no shadows, even lighting")

# ── 생성 계획 ──────────────────────────────────────────────────────────────
# 순서가 곧 지출 순서다. 지시대로 **타일이 맨 앞**이다.
# base: 이 에셋을 init_image 로 삼아 캐릭터 일관성을 유지한다 (걷기 프레임용).
#       추가 비용은 없다 — 같은 1회 생성이고, 없으면 프레임마다 딴 사람이 나온다.
# 타일 프롬프트는 STYLE(아이소메트릭)을 쓰지 않는다 — TILE_STYLE로 갈아탄다. 아래 generate() 참조.
TILES = [
    ("tile_cafe_floor", "worn checkered cafe floor, warm wood planks and cream ceramic squares, coffee stains"),
    ("tile_zone_floor", "cracked grey asphalt road surface, ash, small rubble, dead grass in the cracks"),
]
WALLS = [
    ("wall_cafe", "cafe interior wall segment, warm painted plaster, wooden wainscot, hanging menu board"),
    ("wall_ruined", "ruined concrete wall segment, exposed rebar, scorch marks, peeling paint"),
]
CHARS = [
    ("player_idle", "barista survivor standing, apron over armored jacket, short hair, backpack", None),
    ("player_carry", "barista survivor standing, holding a coffee cup out in both hands, apron, backpack", "player_idle"),
    ("player_walk_0", "barista survivor walking, left leg forward, apron, backpack", "player_idle"),
    ("player_walk_1", "barista survivor walking, legs together mid stride, apron, backpack", "player_idle"),
    ("player_walk_2", "barista survivor walking, right leg forward, apron, backpack", "player_idle"),
    ("player_walk_3", "barista survivor walking, legs together mid stride, apron, backpack", "player_idle"),
    ("zombie_walk_0", "shambling zombie, torn clothes, grey-green rotten skin, arms hanging, left leg forward", None),
    ("zombie_walk_1", "shambling zombie, torn clothes, grey-green rotten skin, arms hanging, right leg forward", "zombie_walk_0"),
    ("raider_walk_0", "raider looter, gas mask, patched leather coat, pipe weapon in hand, left leg forward", None),
    ("raider_walk_1", "raider looter, gas mask, patched leather coat, pipe weapon in hand, right leg forward", "raider_walk_0"),
    ("guest_a", "tired wasteland customer standing, hooded coat, scarf", None),
    ("guest_b", "wasteland customer standing, worn suit jacket, satchel", None),
    ("guest_c", "wasteland customer standing, mechanic overalls, goggles on forehead", None),
    ("guest_d", "elderly wasteland customer standing, long shawl, walking cane", None),
]
PROPS = [
    ("counter",           "cafe service counter, wooden top, front panel, cash register"),
    ("bean_shelf",        "tall wooden shelf stacked with burlap coffee bean sacks and jars"),
    ("grinder",           "large cast iron coffee grinder machine with hopper and crank"),
    ("espresso_machine",  "chrome espresso machine, group heads, pressure gauge, portafilter"),
    ("steam_wand",        "milk steaming station, steel pitcher, steam wand, small boiler"),
    ("cold_brew_tank",    "tall glass cold brew tank on a steel frame, dark coffee inside, brass tap"),
    ("serving_station",   "serving counter with tray, finished coffee cups, napkin holder"),
    ("cafe_table",        "small round cafe table with two mismatched chairs"),
    ("drawer_chest",      "battered wooden chest of drawers, one drawer half open"),
    ("metal_container",   "rusted steel supply crate with latches, lid closed"),
    ("random_box",        "mysterious sealed strongbox, heavy padlock, faded pre-war markings"),
    ("barrel",            "rusted oil barrel, dented, faded warning stencil"),
    ("cafe_home",         "small one-story coffee shop storefront building, boarded windows, warm lit doorway, hanging cup sign"),
    ("cafe_rival",        "small one-story rival coffee shop storefront building, dark windows, red cup sign, sandbags at the door"),
    ("corpse",            "dead body lying face down on the ground, tattered clothes, looted"),
]

GROUPS = {"tiles": TILES, "walls": WALLS, "chars": CHARS, "props": PROPS}
ORDER = ["tiles", "walls", "props", "chars"]


def plan():
    """(name, prompt, kind, base) 목록을 생성 순서대로."""
    out = []
    for g in ORDER:
        for row in GROUPS[g]:
            name, prompt = row[0], row[1]
            base = row[2] if len(row) > 2 else None
            out.append((name, prompt, g, base))
    return out


# ── API ────────────────────────────────────────────────────────────────────
def api_key():
    k = os.environ.get("PIXELLAB_API_KEY", "").strip()
    if k:
        return k
    f = os.path.join(HERE, ".pixellab_key")
    if os.path.exists(f):
        return _io.open(f, encoding="utf-8").read().strip()
    sys.exit("PIXELLAB_API_KEY 환경변수 또는 Art/.pixellab_key 가 필요합니다.")


def _post(path, body, key, timeout=300):
    req = urllib.request.Request(
        API + path, data=json.dumps(body).encode("utf-8"), method="POST",
        headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        raise RuntimeError("HTTP %d — %s" % (e.code, e.read().decode("utf-8", "replace")[:400]))


def balance(key):
    req = urllib.request.Request(API + "/balance", headers={"Authorization": "Bearer " + key})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8"))


def b64_png(path):
    return base64.b64encode(_io.open(path, "rb").read()).decode("ascii")


def generate(name, prompt, kind, base, key):
    """PixelLab 1회 호출. 반환은 RGBA 이미지."""
    # no_background 는 200x200 이상에서만 제대로 동작한다(API 문서). 넉넉히 256.
    # 어차피 후처리에서 목표 크기로 축소하므로 크게 뽑는 게 손해가 아니다.
    body = {
        "description": prompt + ", " + STYLE,
        "image_size": {"width": 256, "height": 256},
        "no_background": True,
        # isometric=True 는 쓰지 않는다. 실측 결과 모델이 **오브젝트를 흙 받침대
        # 블록 위에 올려서** 그린다(Art/pl_raw/espresso_machine.png 참조). 우리는
        # 자체 바닥 타일 위에 얹으므로 받침대가 있으면 소품마다 흙덩이가 깔린다.
        # 쿼터뷰 각도는 view="low top-down" + STYLE 문구로 충분히 나온다.
        "isometric": False,
        "outline": "single color black outline",
        "shading": "medium shading",
        "detail": "highly detailed",
        "text_guidance_scale": 9,
        "view": "low top-down",
    }
    if kind == "tiles":
        # 타일에 isometric=True 를 주면 모델이 '받침대 위에 놓인 건물'을 그린다.
        # (실측: 1차 생성분이 전부 그랬다.) 2:1 투영은 diamond_mask 가 만드므로
        # 모델에게는 **평평한 이음새 없는 바닥 텍스처**만 시킨다.
        body["description"] = prompt + ", " + TILE_STYLE
        body["isometric"] = False
        body["view"] = "high top-down"
        body["no_background"] = False          # 바닥은 프레임을 꽉 채워야 한다
        body["outline"] = "lineless"           # 바닥에 외곽선이 있으면 격자가 두 번 보인다
        body["shading"] = "flat shading"
    if kind in ("chars",):
        body["direction"] = "south-east"           # 쿼터뷰 기본 방향 (§16: 남동/남서 2벌)
    if os.path.exists(PALETTE_PNG):
        body["color_image"] = {"type": "base64", "base64": b64_png(PALETTE_PNG)}
    if base:
        bp = os.path.join(RAW, base + ".png")
        if os.path.exists(bp):
            body["init_image"] = {"type": "base64", "base64": b64_png(bp)}
            body["init_image_strength"] = 220      # 실루엣은 유지, 포즈는 바뀔 만큼
    res = _post("/generate-image-pixflux", body, key)
    img = res.get("image") or {}
    data = img.get("base64") or img.get("base64_image")
    if not data:
        raise RuntimeError("응답에 이미지가 없습니다: " + json.dumps(res)[:300])
    return Image.open(_io.BytesIO(base64.b64decode(data))).convert("RGBA")


# ── 후처리 ─────────────────────────────────────────────────────────────────
def diamond_mask(im, w=128, h=64):
    """바닥 타일을 정확한 2:1 마름모로 자른다.

    PixelLab에 '이음새 없는 마름모를 그려라'라고 시켜 봐야 몇 픽셀은 어긋난다.
    모델은 텍스처만 대고, 모양은 여기서 만든다. |dx/(w/2)| + |dy/(h/2)| <= 1 은
    이웃 타일의 마름모와 픽셀 단위로 정확히 맞물린다 — 이음새가 원천적으로 없다.
    """
    # 모델은 프레임 가장자리를 어둡게(비네트) 그리고 주제를 중앙에 둔다. 마름모의
    # 네 꼭짓점이 정확히 그 어두운 모서리에 닿아서, 깔아 놓으면 어두운 격자가 보인다.
    # 중앙부만 잘라 쓰면 생성 재시도 없이(=0회 소모) 사라진다.
    im = im.convert("RGBA")
    s = int(min(im.size) * 0.58)
    l, t = (im.width - s) // 2, (im.height - s) // 2
    im = im.crop((l, t, l + s, t + s)).resize((w, h), Image.LANCZOS)
    px = im.load()
    cx, cy = (w - 1) / 2.0, (h - 1) / 2.0
    for y in range(h):
        for x in range(w):
            if abs(x - cx) / (w / 2.0) + abs(y - cy) / (h / 2.0) > 1.0:
                px[x, y] = (0, 0, 0, 0)
            else:
                r, g, b, _ = px[x, y]
                px[x, y] = (r, g, b, 255)      # 마름모 안은 완전 불투명 — 틈 방지
    return im


def keep_main_blob(im, keep=0.06):
    """가장 큰 연결 덩어리만 남기고 떠 있는 섬을 지운다.

    PixelLab 배경이 단색이 아니라 얼룩(2톤 텍스처)으로 오는 경우가 있다
    (실측: pl_raw/guest_b.png). 플러드 필은 한 톤만 먹고 다른 톤을 섬으로 남기는데,
    그 섬은 **완전 불투명**이라 drop_specks 로는 안 지워진다. 크기로 자른다.
    keep 은 본체 대비 비율 — 인물의 분리된 소품(가방 손잡이 등)은 살리고
    먼지만 지우는 선.
    """
    from collections import deque
    w, h = im.size
    px = im.load()
    lab = [0] * (w * h)
    blobs = []                                   # (크기, 라벨)
    for sy in range(h):
        for sx in range(w):
            i = sy * w + sx
            if lab[i] or px[sx, sy][3] == 0:
                continue
            n = len(blobs) + 1
            dq, size = deque([(sx, sy)]), 0
            lab[i] = n
            while dq:
                x, y = dq.popleft()
                size += 1
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h:
                        j = ny * w + nx
                        if not lab[j] and px[nx, ny][3]:
                            lab[j] = n
                            dq.append((nx, ny))
            blobs.append((size, n))
    if not blobs:
        return im
    big = max(blobs)[0]
    live = set(n for sz, n in blobs if sz >= big * keep)
    for y in range(h):
        for x in range(w):
            if lab[y * w + x] not in live:
                px[x, y] = (0, 0, 0, 0)
    return im


# direction="south-east" 를 줘도 모델이 남서(화면 왼쪽)로 그려 놓는 에셋이 있다.
# 게임의 flip 은 "기본 스프라이트는 오른쪽을 본다"를 전제로 하므로, 한 벌만 반대면
# 그 캐릭터만 회전이 거꾸로 돈다(실측: 밤 플레이어가 raider 에셋을 쓴다).
# 재생성해도 다시 남서로 나올 수 있어서, 여기서 확정적으로 뒤집는다.
MIRROR = {"raider_walk_0", "raider_walk_1"}


def postprocess(name, im, kind):
    if kind == "tiles":
        return P.quantize(diamond_mask(im))
    # no_background=True 를 줘도 알파가 통째로 255 로 오는 경우가 있다(실측).
    # 그때만 기존 플러드 필 키잉으로 자른다 — PixelLab 배경은 단색이라 Codex 때보다 쉽다.
    if im.getchannel("A").getextrema()[0] == 255:
        im = P.key_background(im, tol=40)
    im = P.drop_specks(im)                     # 가장자리 반투명은 도트에서 금지
    im = keep_main_blob(im)                    # 얼룩 배경이 남긴 불투명 섬 제거
    im = P.trim(im)
    if im.width == 0 or im.height == 0:
        raise ValueError("빈 이미지 — 프롬프트가 배경만 그린 듯")
    if name in P.TARGET_WH:
        tw, th = P.TARGET_WH[name]
    else:
        th = P.TARGET_H.get(name, P.DEFAULT_H)
        tw = max(1, round(im.width * th / im.height))
    im = im.resize((tw, th), Image.LANCZOS)
    im = P.drop_specks(im, 110)
    if name in MIRROR:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    return P.quantize(im)


# ── 메인 ───────────────────────────────────────────────────────────────────
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="잔액과 계획만 출력")
    ap.add_argument("--only", choices=ORDER, help="이 그룹만")
    ap.add_argument("--force", nargs="*", default=[], help="캐시를 무시하고 재생성할 에셋 이름")
    ap.add_argument("--max", type=int, default=999, help="이번 실행에서 소모할 생성 횟수 상한")
    a = ap.parse_args()

    key = api_key()
    bal = balance(key)
    left = bal.get("usd", 0.0)
    print("잔액: %s" % json.dumps(bal, ensure_ascii=False))

    os.makedirs(RAW, exist_ok=True)
    os.makedirs(P.ASSETS, exist_ok=True)

    rows = [r for r in plan() if not a.only or r[2] == a.only]
    todo = [r for r in rows if r[0] in a.force or not os.path.exists(os.path.join(RAW, r[0] + ".png"))]
    print("대상 %d종 / 이번에 생성할 것 %d종 (나머지는 pl_raw 캐시 재사용)" % (len(rows), len(todo)))
    if a.check:
        for n, _, k, b in todo:
            print("  [%s] %s%s" % (k, n, "  <- init:" + b if b else ""))
        return

    spent, failed = 0, []
    for name, prompt, kind, base in todo:
        if spent >= a.max:
            print("  --max %d 도달, 중단" % a.max)
            break
        raw = os.path.join(RAW, name + ".png")
        try:
            print("  생성 %-18s ..." % name, end="", flush=True)
            im = generate(name, prompt, kind, base, key)
            im.save(raw)
            spent += 1
            print(" ok (누적 %d회)" % spent)
        except Exception as e:
            print(" FAIL %s" % e)
            failed.append((name, str(e)))
            time.sleep(2)

    # 후처리는 캐시 전체에 대해 매번 돌린다 — 공짜고, 규격이 바뀌면 다시 맞춰야 한다.
    done = []
    for name, _, kind, _ in rows:
        raw = os.path.join(RAW, name + ".png")
        if not os.path.exists(raw):
            continue
        try:
            out = postprocess(name, Image.open(raw).convert("RGBA"), kind)
            out.save(os.path.join(P.ASSETS, name + ".png"))
            done.append((name, out))
        except Exception as e:
            failed.append((name, "후처리: " + e.__class__.__name__ + " " + str(e)))

    # 게임에는 pl_raw 에 없는 기존 에셋도 있을 수 있다. Art/assets 전체를 배포한다.
    allnames = sorted(n[:-4] for n in os.listdir(P.ASSETS)
                      if n.endswith(".png") and not n.startswith("_"))
    P.publish([(n, Image.open(os.path.join(P.ASSETS, n + ".png"))) for n in allnames])

    print("\n생성 %d회 소모 / 후처리 %d종 / 실패 %d종" % (spent, len(done), len(failed)))
    for n, e in failed:
        print("  FAIL %s — %s" % (n, e))
    if spent:
        print("잔여: %s" % json.dumps(balance(key), ensure_ascii=False))


if __name__ == "__main__":
    main()
