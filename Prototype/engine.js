/* BLOOD & BEAN — 렌더 코어.
 *
 * game.js 는 게임 규칙(경제·좀비 FSM·루팅)만 갖고, "어디에 어떤 순서로 그리는가"는
 * 전부 여기로 위임한다. 클래스는 4개고 각자 하나만 책임진다.
 *
 *   AssetManager   에셋 로드와 조회.            좌표를 모른다.
 *   IsoGrid        타일 <-> 화면 좌표와 카메라.  그림을 모른다.
 *   SpriteRenderer 피벗 스냅과 실제 drawImage.  순서를 모른다.
 *   DepthRenderer  깊이 정렬.                   무엇을 그리는지 모른다.
 *
 * 이 분리의 실익은 취향이 아니다. 예전엔 호출부 30곳이 각자 정렬 키(d, z)를 손으로
 * 계산했다. 한 곳만 틀려도 그 오브젝트만 순서가 꼬이고, 화면을 봐야만 안다.
 * 이제 키 계산이 DepthRenderer 안 한 곳뿐이라 틀릴 수 있는 자리가 하나로 줄었다.
 *
 * 브라우저에서는 classic script 로 로드되고(game.js 보다 먼저), Node 하네스에서는
 * module.exports 로 가져간다. ES module 로 바꾸지 않은 이유는 그것뿐이다 —
 * qa/harness.js 가 vm.runInContext 로 소스를 이어 붙여 돌린다.
 */
'use strict';

/* ── 에셋 ──────────────────────────────────────────────────────────────────
   책임: 이름 -> 이미지. 그 이상 아무것도 하지 않는다. */
class AssetManager {
  constructor(makeImage, base) {
    this.base = base || 'assets/';
    this._make = makeImage;            // 주입 — Node 하네스에는 Image 가 없다
    this.images = Object.create(null);
    this.names = [];
  }
  load(names) {
    this.names = names.slice();
    for (const n of names) {
      const img = this._make();
      img.src = this.base + n + '.png';
      this.images[n] = img;
    }
    return this;
  }
  /** 로드가 끝나 실제로 그릴 수 있는 이미지, 아니면 null.
   *  null 을 던지지 않고 돌려주는 게 중요하다 — 호출부가 도형 폴백으로 넘어간다. */
  get(name) {
    const i = this.images[name];
    return (i && i.complete && i.naturalWidth) ? i : null;
  }
  has(name) { return !!this.get(name); }
  /** 로드 진행률 0~1. 로딩 화면이 필요해지면 쓴다. */
  progress() {
    if (!this.names.length) return 1;
    let n = 0;
    for (const k of this.names) if (this.get(k)) n++;
    return n / this.names.length;
  }
}

/* ── 좌표 ──────────────────────────────────────────────────────────────────
   책임: 타일 좌표 <-> 화면 픽셀, 그리고 카메라. 2:1 아이소메트릭.
   tw/th 는 논리 픽셀 기준 타일 크기(64x32). 에셋은 2배(128x64)로 만들고
   SpriteRenderer 가 절반으로 줄여 그린다 — 그래야 정수 배율이 유지된다. */
class IsoGrid {
  constructor(tw, th, ox, oy) {
    this.tw = tw; this.th = th;
    this.hw = tw / 2; this.hh = th / 2;
    this.ox = ox; this.oy = oy;
    this.camX = 0; this.camY = 0;
  }
  /** 타일 (tx,ty)의 **바닥 중심**을 화면 픽셀로. z는 위로 띄우는 높이. */
  toScreen(tx, ty, z) {
    return [
      (this.ox + (tx - ty) * this.hw - this.camX) | 0,
      (this.oy + (tx + ty) * this.hh - (z || 0) - this.camY) | 0
    ];
  }
  /** 화면 픽셀 -> 타일 좌표(실수). 마우스 피킹용. toScreen 의 정확한 역함수다. */
  toTile(sx, sy) {
    const x = sx + this.camX - this.ox, y = sy + this.camY - this.oy;
    return [(x / this.hw + y / this.hh) / 2, (y / this.hh - x / this.hw) / 2];
  }
  /** 카메라를 이 타일에 맞춘다. lift 만큼 위로 올려 시야를 앞쪽에 준다. */
  focus(tx, ty, lift) {
    this.camX = ((tx - ty) * this.hw) | 0;
    this.camY = (((tx + ty) * this.hh) | 0) - (lift === undefined ? 140 : lift);
  }
  /** 깊이 키. 2:1 아이소메트릭에서 화면 앞뒤 순서는 정확히 (tx+ty) 다.
   *  이 한 줄이 정렬의 유일한 정의다. 다른 데서 다시 계산하지 않는다. */
  depth(tx, ty) { return tx + ty; }
}

