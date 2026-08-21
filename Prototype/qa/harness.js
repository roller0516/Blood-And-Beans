// headless 하네스 — 브라우저 없이 game.js 를 실제로 굴린다.
// 코드를 읽고 판단하지 않는다. 돌려서 나온 값만 본다.
'use strict';
const fs = require('fs'), path = require('path'), vm = require('vm');

/* ── 소프트웨어 캔버스 ──────────────────────────────────────
   fillRect 만 실제로 래스터한다. 셀프테스트가 icon()/diamond() 를
   getImageData 로 검사하므로 픽셀이 진짜로 찍혀야 한다. */
function parseColor(s){
  if(typeof s !== 'string') return [0,0,0,255];
  if(s[0] === '#'){
    let h = s.slice(1);
    if(h.length === 3) h = h[0]+h[0]+h[1]+h[1]+h[2]+h[2];
    return [parseInt(h.slice(0,2),16), parseInt(h.slice(2,4),16), parseInt(h.slice(4,6),16), 255];
  }
  const m = s.match(/rgba?\(([^)]+)\)/);
  if(m){
    const p = m[1].split(',').map(Number);
    return [p[0]|0, p[1]|0, p[2]|0, Math.round((p[3] === undefined ? 1 : p[3]) * 255)];
  }
  return [0,0,0,255];
}
function Ctx(w,h){
  this.W=w; this.H=h;
  this.buf = new Uint8ClampedArray(w*h*4);
  this.fillStyle = '#000'; this.globalAlpha = 1; this.imageSmoothingEnabled = false;
  this.strokeStyle = '#000'; this.lineWidth = 1; this.font = '';
  this._t = {a:1,d:1,e:0,f:0}; this._stack = [];
}
Ctx.prototype.setTransform = function(a,b,c,d,e,f){ this._t = {a:a,d:d,e:e,f:f}; };
Ctx.prototype.save = function(){ this._stack.push(Object.assign({},this._t)); };
Ctx.prototype.restore = function(){ if(this._stack.length) this._t = this._stack.pop(); };
Ctx.prototype.translate = function(x,y){ this._t.e += x*this._t.a; this._t.f += y*this._t.d; };
Ctx.prototype.scale = function(x,y){ this._t.a *= x; this._t.d *= y; };
Ctx.prototype.fillRect = function(x,y,w,h){
  const t = this._t;
  let x0 = Math.round(x*t.a + t.e), y0 = Math.round(y*t.d + t.f);
  let x1 = Math.round((x+w)*t.a + t.e), y1 = Math.round((y+h)*t.d + t.f);
  if(x1 < x0){ const s=x0; x0=x1; x1=s; }
  if(y1 < y0){ const s=y0; y0=y1; y1=s; }
  const col = parseColor(this.fillStyle), a = Math.max(0,Math.min(1,this.globalAlpha));
  if(a <= 0) return;
  for(let py=Math.max(0,y0); py<Math.min(this.H,y1); py++)
    for(let px=Math.max(0,x0); px<Math.min(this.W,x1); px++){
      const i = (py*this.W+px)*4;
      this.buf[i]=col[0]; this.buf[i+1]=col[1]; this.buf[i+2]=col[2];
      this.buf[i+3]=Math.round(col[3]*a);
    }
};
Ctx.prototype.clearRect = function(x,y,w,h){
  const s = this.fillStyle, g = this.globalAlpha;
  this.fillStyle = 'rgba(0,0,0,0)'; this.globalAlpha = 1;
  const t=this._t;
  let x0=Math.round(x*t.a+t.e), y0=Math.round(y*t.d+t.f);
  let x1=Math.round((x+w)*t.a+t.e), y1=Math.round((y+h)*t.d+t.f);
  for(let py=Math.max(0,y0); py<Math.min(this.H,y1); py++)
    for(let px=Math.max(0,x0); px<Math.min(this.W,x1); px++)
      this.buf[(py*this.W+px)*4+3]=0;
  this.fillStyle=s; this.globalAlpha=g;
};
Ctx.prototype.getImageData = function(x,y,w,h){
  const out = new Uint8ClampedArray(w*h*4);
  for(let j=0;j<h;j++) for(let i=0;i<w;i++){
    const sx=x+i, sy=y+j;
    if(sx<0||sy<0||sx>=this.W||sy>=this.H) continue;
    const a=(sy*this.W+sx)*4, b=(j*w+i)*4;
    out[b]=this.buf[a]; out[b+1]=this.buf[a+1]; out[b+2]=this.buf[a+2]; out[b+3]=this.buf[a+3];
  }
  return {data:out, width:w, height:h};
};
Ctx.prototype.drawImage = function(){};          // 노드에는 이미지가 없다 — 폴백 경로로 간다
Ctx.prototype.beginPath = function(){};
Ctx.prototype.closePath = function(){};
Ctx.prototype.moveTo = function(){};
Ctx.prototype.lineTo = function(){};
Ctx.prototype.fill = function(){};
Ctx.prototype.stroke = function(){};
Ctx.prototype.strokeRect = function(){};
Ctx.prototype.fillText = function(){};
Ctx.prototype.measureText = function(){ return {width:0}; };

/* ── DOM 스텁 ────────────────────────────────────────────── */
function El(tag){
  this.tagName = (tag||'div').toUpperCase();
  this.style = {}; this.dataset = {}; this.childNodes = [];
  this._text = ''; this._html = ''; this.hidden = false;
  this.className = ''; this.onclick = null;
  const set = new Set();
  this.classList = {
    add:(c)=>set.add(c), remove:(c)=>set.delete(c),
    contains:(c)=>set.has(c),
    toggle:(c,on)=>{ if(on===undefined){ set.has(c)?set.delete(c):set.add(c); } else { on?set.add(c):set.delete(c); } }
  };
}
Object.defineProperty(El.prototype,'textContent',{
  get(){ return this._text; }, set(v){ this._text = String(v); }});
Object.defineProperty(El.prototype,'innerHTML',{
  get(){ return this._html; }, set(v){ this._html = String(v); this.childNodes = []; }});
El.prototype.appendChild = function(n){ this.childNodes.push(n); return n; };
El.prototype.insertBefore = function(n){ this.childNodes.unshift(n); return n; };
El.prototype.removeChild = function(n){
  const i = this.childNodes.indexOf(n); if(i>=0) this.childNodes.splice(i,1); return n; };
El.prototype.querySelector = function(){ return null; };
El.prototype.querySelectorAll = function(){ return []; };

function makeCanvas(w,h){
  const c = new El('canvas');
  c.width = w||300; c.height = h||150;
  let ctx = null;
  c.getContext = function(){ if(!ctx) ctx = new Ctx(c.width, c.height); return ctx; };
  return c;
}

/** game.js 를 새 컨텍스트에서 로드하고 내부 심볼을 돌려준다. */
function load(){
  const els = {};
  const doc = {
    getElementById(id){
      if(!els[id]) els[id] = (id === 'cv') ? makeCanvas(1280,720) : new El('div');
      return els[id];
    },
    createElement(tag){ return tag === 'canvas' ? makeCanvas() : new El(tag); },
    createTextNode(t){ const n = new El('#text'); n._text = t; return n; },
    body: new El('body')
  };
  const listeners = {};
  const sandbox = {
    document: doc,
    console: console,
    performance: {now: () => Date.now()},
    requestAnimationFrame: () => 0,        // 루프를 자동으로 돌리지 않는다. 테스트가 직접 민다.
    setTimeout: (f) => 0,
    addEventListener(t, f){ (listeners[t] = listeners[t] || []).push(f); },
    Image: function(){ this.complete = false; this.naturalWidth = 0; this.naturalHeight = 0; },
    location: {reload(){}},
    Math: Math, Object: Object, Array: Array, JSON: JSON, Set: Set, Map: Map,
    String: String, Number: Number, Boolean: Boolean, Date: Date,
    parseInt: parseInt, parseFloat: parseFloat, isNaN: isNaN,
    Uint8ClampedArray: Uint8ClampedArray
  };
  sandbox.window = sandbox; sandbox.globalThis = sandbox;

  const EXPORT = [
    'R','ALL','W','H','RECIPES','TAGS','FACIL','BASE','MISMATCH','DAYS','DAY_SEC','NIGHT_SEC',
    'POUCH_CAP','ME','BOTS','G','keys','pressed','moveVec','ranked','PROC','STEPS','ST','COOK',
    'QUEUE','stepsOf','isDone','nextProc','D','maxCust','startDay','updateDay','interact','sell',
    'facMul','regularCap','seatCount','dayCustomers','spawnGap','stAt','MAP','HOME','SPAWN',
    'RIVAL','BLDG','ZTYPE','NOISE','N','dist','bagCap','searchMul','rollSlots','startNight',
    'bagAdd','hurt','dropCorpse','toPouch','updateNight','pickSlot','endDay','endNight','freshStations',
    'grid','sprites','depthQ','assets','ASSET_NAMES','LAYER','iso','TW','TH','HW','HH','OX','OY',
    'WALLS','BLDG_BLOCK','blocked','step','OUTLINE_R','contOutlineCol',
    'menuCap','relocate','moveCost','MOVE_COST',
    'DASH_T','DASH_CD','DASH_MUL','KNOCK','STUN_T','BANDAGE_T',
    'mkZombie','findPath','losClear','pathStep','FOV_DEG','FOV_COS',
    'RAID_TIER','TIER_MIN','RAID_P','RAID_BEANS','BEAN_COST','BEAN_PER_SLOT','SERVE_CAP',
    'WARES','rollTag','LURE_T',
    'mkZombie','findPath','losClear','pathStep','FOV_DEG','FOV_COS'
  ];
  // engine.js 가 먼저다 — game.js 가 전역에서 AssetManager 등을 집어 간다.
  const src = fs.readFileSync(path.join(__dirname,'..','engine.js'),'utf8') + '\n'
    + fs.readFileSync(path.join(__dirname,'..','game.js'),'utf8')
    + '\n;globalThis.__X = {' + EXPORT.map(k => k + ':' + k).join(',') + '};\n';

  vm.createContext(sandbox);
  vm.runInContext(src, sandbox, {filename:'game.js'});
  const X = sandbox.__X;
  X.__els = els; X.__doc = doc; X.__sandbox = sandbox;
  return X;
}
module.exports = {load, Ctx, El};