/* ── 스프라이트 ────────────────────────────────────────────────────────────
   책임: 피벗 스냅 + drawImage. **오브젝트가 타일 밖으로 삐져나오지 않게 하는
   유일한 지점**이라, 앵커 규칙이 여기 말고 다른 데 있으면 안 된다.

   앵커 3종:
     foot  인물. 스프라이트 **하단 중앙 = 타일 중심**. 발이 타일 한가운데 선다.
     base  소품. 밑면이 타일 마름모에 얹히도록 아래 꼭짓점(+hh)까지 내린다.
           foot 을 쓰면 소품이 타일 뒤쪽으로 반 칸 떠 보인다.
     mid   바닥 타일 자체. 이미지 중심 = 타일 중심.
*/
class SpriteRenderer {
  constructor(ctx, assets, grid) { this.ctx = ctx; this.assets = assets; this.grid = grid; }

  /** 앵커를 적용한 좌상단 y (논리 px). 검증 스크립트가 이걸 직접 부른다. */
  anchorY(cy, h, anchor) {
    if (anchor === 'mid') return cy - h / 2;
    if (anchor === 'base') return cy - h + this.grid.hh;
    return cy - h;                                   // 'foot'
  }
  /** 화면 좌표 (cx,cy)에 그린다. 그렸으면 true, 에셋이 아직 없으면 false. */
  drawAt(name, cx, cy, flip, anchor) {
    const i = this.assets.get(name);
    if (!i) return false;
    const w = i.naturalWidth / 2, h = i.naturalHeight / 2;   // 에셋은 2배 해상도
    const y = this.anchorY(cy, h, anchor);
    const c = this.ctx;
    if (flip) {
      c.save(); c.translate(cx, 0); c.scale(-1, 1);
      c.drawImage(i, -w / 2, y, w, h);
      c.restore();
    } else {
      c.drawImage(i, cx - w / 2, y, w, h);
    }
    return true;
  }
  /** 타일 좌표에 그린다. 스냅이 필요한 새 코드는 이쪽을 쓴다. */
  draw(name, tx, ty, opt) {
    opt = opt || {};
    const p = this.grid.toScreen(tx, ty, opt.z || 0);
    return this.drawAt(name, p[0], p[1], opt.flip, opt.anchor || 'foot');
  }
}

/* ── 깊이 정렬 ─────────────────────────────────────────────────────────────
   책임: 그리기 명령을 모아 뒤에서 앞으로 흘린다.

   키는 (depth, layer) 두 단계다.
     depth  = tx + ty. 아이소메트릭 앞뒤.
     layer  = 같은 타일 안에서의 위아래. 바닥 0 / 소품 100 / 인물 1000 / 이펙트 2000.
   같은 (depth, layer)면 **넣은 순서**를 지킨다 — Array.sort 가 안정 정렬이라
   그냥 되는 게 아니라, 그렇게 보장되는 것에 기대고 있다는 뜻이다. */
const LAYER = { FLOOR: 0, PROP: 100, ACTOR: 1000, FX: 2000 };

class DepthRenderer {
  constructor(grid) { this.grid = grid; this.q = []; }
  clear() { this.q.length = 0; }
  /** 타일 좌표로 넣는다. 권장 경로 — 호출부가 깊이를 계산하지 않는다. */
  push(tx, ty, layer, fn) {
    this.q.push({ d: this.grid.depth(tx, ty), z: layer || 0, f: fn });
    return this;
  }
  /** 깊이를 직접 아는 경우(건물 벽처럼 여러 타일에 걸친 것)만 쓴다. */
  pushDepth(d, layer, fn) { this.q.push({ d: d, z: layer || 0, f: fn }); return this; }
  flush() {
    this.q.sort((a, b) => (a.d - b.d) || (a.z - b.z));
    for (const o of this.q) o.f();
    this.q.length = 0;
  }
}

/* class / const 선언은 window 에 자동으로 붙지 않는다 (var 와 다른 점).
   game.js 와 qa 하네스가 전역으로 집어 가므로 직접 올린다. */
Object.assign(globalThis, { AssetManager, IsoGrid, SpriteRenderer, DepthRenderer, LAYER });
if (typeof module !== 'undefined' && module.exports)
  module.exports = { AssetManager, IsoGrid, SpriteRenderer, DepthRenderer, LAYER };
