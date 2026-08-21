"use strict";
/* ══════════ 팔레트 46색 / 11램프 (아트컨셉 v2.1 §3, 컨셉 아트 실측본) ══════════ */
const R = {
  soot :['#06060B','#10101A','#1C1C26','#2B2B33','#3C3F40'],
  amber:['#331109','#562713','#78381A','#A95A2A','#E09A45','#FDCD6F'],
  wood :['#2A2214','#453E23','#6B5733','#8D7A58'],
  bone :['#4A443B','#6E6656','#A2957E','#E8DCC8'],
  dusk :['#050710','#08101A','#0D1826','#121D29','#202D40','#3A4A60'],
  rot  :['#24301C','#4A5A2A','#7A9A4A','#A8C46A'],
  toxic:['#2A6B58','#5FD3A6'],
  blood:['#2A0C0A','#5E1A12','#9B2E28','#D9463C'],
  skin :['#4E3828','#7A5C42','#C9B79A','#E5D4B8'],
  steel:['#1C2024','#343C44','#5E6A74','#9AA8B2'],
  glass:['#26343A','#4A6A72','#8FBFC4']
};
const ALL = Object.keys(R).reduce((a,k)=>a.concat(R[k]),[]);

/* ══════════ 아이소 프리미티브 (64×32, 2:1) ══════════ */
const cv=document.getElementById('cv'), ctx=cv.getContext('2d');
ctx.imageSmoothingEnabled=false;
// 캔버스는 1280x720, 논리 좌표는 640x360. 에셋은 논리의 2배로 저장돼 있어서
// 절반 크기로 그리면 디바이스 픽셀과 1:1로 떨어진다 — 축소도 확대도 없다.
ctx.setTransform(2,0,0,2,0,0);
const W=640,H=360, TW=64,TH=32, HW=32,HH=16, OX=320,OY=80;

/* ── Codex imagegen 에셋 (Art/prep_assets.py 가 주입한다) ── */
const ASSET_NAMES=[
  'barrel',
  'bean_shelf',
  'cafe_home',
  'cafe_rival',
  'cafe_table',
  'cold_brew_tank',
  'corpse',
  'counter',
  'drawer_chest',
  'espresso_machine',
  'grinder',
  'guest_a',
  'guest_b',
  'guest_c',
  'guest_d',
  'metal_container',
  'player_carry',
  'player_idle',
  'player_walk_0',
  'player_walk_1',
  'player_walk_2',
  'player_walk_3',
  'raider_walk_0',
  'raider_walk_1',
  'random_box',
  'serving_station',
  'steam_wand',
  'tile_cafe_floor',
  'tile_zone_floor',
  'wall_cafe',
  'wall_ruined',
  'zombie_walk_0',
  'zombie_walk_1'
];
/* 렌더 코어는 engine.js 가 갖는다 (AssetManager / IsoGrid / SpriteRenderer /
   DepthRenderer). 여기 남은 spr·drawSpr·iso·focus·push 는 전부 얇은 위임이다 —
   호출부 30여 곳을 건드리지 않으려고 이름만 유지한다. */
const E = globalThis;                    // engine.js 가 여기에 올려 둔다
const assets = new E.AssetManager(function(){ return new Image(); }).load(ASSET_NAMES);
const IMG = assets.images;
function spr(k){ return assets.get(k); }
// (cx,cy)=발밑/바닥 중심. 그려졌으면 true.
// anchor: 'foot' (기본) 인물 발이 타일 중심 / 'base' 소품 밑면 마름모가 타일 마름모에 겹침
//         / 'mid' 타일 그림 자체
function drawSpr(k,cx,cy,flip,anchor){
  return sprites.drawAt(k,cx,cy,flip,anchor||'foot');
}
// 인물 머리 위 UI(말풍선·게이지·프롬프트)가 붙는 기준 높이(논리 px).
const CH_H=76;
// 좀비·레이더의 대기 자세는 걷기 세트의 첫 프레임을 쓴다. 따로 생성한 idle은
// 완전히 다른 캐릭터로 나와서(좀비=노인, 레이더=장비 없는 체형) 못 쓴다.
const ASSET_OF={player:'player_idle',raider:'raider_walk_0',zombie:'zombie_walk_0',
                guestA:'guest_a',guestB:'guest_b',guestC:'guest_c',guestD:'guest_d'};
// 걷기 사이클. 프레임이 하나라도 없으면 idle + 호흡으로 폴백한다.
const WALK_SET={
  player:['player_walk_0','player_walk_1','player_walk_2','player_walk_3'],
  zombie:['zombie_walk_0','zombie_walk_1'],
  raider:['raider_walk_0','raider_walk_1']
  // 손님 A 걷기는 뺐다 — 치비 비율이라 idle 및 손님 B·C와 안 맞는다
};
function walkFrame(key,frame){
  const set=WALK_SET[key];
  if(!set || frame<0) return null;
  for(let i=0;i<set.length;i++) if(!spr(set[i])) return null;
  return set[frame%set.length];
}
const grid = new E.IsoGrid(TW,TH,OX,OY);
const sprites = new E.SpriteRenderer(ctx,assets,grid);
const depthQ = new E.DepthRenderer(grid);
const iso=(tx,ty,z)=>grid.toScreen(tx,ty,z);
const focus=(tx,ty)=>grid.focus(tx,ty);

// 2:1 다이아. size 64(풀) / 32(반). (cx,cy)=중심
function diamond(c,cx,cy,size,col){
  const rows=size/2, half=rows/2; c.fillStyle=col;
  for(let r=0;r<rows;r++){
    const hw=(r<half?(r+1):(rows-r))*2;
    c.fillRect(cx-hw,cy-half+r,hw*2,1);
  }
}
// 램프 두 단계를 잇는 체커 디더 (아트컨셉 §4-1). 면 경계에만.
function ditherBand(c,x,y,w,h,col,phase){
  c.fillStyle=col;
  for(let j=0;j<h;j++) for(let i=0;i<w;i++)
    if(((x+i+y+j+(phase||0))&1)===0) c.fillRect(x+i,y+j,1,1);
}
// 아이소 박스. 광원은 위 — 윗면 상위단 / 좌 중간 / 우 하위단 (§3 규칙 5)
function box(c,cx,cy,size,h,ramp,lift){
  const half=size/2, hh=size/4, L=ramp.length-1, up=lift||0;
  const top=ramp[Math.min(L,3+up)], lt=ramp[Math.min(L,2+up)], rt=ramp[Math.max(0,1+up)];
  c.fillStyle=lt; for(let i=0;i<half;i++) c.fillRect(cx-half+i,cy-h+(i>>1),1,h);
  c.fillStyle=rt; for(let i=0;i<half;i++) c.fillRect(cx+i,cy-h+hh-((i+1)>>1),1,h);
  // 면 경계 디더 — 좌/우면이 만나는 아래 모서리를 2px 섞는다
  ditherBand(c,cx-2,cy-h+hh-2,4,Math.min(h,4),ramp[Math.max(0,up)],0);
  diamond(c,cx,cy-h,size,top);
  // 윗면 앞 모서리 1px 하이라이트
  c.fillStyle=ramp[Math.min(L,4+up)]||top;
  for(let i=0;i<half;i++) c.fillRect(cx-half+i,cy-h+(i>>1)-1,1,1);
}
function faceBand(c,cx,cy,size,h,side,x0,x1,yOff,hgt,col){
  const half=size/2, hh=size/4; c.fillStyle=col;
  for(let i=x0;i<x1;i++){
    const x=side==='L'?cx-half+i:cx+i;
    const t=side==='L'?cy-h+(i>>1):cy-h+hh-((i+1)>>1);
    c.fillRect(x,t+yOff,1,hgt);
  }
}
// 타일 한 칸을 깔고 그 위에 어둡기를 얹는다. dim 0 이면 그대로.
// checker 는 홀수 칸을 살짝 눌러 체커 느낌을 만든다 (아트컨셉 §6).
function floorTile(key,cx,cy,dim,checker,warm){
  if(!drawSpr(key,cx,cy,false,'mid')) return false;
  const d=dim+(checker?0.10:0);
  if(warm>0){ ctx.save(); ctx.globalAlpha=warm; diamond(ctx,cx,cy,64,R.amber[3]); ctx.restore(); }
  if(d>0){ ctx.save(); ctx.globalAlpha=d; diamond(ctx,cx,cy,64,R.soot[0]); ctx.restore(); }
  return true;
}

// 1px 도트 링 — 소음/구역 반경. 아이소 비율 그대로
function ring(c,cx,cy,rTiles,col,alpha,dash){
  const rx=rTiles*HW, n=Math.max(40,(rx*1.4)|0);
  c.save(); c.globalAlpha=alpha; c.fillStyle=col;
  for(let i=0;i<n;i++){
    if(dash&&(i%7)>3) continue;
    const a=i/n*Math.PI*2;
    c.fillRect((cx+Math.cos(a)*rx)|0,(cy+Math.sin(a)*rx/2)|0,1,1);
  }
  c.restore();
}
/* ── 재질 (아트컨셉 §4-3) ── */
function woodGrain(c,cx,cy,size,h){          // 결 1px 라인
  c.fillStyle=R.wood[1];
  const half=size/2, hh=size/4;
  for(let k=1;k<4;k++){
    const yo=(h*k/4)|0;
    for(let i=2;i<half-2;i++) c.fillRect(cx-half+i,cy-h+(i>>1)+yo,1,1);
  }
}
function metalSheen(c,cx,cy,size,h){         // 상단 하이라이트 + 하단 반사광
  const half=size/2, hh=size/4;
  c.fillStyle=R.steel[3];
  for(let i=1;i<half-1;i++) c.fillRect(cx-half+i,cy-h+(i>>1)+1,1,1);
  c.fillStyle=R.steel[0];
  for(let i=1;i<half-1;i++) c.fillRect(cx-half+i,cy+(i>>1)-2,1,1);
}
function glassSheen(c,x,y,w,h){              // 대각 2px 하이라이트 1줄
  c.fillStyle=R.glass[2];
  for(let i=0;i<Math.min(w,h*2);i++) c.fillRect(x+i,y+((h-1)-(i>>1)),2,1);
}

/* ══════════ 3×5 도트 글리프 — 월드에 한글을 그리지 않는다 (§8) ══════════ */
const GLY={
 '0':['###','#.#','#.#','#.#','###'],'1':['.#.','##.','.#.','.#.','###'],
 '2':['###','..#','###','#..','###'],'3':['###','..#','.##','..#','###'],
 '4':['#.#','#.#','###','..#','..#'],'5':['###','#..','###','..#','###'],
 '6':['###','#..','###','#.#','###'],'7':['###','..#','..#','..#','..#'],
 '8':['###','#.#','###','#.#','###'],'9':['###','#.#','###','..#','###'],
 '+':['...','.#.','###','.#.','...'],'-':['...','...','###','...','...'],
 'G':['###','#..','#.#','#.#','###'],'E':['###','#..','##.','#..','###'],
 'x':['...','#.#','.#.','#.#','...'],'!':['.#.','.#.','.#.','...','.#.'],
 '?':['###','..#','.##','...','.#.'],' ':['...','...','...','...','...']
};
function dots(c,s,x,y,col,scale){
  const S=scale||1; c.fillStyle=col;
  for(let i=0;i<s.length;i++){
    const g=GLY[s[i]]||GLY[' '];
    for(let r=0;r<5;r++) for(let k=0;k<3;k++)
      if(g[r][k]==='#') c.fillRect(x+i*4*S+k*S,y+r*S,S,S);
  }
}
const dotsW=(s,S)=>s.length*4*(S||1)-(S||1);

/* ══════════ 12×12 아이콘 — 맛 태그 / 스테이션 / 컨테이너 (§8) ══════════ */
function icon(c,kind,x,y){
  const P=(dx,dy,col)=>{ c.fillStyle=col; c.fillRect(x+dx,y+dy,1,1); };
  const box2=(x0,y0,w,h,col)=>{ c.fillStyle=col; c.fillRect(x+x0,y+y0,w,h); };
  switch(kind){
    case 'Taste.Sweet':                                   // 각설탕
      box2(2,3,8,7,R.bone[2]); box2(2,3,8,2,R.bone[3]);
      box2(2,9,8,1,R.bone[1]); box2(3,4,3,2,R.bone[3]); break;
    case 'Taste.Bitter':                                  // 커피콩
      box2(3,2,6,8,R.amber[2]); box2(4,1,4,10,R.amber[2]);
      box2(3,2,6,3,R.amber[3]); box2(5,2,2,8,R.amber[0]); break;
    case 'Taste.Sour':                                    // 레몬
      box2(3,3,6,6,R.amber[5]); box2(2,4,8,4,R.amber[5]);
      box2(3,3,4,2,'#FFF0C0'); box2(4,8,4,1,R.amber[4]); break;
    case 'Taste.Nutty':                                   // 견과
      box2(4,2,4,2,R.wood[3]); box2(3,4,6,6,R.wood[2]);
      box2(3,4,6,2,R.wood[3]); box2(4,8,4,1,R.wood[1]); break;
    case 'Rare.PreWar':                                   // 봉인된 전전 캔
      box2(3,2,6,8,R.steel[2]); box2(3,2,6,2,R.steel[3]);
      box2(3,5,6,2,R.blood[3]); box2(3,9,6,1,R.steel[0]); break;
    case 'st.empty':  box2(2,5,8,4,R.bone[0]); box2(2,5,8,1,R.bone[1]); break;
    case 'st.busy':   box2(2,5,8,4,R.amber[2]); box2(2,5,8,1,R.amber[4]);
                      box2(4,3,4,2,R.amber[5]); break;
    case 'st.done':   box2(3,4,6,6,R.bone[3]); box2(3,4,6,2,'#FFFFFF');
                      P(4,10,R.toxic[1]); P(5,11,R.toxic[1]); P(6,10,R.toxic[1]);
                      P(7,9,R.toxic[1]); P(8,8,R.toxic[1]); break;
    case 'st.burnt':  box2(3,4,6,6,R.soot[1]); box2(3,4,6,2,R.soot[3]);
                      box2(4,1,1,3,R.blood[2]); box2(6,0,1,4,R.blood[3]);
                      box2(8,1,1,3,R.blood[2]); break;
  }
}

/* ══════════ 인물 40×64 (아트컨셉 v2.1 §5) ══════════
   컨셉 시트(Art/concept/sheet_characters.png)의 디자인을 네이티브 크기로 옮긴 것.
   램프 3개 이하 + 실루엣 1px 아웃라인, 광원은 위. */
const CH={
  player:{hair:R.bone,  skin:R.skin, cloth:R.wood,  accent:R.amber, apron:true,  bag:false},
  raider:{hair:R.bone,  skin:R.skin, cloth:R.dusk,  accent:R.steel, apron:false, bag:true},
  guestA:{hair:R.wood,  skin:R.skin, cloth:R.rot,   accent:R.wood,  apron:false, bag:true},
  guestB:{hair:R.blood, skin:R.skin, cloth:R.blood, accent:R.wood,  apron:false, bag:true},
  guestC:{hair:R.bone,  skin:R.skin, cloth:R.dusk,  accent:R.steel, apron:false, bag:true},
  guestD:{hair:R.soot,  skin:R.skin, cloth:R.glass, accent:R.bone,  apron:false, bag:false},
  zombie:{hair:R.rot,   skin:R.rot,  cloth:R.soot,  accent:R.toxic, apron:false, bag:false, dead:true}
};
// (cx,cy)=발밑. frame -1 대기 / 0..3 걷기 / 'search' 수색 / 'carry' 운반
function actor(c,cx,cy,key,frame,flip,carry){
  // Codex 에셋이 있으면 그걸 쓴다. 걷기 프레임이 없으므로 1px 상하 호흡으로 대신한다.
  // 잔을 들고 있으면 운반 자세가 우선(운반 걷기 프레임은 없다)
  var carrying = (key==='player' && carry==='cup' && spr('player_carry'));
  var w = carrying ? null : walkFrame(key,frame);
  var a = carrying ? 'player_carry' : (w || ASSET_OF[key]);
  if(a && spr(a)){
    c.save(); c.globalAlpha=.38; diamond(c,cx,cy,32,R.soot[0]); c.restore();
    // 걷기 프레임이 있으면 그 안에 움직임이 들어 있으므로 호흡을 얹지 않는다
    var bob = (!w && frame>=0) ? [0,-1,0,-1][frame&3] : 0;
    drawSpr(a,cx,cy+bob,flip);
    if(carry && carry!=='cup'){                       // 손에 든 원두 / 분쇄 원두
      var col = carry==='beans'?R.wood:R.steel;
      c.fillStyle=col[2]; c.fillRect(cx-20,cy-40,10,9);
      c.fillStyle=col[3]; c.fillRect(cx-20,cy-40,10,3);
    }
    return;
  }
  const S=CH[key]||CH.player;
  const hz = frame>=0 ? [0,-1,0,-1][frame&3] : 0;     // 걷기 상하 1px
  const lean = S.dead?2:0;
  const F=(x,y,w,h,col)=>{ c.fillStyle=col; c.fillRect((flip? cx-(x-cx)-w : x),y,w,h); };
  const top=cy-64+hz;

  // ── 그림자
  c.save(); c.globalAlpha=.38; diamond(c,cx,cy,32,R.soot[0]); c.restore();

  // ── 다리 (18 wide)
  const legSpread = frame>=0 ? [5,2,5,2][frame&3] : 3;
  const legL=R.soot, cl=S.cloth;
  F(cx-9,cy-20,7,17,cl[1]); F(cx-9,cy-20,3,17,cl[2]);      // 왼다리(밝은쪽)
  F(cx+2,cy-20,7,17,cl[0]); F(cx+2,cy-20,3,17,cl[1]);      // 오른다리
  F(cx-10-((legSpread-3)>>1),cy-4,9,4,R.wood[0]);          // 신발
  F(cx+1+((legSpread-3)>>1),cy-4,9,4,R.wood[1]);
  F(cx-10,cy-1,20,1,R.soot[0]);

  // ── 몸통 (24 wide × 24)
  const ty=cy-44+lean;
  F(cx-12,ty,24,25,cl[1]);
  F(cx-12,ty,10,25,cl[2]);                                 // 좌면 밝게
  F(cx+6,ty,6,25,cl[0]);                                   // 우면 어둡게
  ditherBand(c,cx-3,ty,4,25,cl[2],0);                      // 면 경계 디더
  if(S.apron){                                             // 앞치마 — 컨셉의 바리스타
    F(cx-9,ty+7,18,18,R.rot[1]); F(cx-9,ty+7,7,18,R.rot[2]);
    F(cx-9,ty+7,18,1,R.rot[3]);
    F(cx-2,ty+15,5,2,R.wood[2]);                           // 허리끈 매듭
  }
  if(S.bag){ F(cx+8,ty+3,6,14,S.accent[1]); F(cx+8,ty+3,6,2,S.accent[2]); }
  // 팔
  const armSwing = frame>=0 ? [2,0,-2,0][frame&3] : 0;
  F(cx-16,ty+3+armSwing,5,15,cl[2]);
  F(cx+11,ty+3-armSwing,5,15,cl[0]);
  F(cx-16,ty+16+armSwing,5,5,S.skin[2]);                   // 손
  F(cx+11,ty+16-armSwing,5,5,S.skin[1]);

  // ── 머리 20×22
  const hy=cy-64+hz+lean*2;
  F(cx-10,hy+2,20,20,S.skin[2]);
  F(cx-10,hy+2,8,20,S.skin[3]);                            // 좌면 밝게
  F(cx+6,hy+2,4,20,S.skin[1]);                             // 우면 어둡게
  F(cx-10,hy,20,7,S.hair[2]);                              // 머리 위
  F(cx-10,hy,20,3,S.hair[3]);
  F(cx-11,hy+3,3,11,S.hair[2]); F(cx+8,hy+3,3,11,S.hair[1]);
  if(S.dead){                                              // 좀비 — 독성 눈 + 상처
    F(cx-7,hy+11,4,3,R.toxic[1]); F(cx+2,hy+11,4,3,R.toxic[1]);
    F(cx-6,hy+12,2,1,'#FFFFFF');
    F(cx-3,hy+17,7,2,R.rot[0]); F(cx+3,hy+6,4,3,R.blood[1]);
  } else {
    F(cx-7,hy+11,3,3,R.soot[0]); F(cx+3,hy+11,3,3,R.soot[0]);   // 눈
    F(cx-6,hy+11,1,1,R.bone[3]); F(cx+4,hy+11,1,1,R.bone[3]);
    F(cx-2,hy+17,5,1,R.skin[0]);                                // 입
  }
  // ── 실루엣 아웃라인 1px, 검정이 아니라 램프 최암부 (§4-2)
  F(cx-11,hy,1,22,S.hair[0]); F(cx+10,hy,1,22,S.hair[0]);
  F(cx-13,ty,1,25,cl[0]);     F(cx+12,ty,1,25,cl[0]);

  // ── 손에 든 것
  if(carry==='beans'){ F(cx-19,ty+12,9,9,R.wood[2]); F(cx-19,ty+12,9,3,R.wood[3]);
                       F(cx-17,ty+15,2,2,R.amber[1]); }
  if(carry==='ground'){ F(cx-19,ty+13,9,8,R.steel[2]); F(cx-19,ty+13,9,2,R.steel[3]);
                        F(cx-17,ty+16,5,3,R.amber[0]); }
  if(carry==='cup'){ F(cx-18,ty+13,8,8,R.bone[3]); F(cx-18,ty+13,8,2,'#FFFFFF');
                     F(cx-16,ty+15,4,3,R.amber[1]); F(cx-10,ty+15,2,3,R.bone[2]); }
}

/* ══════════ 데이터 (기획서 v1.2 §8) ══════════ */
// 태그 배수는 기획서 §6.6 표를 그대로 쓴다 — 일반 ×1.0 / 고급 ×1.5 / 희귀 ×2.0 / 전전 ×3.0.
// 일반이 ×1.0인 건 의도다. 배수를 얻으려면 밤에 나가서 등급을 올려야 한다.
const RECIPES=[
  {n:'재의 에스프레소', t:'Taste.Bitter',    r:'일반', m:1.0},
  {n:'각설탕 라떼',     t:'Taste.Sweet',     r:'일반', m:1.0},
  {n:'레몬 콜드브루',   t:'Taste.Sour',      r:'고급', m:3.0},
  {n:'견과 모카',       t:'Taste.Nutty',     r:'희귀', m:5.0},
  {n:'봉인된 캔커피',   t:'Rare.PreWar',     r:'전전', m:9.0}
];
const TAGS=RECIPES.map(r=>r.t);
// 가격은 기획서 §6.6 표.
// 금고·자물쇠·레시피 슬롯은 뺐다. 10일 완주 실측 순증이 각각 +9G / −1G / **−84G** 였다 —
// 골드가 시설 말고 쓸 데가 없어서 '골드를 지키는 시설'의 값이 0에 붙었고,
// 슬롯은 수요 70%를 더 잘게 쪼개 기대 배수를 희석시켜 **사면 손해**였다.
// 빈자리는 §6.5 의 황혼 행상(밤 소모품)이 채운다 — 낮 골드 → 밤 생존의 배선이다.
const FACIL=[
  {id:'seat',   n:'좌석 증설',   c:150, d:'손님 +6, 단골 상한 +4, 동시 대기 +1 — 매출은 오르지만 동선이 더 겹칩니다'},
  {id:'roast',  n:'로스터 업그레이드', c:200, d:'시설 배수 +0.1 — 모든 잔의 판매가가 오릅니다'},
  {id:'pack',   n:'배낭',        c:200, d:'검색 속도 30% 단축'}
];
// 황혼 행상 — 밤에 쓰는 소모품. 시설과 달리 **매일 다시 산다**. 낮에 번 골드가
// 다음 밤의 생존 확률이 되어야 낮↔밤이 양방향 고리가 된다 (§6.5).
const WARES=[
  {id:'bandage', n:'붕대',   c:60, d:'밤에 R 홀드 3초로 체력 1칸 회복'},
  {id:'bait',    n:'미끼',   c:80, d:'밤에 F 로 던진다 — 떨어진 자리로 좀비가 몰린다'},
  {id:'battery', n:'배터리', c:70, d:'그날 밤 손전등 반경 +40%'}
];
// §4 하루의 구조 / §6.6 기준 수치
const BASE=10, MISMATCH=.5, DAYS=10, DAY_SEC=180, NIGHT_SEC=180;
// 습격 한 번이 주고받는 생산 능력. tier 1당 봇 일 매출 35G 이므로 0.5 = 하루 17.5G.
// 10일이면 최대 175G — 봇 par(약 1435G)의 12%다. 한 번으로 판이 뒤집히지 않되
// 반복하면 누적으로 갈린다.
const RAID_TIER=0.5, TIER_MIN=0.2, RAID_P=.45;
const RAID_BEANS=30;                      // 습격 1회 = 파밍 한 밤과 동급 (§6.5)
// 등급 배수는 **특수 재료를 써야 발동한다**(§6.5). 재고가 없으면 못 만드는 게 아니라
// 그냥 만들어지고 ×1.0 으로 팔린다 — "기본 원두 무한"이라는 파산 방지 바닥을 안 깬다.
// 원두는 밤에만 나오고 살 수 없다. 레시피가 3일이면 포화하는 자산인 반면
// 원두는 소모품이라 포화하지 않는다 — 매일 밤 나갈 이유가 여기서 나온다.
const BEAN_COST={'일반':0,'고급':1,'희귀':2,'전전':3};
// 12 로 잡은 근거: 10이면 무등급 잔이 10일에 8잔 남고, 15면 0.7잔으로 남아돈다.
// 12가 "가끔 모자라는" 지점이다(무등급 2.3잔). 사람은 잘못 만들고 버리므로 더 쓴다.
const BEAN_PER_SLOT=12;                   // 전리품 1칸 = 원두 12개
const POUCH_CAP=2;                                    // 보관 주머니 — 죽어도 유지 (§7.5)
// 낮 대시 — 0.22초 동안 2.6배. 쿨다운 2.5초. 한 번에 약 1.7타일을 번다.
// 쿨다운이 짧으면 걷기가 사라지고, 길면 아무도 안 쓴다.
const DASH_T=0.22, DASH_CD=2.5, DASH_MUL=2.6;
// 이사 (§5). 뒤처진 쪽의 탈출구다 — 비싸지만 존재해야 한다.
// 비용은 골드 400 + 그날 낮 매출의 절반(이사하는 동안 영업을 못 한 몫).
// 누적 판매(ME.sales)는 건드리지 않는다 — 습격과 같은 원칙으로 승리 지표는 못 뺏는다.
const MOVE_COST=400;
const moveCost=()=>MOVE_COST+Math.round(G.daySales/2);
function relocate(){
  const c=moveCost();
  if(ME.gold<c) return false;
  ME.gold-=c;
  BOTS.forEach(b=>{ b.knows=false; });        // 나를 가리키는 위치 정보가 전부 무효
  ME.moves=(ME.moves||0)+1;
  return true;
}

const ME={name:'나',sales:0,gold:100,day:1,owned:[0],menu:[0,-1,-1,-1],stock:0,regulars:0,
          regs:[], bought:[],
          fac:{},found:[],pouch:[],hurtDay:0};
const BOTS=[
  // gold 필드는 지웠다 — 훔칠 금액을 재는 데만 쓰이고 봇의 점수·행동엔 영향이 0이었다.
  // 이제 주고받는 것은 tier(=일일 매출 증가분)다. 승리 지표가 판매 총액이므로
  // 상호작용도 판매 능력에 닿아야 한다.
  {name:'세 번째 골목',  sales:0,tier:1,menu:[1,2],knows:false},
  {name:'재의 바리스타', sales:0,tier:1,menu:[3,0],knows:false}
];
const G={phase:'boot',t:0,daySales:0,bag:[],sortie:'farm'};

/* ══════════ 입력 ══════════ */
const keys={}, pressed={};
addEventListener('keydown',e=>{
  if(e.target && e.target.tagName==='BUTTON') return;
  if(['ArrowUp','ArrowDown','ArrowLeft','ArrowRight',' '].indexOf(e.key)>=0) e.preventDefault();
  if(!keys[e.code]) pressed[e.code]=true;
  keys[e.code]=true;
});
addEventListener('keyup',e=>{ keys[e.code]=false; });
const hit=c=>{ if(pressed[c]){ pressed[c]=false; return true; } return false; };
const held=c=>!!keys[c];
// 입력은 화면 기준으로 받고 타일 좌표로 회전해서 돌려준다.
// 쿼터뷰에서 화면 오른쪽 = 타일 (+1,-1), 화면 아래 = 타일 (+1,+1).
// 회전 없이 타일에 그대로 더하면 조작이 45도 돌아간다.
function moveVec(){
  let sx=0,sy=0;
  if(held('KeyA')||held('ArrowLeft')) sx--;
  if(held('KeyD')||held('ArrowRight')) sx++;
  if(held('KeyW')||held('ArrowUp')) sy--;
  if(held('KeyS')||held('ArrowDown')) sy++;
  if(!sx && !sy) return [0,0];
  return [sx+sy, sy-sx];
}

/* ══════════ UI ══════════ */
const $=id=>document.getElementById(id);
const ov=$('ov'), logEl=$('log');
const esc=s=>String(s).replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'})[c]);
const say=h=>{ logEl.innerHTML=h; };
const PH={boot:'대기',day:'낮 · 운영',dusk:'황혼 · 준비',night:'밤 · 침투',settle:'결산'};
function ranked(){
  return [{name:ME.name,sales:ME.sales,me:true}]
    .concat(BOTS.map(b=>({name:b.name,sales:b.sales,me:false})))
    .sort((a,b)=>b.sales-a.sales);
}
const myRank=()=>ranked().findIndex(p=>p.me)+1;
let lastPhase='';
function hud(){
  if(G.phase!==lastPhase){                    // 페이즈 전환 순간 CSS 플래시
    lastPhase=G.phase;
    const f=$('flash');
    if(f){ f.classList.add('on'); setTimeout(()=>f.classList.remove('on'),40); }
  }
  $('hDay').textContent=ME.day; $('hPhase').textContent=PH[G.phase]||'';
  $('hSales').textContent=Math.round(ME.sales); $('hGold').textContent=Math.round(ME.gold);
  $('hRank').textContent=myRank();
  document.body.classList.toggle('night',G.phase==='night');
  $('rank').innerHTML=ranked().map((p,i)=>
    '<div class="row'+(p.me?' self':'')+'"><span class="rk">'+(i+1)+'</span><span>'+esc(p.name)
    +(p.me?'<span class="tagme">ME</span>':'')+'</span><span class="dy">DAY '+ME.day
    +'</span><span class="sc">'+Math.round(p.sales)+'G</span></div>').join('');
}
const timerBar=f=>{ $('hTimer').style.width=Math.max(0,Math.min(1,f))*100+'%'; };

/* ══════════════════ 낮 — 오버쿡식 제조 (기획서 v1.2 §6.1) ══════════════════
   v1은 스테이션이 종류당 1대뿐이라 병렬이 원천 불가능했다. 한 잔을 끝내야
   다음 잔을 시작할 수 있으니 오버쿡이 아니라 그냥 줄 서기였다.
   v2에서 바꾼 것 둘:
     1) 그라인더·머신을 2대씩 둬서 여러 잔이 항상 겹치게 한다
     2) 레시피마다 거치는 공정을 다르게 해서, 무엇을 메뉴에 올리느냐가
        곧 동선 설계가 되게 한다 */
const FW=9, FD=6;

// 공정별 소요 시간과 방치 허용 시간. 콜드브루는 오래 걸리지만 타지 않는다 —
// "미리 걸어두고 그동안 다른 걸 한다"는 선택지를 만들기 위해서다.
const PROC={
  grind :{t:2.0, burn:5.0, ok:'분쇄',   bad:'과분쇄'},
  brew  :{t:3.0, burn:4.5, ok:'추출',   bad:'탄 잔'},
  cold  :{t:7.0, burn:1e9, ok:'냉침',   bad:''},
  finish:{t:1.6, burn:6.0, ok:'마무리', bad:'식은 잔'}
};
// 레시피별 공정. 빈 배열이면 선반에서 꺼내는 즉시 완성이다.
const STEPS={
  'Taste.Bitter':['grind','brew'],            // 에스프레소 — 최단 공정
  'Taste.Sweet' :['grind','brew','finish'],   // 라떼 — 스팀 한 단계 더
  'Taste.Sour'  :['grind','cold'],            // 콜드브루 — 길지만 안 탄다
  'Taste.Nutty' :['grind','brew','finish'],   // 모카
  'Rare.PreWar' :['finish']                   // 봉인을 뜯어 데운다 — 공정 0단계는 조리 게임을 삭제한다
};
const ST=[
  {id:'shelf', type:'shelf',  tx:0, ty:0},
  {id:'g1',    type:'grind',  tx:2, ty:0},
  {id:'g2',    type:'grind',  tx:2, ty:2},
  {id:'b1',    type:'brew',   tx:4, ty:0},
  {id:'b2',    type:'brew',   tx:4, ty:2},
  {id:'c1',    type:'cold',   tx:6, ty:0},
  {id:'f1',    type:'finish', tx:6, ty:2},
  {id:'serve', type:'serve',  tx:4, ty:4},
  // 쓰레기 봉투 — 잘못 만든 잔·잘못 꺼낸 재료를 버린다. 이게 없으면 손에 든 것을
  // 처리할 방법이 스테이션에 넣는 것뿐이라, 취향이 안 맞는 잔을 들면 손이 묶인다.
  {id:'trash', type:'trash',  tx:0, ty:4}
];
const COOK=ST.filter(s=>PROC[s.type]);
const QUEUE=[[1.5,5],[3,5],[4.5,5],[6,5],[7.5,5],[9,5]];   // 좌석 증설 시 6칸까지

const stepsOf = r => STEPS[RECIPES[r].t] || [];
const isDone  = it => !!it && it.stage >= stepsOf(it.r).length;
const nextProc= it => isDone(it) ? null : stepsOf(it.r)[it.stage];
// 손에 든 것의 겉모습 — 원두 / 분쇄·중간 / 완성된 잔
const carryLook = it => !it ? null : (isDone(it) ? 'cup' : (it.stage===0 ? 'beans' : 'ground'));

function freshStations(){
  const o={};
  COOK.forEach(s=>{ o[s.id]={state:'idle', t:0, item:null}; });
  return o;
}
const D={p:{x:3,y:3},dir:1,moving:false,carry:null,recipe:0,cust:[],spawn:0,floats:[],
         st:freshStations(), served:[], flash:0, dash:0, dashCd:0, queue:[]};

// 시설 배수 = 1.0 + 0.1 × 로스터 레벨 (최대 ×1.5). 지금은 1단계만 판다.
const facMul   = () => Math.min(1.5, 1 + 0.1*(ME.fac.roast?1:0));
// 메뉴판 칸 수 (§6.6 레시피 슬롯 +1). ME.menu 배열 길이를 직접 늘리지 않고 이 함수로
// 자른다 — 길이를 늘렸다 줄이면 등록해 둔 레시피가 조용히 사라진다.
const menuCap  = () => 3;                 // 레시피 슬롯 시설은 삭제(사면 손해였다). 호출부는 유지
const seatStep = () => ME.fac.seat?1:0;
// 단골 상한 = 좌석 수. 자리가 없으면 더 안 늘어난다.
const seatCount   = () => 4 + seatStep()*4;
const regularCap  = () => seatCount();
// 하루 손님 수 = 12 + (좌석단계 × 4) + 단골 수  (§6.6)
// 손님 24명. 실측 처리량 한계(3공정 레시피 3분 26잔)의 92%에 붙였다 —
// 12명일 때는 공급이 수요의 3.9배라 유휴가 77%였고, 순서를 틀릴 이유가 없었다.
const dayCustomers = () => 24 + seatStep()*6 + ME.regulars;
function maxCust(){ return ME.fac.seat?6:5; }         // 동시에 줄 설 수 있는 인원
// 등록한 메뉴 쪽으로 손님이 몰리게 한다. 아니면 만들 수 없는 주문만 들어온다.
// 분포는 예전 인라인 코드 그대로다 - 뽑는 **시점만** 낮 시작으로 옮겼다(F3).
// 같이 바꾸면 주문 큐 공개의 효과를 잴 수 없다.
function rollTag(){
  const pool=ME.menu.slice(0,menuCap()).filter(v=>v>=0);
  return (pool.length && Math.random()<.7)
    ? RECIPES[pool[(Math.random()*pool.length)|0]].t
    : TAGS[(Math.random()*TAGS.length)|0];
}
function startDay(){
  G.phase='day'; G.t=DAY_SEC; G.daySales=0;
  // 하루치 주문을 **미리** 뽑는다. 이게 있어야 다음 3장을 보여 줄 수 있고,
  // 그래야 7초짜리 냉침을 미리 걸 이유가 생긴다 (F3).
  // 단골은 **자기 취향을 들고** 온다. 나머지는 추첨. 단골이 늘수록 하루가 예측
  // 가능해지는 게 보상이다 - 미리 걸어 둘 수 있는 잔이 늘어난다.
  D.queue=ME.regs.map(function(g){ return g.tag; })
    .concat(Array.from({length:Math.max(0,dayCustomers()-ME.regs.length)}, rollTag));
  for(let i=D.queue.length-1;i>0;i--){                 // 섞는다 - 단골만 먼저 오면 안 된다
    const j=(Math.random()*(i+1))|0, t=D.queue[i]; D.queue[i]=D.queue[j]; D.queue[j]=t;
  }
  D.budget=D.queue.length;                            // 오늘 올 손님 총량
  N.hp=3;                                             // 낮 시작 시 체력 전량 회복 (§7.3)
  D.p={x:3,y:3}; D.carry=null; D.cust=[]; D.spawn=1.2; D.floats=[]; D.served=[]; D.flash=0;
  D.st=freshStations();
  D.recipe=ME.menu.findIndex(v=>v>=0); if(D.recipe<0) D.recipe=0;
  grid.camX=0; grid.camY=0; ov.hidden=true; hud(); bar();
  say('<b>1/2/3</b> 으로 만들 메뉴를 고르고 <b>선반</b>에서 집으세요. 레시피마다 거치는 공정이 다릅니다.'
      +(ME.hurtDay===ME.day?' <span class="bad">어젯밤 부상 — 오늘 동작이 20% 느립니다.</span>':''));
}
// 하루 총량을 낮 길이에 균등 분산한다. 단골이 늘면 손님이 늘고, 그만큼 간격이 좁아진다.
const spawnGap=()=>Math.max(1.6, DAY_SEC/Math.max(1,dayCustomers()));
const stAt=(tx,ty)=>ST.find(s=>Math.abs(s.tx-tx)<1.1 && Math.abs(s.ty-ty)<1.1);

function updateDay(dt){
  // 페이즈가 넘어간 뒤에 한 틱이라도 더 들어오면 endDay() 가 다시 돌아 봇 매출이
  // 이중으로 쌓인다(endDay 는 멱등하지 않다). loop() 가 이미 막고 있지만, 돈이
  // 걸린 경로라 여기서도 막는다 — 호출부를 믿는 것보다 싸다.
  if(G.phase!=='day') return;
  G.t-=dt; timerBar(G.t/DAY_SEC);
  D.flash=Math.max(0,D.flash-dt);

  const v=moveVec(); D.moving=!!(v[0]||v[1]);
  D.dash=Math.max(0,D.dash-dt); D.dashCd=Math.max(0,D.dashCd-dt);
  // 대시 (§10). 밤의 Shift 가 '뛰기=소음=위험'인 것과 정반대로 낮은 순수 이득이다.
  // 같은 키가 페이즈마다 반대 성질인 게 의도다 — 낮엔 소음이라는 개념이 없다.
  if(hit('ShiftLeft')||hit('ShiftRight')){
    if(D.dashCd<=0 && D.moving){ D.dash=DASH_T; D.dashCd=DASH_CD; }
  }
  if(D.moving){
    // 전날 밤에 죽었으면 다음 낮의 이동·제조가 20% 느리다 (§7.5)
    const l=Math.hypot(v[0],v[1]), sp=3.0*(ME.hurtDay===ME.day?0.8:1)*(D.dash>0?DASH_MUL:1);
    D.p.x=Math.max(-0.4,Math.min(FW-1+0.4,D.p.x+v[0]/l*sp*dt));
    D.p.y=Math.max(-0.4,Math.min(FD-1.2,D.p.y+v[1]/l*sp*dt));
    const sdx=v[0]-v[1]; if(sdx) D.dir=sdx>0?1:-1;
  }
  for(let i=0;i<menuCap();i++) if(hit('Digit'+(i+1)) && ME.menu[i]>=0){
    D.recipe=i; say('만들 메뉴: <b>'+RECIPES[ME.menu[i]].n+'</b> ('+
      (stepsOf(ME.menu[i]).map(k=>PROC[k].ok).join(' → ')||'조리 없음')+')'); bar();
  }

  // 모든 공정 스테이션이 동시에 돌아간다 — 이게 병렬성의 전부다
  COOK.forEach(s=>{
    const st=D.st[s.id];
    if(st.state==='busy'){ st.t-=dt; if(st.t<=0){ st.state='done'; st.t=PROC[s.type].burn; } }
    else if(st.state==='done'){
      st.t-=dt;
      if(st.t<=0){ st.state='burnt'; D.flash=.08;
        say('<span class="bad">'+PROC[s.type].bad+' — 비우고 다시 하세요.</span>'); }
    }
  });

  if(hit('KeyE')){ const s=stAt(D.p.x,D.p.y); if(s) interact(s); }

  D.spawn-=dt;
  if(D.spawn<=0 && D.cust.length<maxCust() && D.queue.length>0){
    D.spawn=spawnGap();
    const tag = D.queue.shift(); D.budget=D.queue.length;
    D.cust.push({tx:10.5,ty:5,tag:tag,pat:25,max:25,
                 skin:['guestA','guestB','guestC','guestD'][(Math.random()*4)|0],state:'walk'});
  }
  D.cust.forEach((c,i)=>{
    const q=QUEUE[Math.min(i,QUEUE.length-1)];
    if(c.tx>q[0]+.02){ c.tx=Math.max(q[0],c.tx-2.4*dt); c.state='walk'; }
    else { c.tx=q[0]; c.state='wait'; c.pat-=dt; }
  });
  const kept=D.cust.filter(c=>c.pat>0);
  if(kept.length!==D.cust.length){
    // 손님이 나가면 단골도 하나 떠난다 (§6.6). 실패가 다음 날까지 이어져야 압박이 생긴다.
    const lost=D.cust.length-kept.length;
    let gone=null;
    for(let i=0;i<lost && ME.regs.length;i++) gone=ME.regs.pop();
    ME.regulars=ME.regs.length; hud();
    say('<span class="bad">손님이 기다리다 나갔습니다'
        +(gone?' — 단골 한 명이 발길을 끊었습니다.':'.')+'</span>');
  }
  D.cust=kept;

  // 잔은 오래된 것부터, 손님은 줄 앞에서부터. **태그가 맞는 첫 쌍**만 판다.
  // F1(손님 24명)이 줄을 길게 만들었는데 FIFO 를 남긴 것이 충돌이었다 — 잔을 만드는
  // 6~7초 사이에 맨 앞 손님이 바뀌어서, 손님이 늘수록 불일치가 늘고 손해가 됐다.
  // 실측: 불일치 79잔 -> 27잔, 좌석의 한계효용 -265G -> +245G 로 부호가 뒤집힌다.
  if(D.served.length && D.cust.length){
    let si=-1, ci=-1;
    for(let a=0;a<D.served.length && si<0;a++)
      for(let b=0;b<D.cust.length;b++)
        if(D.cust[b].state==='wait' && RECIPES[D.served[a].r].t===D.cust[b].tag){ si=a; ci=b; break; }
    if(si>=0) sell(si,ci);
    // 교착 방지 밸브 — 맞는 쌍이 없는데 서빙대까지 찼으면 맨 앞 손님에게 흘려보낸다(x0.5).
    // 이게 없으면 아무도 안 시키는 잔 3개가 칸을 물고 낮이 잠긴다.
    else if(D.served.length>=SERVE_CAP && D.cust[0].state==='wait') sell(0,0);
  }

  D.floats.forEach(f=>{ f.l-=dt; f.y-=26*dt; });
  D.floats=D.floats.filter(f=>f.l>0);
  if(G.t<=0) endDay();
}

function interact(s){
  if(s.type==='shelf'){
    if(D.carry) return say('<span class="bad">손이 비어 있어야 합니다.</span>');
    const r=ME.menu[D.recipe];
    if(r<0) return say('<span class="bad">그 슬롯은 비어 있습니다. 1/2/3 으로 고르세요.</span>');
    // 원두는 **집는 순간** 차감되고 잔에 o 플래그로 붙어 다닌다. 판매 시점에 재면
    // 서빙대에 올려 둔 잔의 등급이 나중 재고에 따라 흔들린다.
    const cost=BEAN_COST[RECIPES[r].r]||0;
    const o=ME.stock>=cost;
    if(o) ME.stock-=cost;
    D.carry={r:r, stage:0, o:o}; bar(); hud();
    if(!o) return say('<span class="bad">원두가 모자랍니다 — <b>'+RECIPES[r].n+'</b>이 등급 없이 나갑니다 (×1.0)</span>');
    return say(isDone(D.carry) ? '<b>'+RECIPES[r].n+'</b> — 조리 없이 바로 낼 수 있습니다.'
                               : '<b>'+RECIPES[r].n+'</b> 시작 — '+PROC[nextProc(D.carry)].ok+' 부터');
  }
  if(s.type==='trash'){
    if(!D.carry) return say('버릴 게 없습니다.');
    const n=RECIPES[D.carry.r].n;
    D.carry=null; bar();
    return say('<b>'+n+'</b> — 버렸습니다.');
  }
  if(s.type==='serve'){
    // 빈손이면 되가져온다. 이게 없으면 아무도 안 시키는 잔이 칸을 영구 점유해
    // 낮이 잠긴다 - 상한을 거는 순간 반드시 같이 있어야 하는 짝이다.
    if(!D.carry){
      if(!D.served.length) return say('<span class="bad">낼 잔이 없습니다.</span>');
      D.carry=D.served.pop(); bar();
      return say('<b>'+RECIPES[D.carry.r].n+'</b>을(를) 되가져왔습니다.');
    }
    if(!isDone(D.carry)) return say('<span class="bad">아직 <b>'+PROC[nextProc(D.carry)].ok+'</b> 공정이 남았습니다.</span>');
    if(D.served.length>=SERVE_CAP)
      return say('<span class="bad">서빙대가 찼습니다 ('+SERVE_CAP+'/'+SERVE_CAP+') — 손님부터 받으세요.</span>');
    D.served.push(D.carry); D.carry=null; bar();
    return say(D.cust.length?'':'잔을 올려뒀습니다. 손님이 오면 나갑니다.');
  }
  const st=D.st[s.id];
  if(st.state==='burnt'){ st.state='idle'; st.item=null; D.flash=.05; bar(); return say('버렸습니다.'); }
  if(st.state==='done'){
    if(D.carry) return say('<span class="bad">손이 찼습니다.</span>');
    D.carry=st.item; D.carry.stage++; st.item=null; st.state='idle'; bar();
    return say(isDone(D.carry) ? '<b>'+RECIPES[D.carry.r].n+'</b> 완성 — 서빙대로'
                               : '다음 공정: <b>'+PROC[nextProc(D.carry)].ok+'</b>');
  }
  if(st.state==='busy') return say('아직 <b>'+PROC[s.type].ok+'</b> 중입니다.');
  if(!D.carry) return say('<span class="bad">넣을 게 없습니다.</span>');
  if(isDone(D.carry)) return say('<span class="bad">이미 완성됐습니다. 서빙대로 가세요.</span>');
  if(nextProc(D.carry)!==s.type)
    return say('<span class="bad">이 공정이 아닙니다 — <b>'+PROC[nextProc(D.carry)].ok+'</b> 부터입니다.</span>');
  st.item=D.carry; D.carry=null; st.state='busy';
  st.t=PROC[s.type].t*(ME.hurtDay===ME.day?1.25:1);     // 사망 페널티는 제조 속도에도 걸린다
  bar();
  say(PROC[s.type].ok+' 시작'+(PROC[s.type].burn<1e8?' — 다 되면 바로 빼세요':''));
}

// 잔당 골드 = 기본가 × 태그 배수 × 시설 배수  (기획서 §6.6)
// 태우거나 손님이 나가면 여기 오지 않는다 — 제공한 잔만 계산된다.
function sell(si,ci){
  const cup=D.served.splice(si||0,1)[0], c=D.cust.splice(ci||0,1)[0];
  let g=BASE, sub='', col=R.bone[3];
  const r=RECIPES[cup.r], fm=facMul();
  if(r.t===c.tag){
    const m=cup.o?r.m:1.0;                 // 원두를 못 쓴 잔은 등급이 안 붙는다 (§6.5)
    g=BASE*m*fm; sub=(cup.o?'x'+r.m:'무등급')+(fm>1?' x'+fm.toFixed(1):''); col=cup.o?R.amber[5]:R.bone[3];
    // 만족한 손님은 30% 확률로 단골이 된다. 단골 상한 = 좌석 수.
    if(ME.regs.length<regularCap() && Math.random()<0.30){
      // 얼굴과 취향이 고정된다. 내일 **같은 사람이 같은 것을 시키러** 온다.
      ME.regs.push({skin:c.skin, tag:c.tag});
      ME.regulars=ME.regs.length;
      say('<span class="hit">'+r.n+' 적중 — 단골이 됐습니다 ('+ME.regs.length+'명)</span>');
    } else say('<span class="hit">'+r.n+' 적중 — +'+Math.round(g)+'G</span>');
  }
  else { g=BASE*MISMATCH*fm; sub='-'; col=R.blood[3];
    say('<span class="bad">취향 불일치 — '+Math.round(g)+'G</span>'); }
  ME.sales+=g; ME.gold+=g; G.daySales+=g;
  const p=iso(c.tx,c.ty,0);
  D.floats.push({x:p[0],y:p[1]-CH_H-22,txt:'+'+Math.round(g)+'G',sub:sub,c:col,l:1.2});
  hud(); bar();
}

// 색은 Codex 타일 에셋(tile_cafe_floor.png)에서 실측했다. 붉은 타일과 크림 타일의
// 체커. 램프에서 멀어질수록 램프를 한 단씩 내려 식힌다 (아트컨셉 §6 조명 계단).
function floorDay(tx,ty){
  const even=((tx+ty)&1)===0;
  if(tx>=FW-1) return even?R.soot[2]:R.soot[1];         // 문밖 — 잿빛
  const d=Math.abs(tx-3)+Math.abs(ty-2);
  return d<=2 ? (even?R.blood[2]:R.amber[5])
       : d<=4 ? (even?R.blood[1]:R.amber[3])
       : (even?R.amber[0]:R.amber[2]);
}

// 스테이션 에셋 — 앞에서부터 있는 것을 쓴다. 전용 에셋이 없으면 대체품으로 떨어진다.
const ST_ASSET={shelf:['bean_shelf'], grind:['grinder'], brew:['espresso_machine'],
                trash:['barrel'],
                cold:['cold_brew_tank','metal_container'],
                finish:['steam_wand','serving_station'], serve:['counter']};
const stAsset=ty=>(ST_ASSET[ty]||[]).find(spr)||null;
// 컨테이너·건물 에셋. 호출부에 문자열을 박지 않고 여기 모은 이유는 하나다 —
// 아래 셀프체크가 ASSET_NAMES 와 대조할 수 있어야 오타가 조용히 폴백되지 않는다.
const CONT_ASSET={crate:'metal_container',drawer:'drawer_chest',random:'random_box',corpse:'corpse'};
const BLDG_ASSET={home:'cafe_home',rival:'cafe_rival'};
// 컨테이너 아웃라인 색 — 새것 / 수색중 / 이미 턴 것이 서로 달라야 한다 (§7.2-1).
// 인라인 삼항으로 두면 "다른 색인가"를 화면으로만 확인할 수 있어서 함수로 뺐다.
const OUTLINE_R=3.0;                                   // 손전등 반경(타일)
const contOutlineCol=st=>st==='done'?R.bone[1]:(st==='busy'?R.amber[5]:R.bone[3]);
function drawStation(s,t){
  const p=iso(s.tx,s.ty,0), st=D.st[s.id];
  const A=stAsset(s.type);
  if(A){
    drawSpr(A,p[0],p[1],false,'base');
    if(s.type==='serve') D.served.forEach(function(c,i){    // 올려둔 잔 (최대 3)
      var x=p[0]-14+i*11, y=p[1]-56;
      ctx.fillStyle=R.bone[3]; ctx.fillRect(x,y,8,8);
      ctx.fillStyle='#FFFFFF'; ctx.fillRect(x,y,8,2);
      ctx.fillStyle=c.o?R.amber[1]:R.soot[3]; ctx.fillRect(x+2,y+3,4,3);   // 무등급은 흐리게
      icon(ctx,RECIPES[c.r].t,x-2,y-11);                    // 어떤 잔인지 (§8 아이콘)
    });
    stationBadge(s,st,p[0],p[1]-(spr(A).naturalHeight/2)-26,t);
    return;
  }
  // 에셋 없을 때 폴백
  box(ctx,p[0],p[1],64,30,R.wood); woodGrain(ctx,p[0],p[1],64,30);
  stationBadge(s,st,p[0],p[1]-60,t);
}
// 상태 아이콘 + 진행바 (§8 아이콘, §9 이펙트)
function stationBadge(s,st,cx,iy,t){
  if(!st) return;
  icon(ctx,{idle:'st.empty',busy:'st.busy',done:'st.done',burnt:'st.burnt'}[st.state],cx-6,iy);
  if(st.state==='busy'){
    var f=1-st.t/PROC[s.type].t;
    ctx.fillStyle=R.soot[0]; ctx.fillRect(cx-16,iy+14,32,4);
    ctx.fillStyle=R.amber[5]; ctx.fillRect(cx-16,iy+14,(32*f)|0,4);
  } else if(st.state==='done'){
    if(PROC[s.type].burn>1e8){                          // 안 타는 공정은 게이지 대신 완료 표시만
      ctx.fillStyle=R.toxic[1]; ctx.fillRect(cx-16,iy+14,32,4);
    } else {
      var g=st.t/PROC[s.type].burn;                     // 방치 게이지 — 다 닳으면 탄다
      ctx.fillStyle=R.soot[0]; ctx.fillRect(cx-16,iy+14,32,4);
      ctx.fillStyle=g>.4?R.toxic[1]:R.blood[3]; ctx.fillRect(cx-16,iy+14,(32*g)|0,4);
    }
  } else if(st.state==='burnt'){
    for(var i=0;i<3;i++){                                // blood-3 연기 (§9)
      var life=(t*.8+i*.33)%1;
      ctx.globalAlpha=1-life; ctx.fillStyle=R.blood[i%2?3:2];
      ctx.fillRect(cx-4+i*4,iy-((life*18)|0),3,3);
    }
    ctx.globalAlpha=1;
  }
  // 어떤 잔이 들어 있는지 — 맛 아이콘으로 (월드에 글자를 쓰지 않는다 §8)
  if(st.item && st.state!=='burnt') icon(ctx,RECIPES[st.item.r].t,cx-6,iy-15);
}

function drawDay(t){
  grid.camX=0; grid.camY=0;
  ctx.fillStyle=R.soot[1]; ctx.fillRect(0,0,W,H);
  for(let ty=0;ty<FD;ty++) for(let tx=0;tx<FW;tx++){
    const p=iso(tx,ty,0), even=((tx+ty)&1)===0;
    const d=Math.abs(tx-3)+Math.abs(ty-2);                 // 램프에서 멀수록 식는다
    const dim = tx>=FW-1 ? .70 : (d<=2 ? 0 : d<=4 ? .22 : .44);
    const warm = tx>=FW-1 ? 0 : (d<=2 ? .20 : d<=4 ? .08 : 0);
    if(!floorTile('tile_cafe_floor',p[0],p[1],dim,!even,warm))
      diamond(ctx,p[0],p[1],64,floorDay(tx,ty));            // 에셋 없으면 절차적으로
  }
  // 벽 2면 + 창 (창밖은 늘 잿빛)
  for(let tx=0;tx<FW-1;tx++){ const p=iso(tx,-1,0); box(ctx,p[0],p[1],64,64,R.soot); }
  for(let ty=-1;ty<FD;ty++){
    const p=iso(-1,ty,0); box(ctx,p[0],p[1],64,64,R.soot);
    // tx=-1 벽의 실내면은 '우측면'이다. 방(tx>=0)이 화면상 오른쪽-아래에 있으므로.
    if(ty===1||ty===2){                                  // 창 — 밖은 늘 잿빛 폐허
      faceBand(ctx,p[0],p[1],64,64,'R',2,30,14,34,R.wood[1]);
      faceBand(ctx,p[0],p[1],64,64,'R',4,28,16,30,R.bone[1]);
      faceBand(ctx,p[0],p[1],64,64,'R',4,28,16,2,R.bone[2]);
      faceBand(ctx,p[0],p[1],64,64,'R',4,28,40,4,R.soot[3]);
      faceBand(ctx,p[0],p[1],64,64,'R',15,17,16,30,R.wood[1]);
    }
  }

  // 정렬은 engine.js 의 DepthRenderer 가 한다. push 는 (깊이, 레이어, 그리기)를
  // 그대로 넘기는 얇은 껍데기 — 호출부 30여 곳의 시그니처를 유지하려고 남겼다.
  // 큐는 프레임마다 비우고 시작한다. 예전 `const q=[]` 는 매 프레임 새 배열이라
  // 공짜로 보장되던 건데, 공유 큐로 바꾸면서 중간에 예외가 나면 다음 프레임으로
  // 샌다. 여기서 비워야 그 성질이 유지된다.
  depthQ.clear();
  const push=(d,z,f)=>depthQ.pushDepth(d,z,f);
  ST.forEach(s=>push(s.tx+s.ty,0,()=>drawStation(s,t)));
  // 출입구 — 손님은 여기로 들어온다. tx=FW-1 열은 문밖(잿빛)이다.
  push(8+5,0,()=>{
    const p=iso(FW-1,5,0);
    ctx.fillStyle=R.soot[0]; ctx.fillRect(p[0]-13,p[1]-54,26,46);   // 어두운 문간
    box(ctx,p[0]-15,p[1],16,58,R.wood);                             // 문설주
    box(ctx,p[0]+15,p[1],16,58,R.wood);
    ctx.fillStyle=R.wood[3]; ctx.fillRect(p[0]-20,p[1]-62,40,5);    // 상인방
    ctx.fillStyle=R.wood[1]; ctx.fillRect(p[0]-20,p[1]-57,40,2);
    ctx.fillStyle=R.amber[5]; ctx.fillRect(p[0]-2,p[1]-70,4,4);     // 문등
    ctx.fillStyle=R.amber[3]; ctx.fillRect(p[0]-3,p[1]-66,6,2);
  });
  [[0,4],[7,1]].forEach(o=>push(o[0]+o[1],0,()=>{        // 테이블 — 장식
    const p=iso(o[0],o[1],0);
    if(drawSpr('cafe_table',p[0],p[1],false,'base')) return;
    box(ctx,p[0],p[1],32,20,R.wood); woodGrain(ctx,p[0],p[1],32,20);
    ctx.fillStyle=R.bone[3]; ctx.fillRect(p[0]-3,p[1]-26,6,5);
  }));
  push(D.p.x+D.p.y,1000,()=>{
    const p=iso(D.p.x,D.p.y,0);
    actor(ctx,p[0],p[1],'player',D.moving?((t*7)|0)&3:-1,D.dir<0,carryLook(D.carry));
    const s=stAt(D.p.x,D.p.y);
    if(s) dots(ctx,'E',p[0]-2,p[1]-CH_H-28,R.amber[5],2);  // 상호작용 프롬프트
  });
  D.cust.forEach((c,i)=>push(c.tx+c.ty,1000,()=>{
    const p=iso(c.tx,c.ty,0);
    actor(ctx,p[0],p[1],c.skin,c.state==='walk'?((t*7)|0)&3:-1,true,null);
    if(c.state==='wait'){
      const by=p[1]-CH_H-26;
      ctx.fillStyle=R.soot[0]; ctx.fillRect(p[0]-9,by,18,16);        // 말풍선
      ctx.fillStyle=R.bone[3]; ctx.fillRect(p[0]-8,by+1,16,14);
      ctx.fillRect(p[0]-2,by+15,4,3);
      icon(ctx,c.tag,p[0]-6,by+3);
      const f=Math.max(0,c.pat/c.max);
      ctx.fillStyle=R.soot[0]; ctx.fillRect(p[0]-14,p[1]-CH_H-6,28,4);
      ctx.fillStyle=f>.35?(i?R.bone[1]:R.amber[5]):R.blood[3];
      ctx.fillRect(p[0]-14,p[1]-CH_H-6,(28*f)|0,4);
    }
  }));
  depthQ.flush();

  D.floats.forEach(f=>{
    ctx.globalAlpha=Math.min(1,f.l*2);
    dots(ctx,f.txt,(f.x-dotsW(f.txt,2)/2)|0,f.y|0,f.c,2);
    if(f.sub) dots(ctx,f.sub,(f.x-dotsW(f.sub,1)/2)|0,(f.y+14)|0,R.bone[1],1);
    ctx.globalAlpha=1;
  });
  if(D.flash>0){ ctx.globalAlpha=.45; ctx.fillStyle=R.blood[2]; ctx.fillRect(0,0,W,H); ctx.globalAlpha=1; }
}

/* ══════════════════ 밤 — 타르코프식 루팅 (기획서 v1.2 §7.2) ══════════════════ */
const MAP=26, HOME=[3.5,21.5], SPAWN=[5,21];
const RIVAL=[[21.5,4.5],[21.5,21.5]];
// 좀비는 맵에 고루 퍼져 있지 않다. 정해진 건물에만 스폰된다. (§7.2-4)
// 등급별 스폰 수는 기획서 §9.3 표. run = 추격자 수.
const BLDG=[
  {x:8, y:8, w:6,h:5, z:4, run:2, name:'A', rich:true},   // 중급 — 배회자 4 + 추격자 2
  {x:16,y:12,w:5,h:5, z:3, run:0, name:'B', rich:true},   // 하급 — 배회자 3
  {x:9, y:16,w:5,h:4, z:0, run:0, name:'C', rich:false}   // 빈 건물 — 서랍 몇 개가 전부
];
// 좀비 2종 (§9.2). 소리 감지 반경이 유일한 입력이다.
const ZTYPE={
  shambler:{hp:3, spd:1.5, hear:8,  sight:3, forget:6,  atk:1.5},
  runner  :{hp:2, spd:3.5, hear:12, sight:8, forget:10, atk:1.0}
};
// 소음 반경 일람 (§9.2)
const NOISE={walk:2, search:4, run:8, melee:3};
// 피격 넉백/경직 (§7.3). 경직 0.5초 < 무적 1.4초 — 밀려난 뒤 도망칠 여지는 남긴다.
const KNOCK=0.9, STUN_T=0.5;
// 시야각 (§7.3). **소리는 전방향, 눈은 전방 100도만.** 이 둘이 갈라져야
// "뒤로 돌아간다"가 전략이 되고 백스탭이 운이 아니라 계획이 된다.
// 좌/우 2벌 스프라이트로는 좀비가 어디를 보는지 못 읽으므로 바닥에 시야 콘을 그린다
// (drawNight 참조) — 판정이 보이지 않으면 그 판정은 없느니만 못하다.
const FOV_DEG=100;
const FOV_COS=Math.cos(FOV_DEG*Math.PI/360);          // 반각의 코사인
const BANDAGE_T=3.0;                       // 붕대 사용 시간 (§7.3)
const LURE_T=6.0;                          // 미끼가 소리를 내는 시간(초)
// 서빙대 3칸. 예전엔 상한 없는 배열이라 **전반 60초에 다 만들고 120초 서 있기**가
// 최적해였다 - 오버쿡의 동시 진행 압박이 그 우회로 하나로 통째로 죽어 있었다.
const SERVE_CAP=3;
const N={p:{x:0,y:0},dir:1,moving:false,run:false,hp:3,inv:0,shake:0,stun:0,heal:0,baits:0,flBoost:1,lure:null,
         cont:[],zomb:[],open:null,hold:0,noise:0,raid:0,target:null,
         swing:0,searchHold:0};
const dist=(a,b)=>Math.hypot(a.x-b.x,a.y-b.y);
const bagCap=()=>ME.fac.pack?9:6;                       // §7.5 — 기본 6, 배낭 9
const searchMul=()=>ME.fac.pack?0.70:1;                 // 배낭 −30% (§9.4)

function inBldg(b,x,y){ return x>=b.x && x<b.x+b.w && y>=b.y && y<b.y+b.h; }
// 타일 격자 A*. 직선 조향만 쓰면 좀비가 벽 모서리에 걸려 제자리에서 비빈다 —
// 벽 충돌이 들어간 뒤로 실제로 그랬다. 대각 이동은 허용하되 두 직교 칸이 모두
// 뚫려 있을 때만 — 안 그러면 벽 모서리를 대각으로 통과한다.
// ponytail: open 리스트를 배열 선형 탐색으로 고른다. 26x26=676칸이라 이걸로 충분하다.
//           맵이 커지면 이진 힙으로 바꾼다.
const DIRS8=[[1,0],[-1,0],[0,1],[0,-1],[1,1],[1,-1],[-1,1],[-1,-1]];
function findPath(sx,sy,gx,gy){
  sx|=0; sy|=0; gx|=0; gy|=0;
  if(sx===gx && sy===gy) return [];
  if(blocked(gx+.5,gy+.5)) return null;               // 목표가 벽이면 길이 없다
  const K=(x,y)=>y*MAP+x;
  const g={}, f={}, prev={}, closed={};
  const open=[[sx,sy]];
  g[K(sx,sy)]=0; f[K(sx,sy)]=Math.hypot(gx-sx,gy-sy);
  let guard=MAP*MAP*4;                                // 무한 루프 방지 — 항상 끝난다
  while(open.length && guard-->0){
    let bi=0;
    for(let i=1;i<open.length;i++)
      if(f[K(open[i][0],open[i][1])] < f[K(open[bi][0],open[bi][1])]) bi=i;
    const [cx,cy]=open.splice(bi,1)[0], ck=K(cx,cy);
    if(cx===gx && cy===gy){                           // 도착 — 경로를 되짚는다
      const path=[]; let k=ck, x=cx, y=cy;
      while(prev[k]!==undefined){ path.unshift([x+.5,y+.5]); [x,y]=prev[k]; k=K(x,y); }
      return path;
    }
    closed[ck]=1;
    for(const [dx,dy] of DIRS8){
      const nx=cx+dx, ny=cy+dy, nk=K(nx,ny);
      if(nx<0||ny<0||nx>=MAP||ny>=MAP||closed[nk]) continue;
      if(blocked(nx+.5,ny+.5)) continue;
      // 대각으로 벽 모서리를 스쳐 지나가지 못하게 한다
      if(dx&&dy&&(blocked(cx+dx+.5,cy+.5)||blocked(cx+.5,cy+dy+.5))) continue;
      const ng=g[ck]+(dx&&dy?1.4142:1);
      if(g[nk]!==undefined && ng>=g[nk]) continue;
      g[nk]=ng; f[nk]=ng+Math.hypot(gx-nx,gy-ny); prev[nk]=[cx,cy];
      if(!open.some(o=>o[0]===nx&&o[1]===ny)) open.push([nx,ny]);
    }
  }
  return null;                                        // 닿을 수 없다
}
// 두 점 사이에 벽이 없으면 true. 경로 웨이포인트를 건너뛰어 이동을 매끄럽게 한다 —
// 안 하면 좀비가 타일 중심마다 꺾여서 격자를 따라 걷는 티가 난다.
function losClear(x0,y0,x1,y1){
  const n=Math.ceil(Math.hypot(x1-x0,y1-y0)*3);
  for(let i=1;i<n;i++){
    const t=i/n;
    if(blocked(x0+(x1-x0)*t, y0+(y1-y0)*t)) return false;
  }
  return true;
}
// 좀비를 목표 (gx,gy) 로 A* 경로를 따라 한 틱 움직인다.
// 경로는 **목표 타일이 바뀔 때만** 다시 푼다 — 매 프레임 풀면 낭비다.
// 배회 중에도 가는 쪽을 봐야 한다 — 안 그러면 옆걸음질하는 좀비가 된다.
function mkZombie(kind,x,y,sx,sy,home){
  const T=ZTYPE[kind];
  return {x:x, y:y,
    sx:sx, sy:sy,                          // 스폰 지점 — 놓치면 여기로 복귀한다
    vx:0, vy:0, wt:0, chase:0,
    hx:x, hy:y, probe:0,                   // hx,hy = 마지막으로 들은/본 좌표
    fx:0, fy:1,                            // 보고 있는 방향 (시야각 판정용)
    path:null, pgoal:-1,                   // A* 경로 캐시
    kind:kind, hp:T.hp, T:T, cd:0, home:home};
}
function faceVel(z){
  const l=Math.hypot(z.vx,z.vy);
  if(l>.01){ z.fx=z.vx/l; z.fy=z.vy/l; }
}
function pathStep(z,gx,gy,spd,dt){
  const gk=(gy|0)*MAP+(gx|0);
  if(z.pgoal!==gk || !z.path){ z.pgoal=gk; z.path=findPath(z.x,z.y,gx,gy); }
  if(!z.path) return step(z,(gx-z.x)*.05,(gy-z.y)*.05,.5,MAP-.5);   // 길이 없으면 밀어보기만
  // 눈에 보이는 앞쪽 웨이포인트로 건너뛴다
  while(z.path.length>1 && losClear(z.x,z.y,z.path[1][0],z.path[1][1])) z.path.shift();
  const w=z.path[0]||[gx,gy];
  const dx=w[0]-z.x, dy=w[1]-z.y, l=Math.max(.001,Math.hypot(dx,dy));
  if(l<.25 && z.path.length) z.path.shift();
  step(z, dx/l*spd*dt, dy/l*spd*dt, .5, MAP-.5);
  if(dx||dy){ z.fx=dx/l; z.fy=dy/l; }                 // 가는 쪽을 본다
}
// 벽 타일. 건물은 북(y=b.y-1)·서(x=b.x-1) 두 면에만 벽을 그리고 남·동은 트여 있다 —
// 그 트인 쪽이 입구다. 모서리(b.x-1,b.y-1)도 넣어야 대각선으로 새지 않는다.
// 이게 없어서 좀비가 그려진 벽을 그대로 통과했다.
const WALLS=(function(){
  const w=new Set();
  BLDG.forEach(b=>{
    for(let i=-1;i<b.w;i++) w.add((b.x+i)+','+(b.y-1));   // 북면 + 모서리
    for(let j=0;j<b.h;j++)  w.add((b.x-1)+','+(b.y+j));   // 서면
  });
  return w;
})();
// 카페 3채는 밑면 앵커 타일 1칸만 막는다 (§7.6). WALLS 와 섞지 않는다 —
// 테스트 11.1/11.2 가 WALLS 를 "그려지는 폐건물 벽"과 1:1로 대조하기 때문.
const BLDG_BLOCK=new Set([HOME].concat(RIVAL).map(p=>Math.floor(p[0])+','+Math.floor(p[1])));
const blocked=(x,y)=>{ const k=Math.floor(x)+','+Math.floor(y);
  return WALLS.has(k)||BLDG_BLOCK.has(k); };
// 축을 따로 밀어 본다 — 벽에 비스듬히 부딪혀도 미끄러진다. 플레이어와 좀비가
// 같은 함수를 쓴다. 한쪽만 막으면 반대쪽이 벽을 통과한다.
// ponytail: 타일 단위 판정이라 벽 두께가 1타일이다. 더 정밀한 히트박스가 필요해지면 AABB로.
function step(e,dx,dy,lo,hi){
  const nx=Math.max(lo,Math.min(hi,e.x+dx));
  if(!blocked(nx,e.y)) e.x=nx;
  const ny=Math.max(lo,Math.min(hi,e.y+dy));
  if(!blocked(e.x,ny)) e.y=ny;
}
function rollSlots(kind){
  // 한 컨테이너에서 나오는 아이템 수는 매번 랜덤이다. 빈 통도 나온다. (§7.2-3)
  // [최소 슬롯, 최대 슬롯, 슬롯당 검색 시간] — §9.4 표 그대로
  const spec={drawer:[1,3,1.0],crate:[2,6,1.5],corpse:[2,4,1.5],random:[1,1,6.0]}[kind];
  const n=spec[0]+((Math.random()*(spec[1]-spec[0]+1))|0);
  const slots=[];
  for(let i=0;i<n;i++){
    let item=null;
    const roll=Math.random();
    if(roll<0.30) item=null;                                // 빈 슬롯
    // 붕대는 시체에서 잘 나온다 (§9.4 전리품 표) — 남이 죽은 자리가 회복 자원이 된다
    else if(roll < (kind==='corpse'?0.52:0.38)) item={kind:'bandage',label:'붕대'};
    else if(roll<0.68) item={kind:'stock',n:BEAN_PER_SLOT,label:'원두 ×'+BEAN_PER_SLOT};
    else {
      const pool=[];
      RECIPES.forEach((r,ix)=>{
        if(r.r==='전전' && kind!=='random') return;         // 전전 레시피는 랜덤 박스에서만
        if(r.r==='희귀' && kind==='drawer') return;
        pool.push(ix);
      });
      const id=pool[(Math.random()*pool.length)|0];
      item={kind:'recipe',id:id,label:RECIPES[id].n};
    }
    slots.push({item:item,rev:0,taken:false,moving:0});
  }
  return {slots:slots, time:spec[2], pick:-1};
}
function startNight(){
  G.phase='night'; G.t=NIGHT_SEC; G.bag=[];
  N.p={x:SPAWN[0],y:SPAWN[1]}; N.hp=3; N.inv=0; N.shake=0; N.open=null; N.hold=0; N.raid=0;
  N.stun=0; N.heal=0; N.bait=0; N.baits=0; N.flBoost=1;
  // 행상에서 산 것을 실어 준다. 붕대는 가방 칸을 먹고(자리 다툼이 곧 결정이다),
  // 미끼·배터리는 칸을 안 먹는다 — 소모 방식이 달라 가방에 둘 이유가 없다.
  ME.bought.forEach(function(id){
    if(id==='bandage') bagAdd({kind:'bandage',label:'붕대'});
    else if(id==='bait') N.baits++;
    else if(id==='battery') N.flBoost=1.4;
  });
  ME.bought=[];
  N.cont=[]; N.zomb=[];
  BLDG.forEach(b=>{
    const n=b.rich?4:2;
    for(let i=0;i<n;i++){
      const kind=b.rich ? (i===0?'crate':'drawer') : 'drawer';
      N.cont.push(Object.assign({x:b.x+0.8+Math.random()*(b.w-1.6),
                                 y:b.y+0.8+Math.random()*(b.h-1.6),
                                 kind:kind,state:'new',bldg:b.name},rollSlots(kind)));
    }
    // 좀비는 건물에만 (§7.2-4)
    // 배회자 + 추격자를 등급표대로 섞는다
    const mk=(kind)=>{
      N.zomb.push(mkZombie(kind, b.x+Math.random()*b.w, b.y+Math.random()*b.h,
                           b.x+b.w/2, b.y+b.h/2, b)); };
    for(let i=0;i<b.z;i++)   mk('shambler');
    for(let i=0;i<(b.run||0);i++) mk('runner');
  });
  // 거리의 컨테이너 + 시체 + 희귀 랜덤 박스
  [['crate',13,6],['corpse',6,13],['crate',18,18],['random',12.5,12.5]].forEach(o=>{
    N.cont.push(Object.assign({x:o[1],y:o[2],kind:o[0],state:'new',bldg:'-'},rollSlots(o[0])));
  });
  ov.hidden=true; hud(); bar();
  say(G.sortie==='raid'
    ? '남의 카페를 노립니다. 가까이서 <b>E</b>를 누르고 있으면 메뉴판을 베낍니다.'
    : '건물 안에 좋은 물자가 있고, <b>좀비도 건물에만</b> 있습니다. <b>E</b>로 컨테이너를 엽니다.');
}
function bagAdd(it){
  if(G.bag.length>=bagCap()) return false;
  G.bag.push(it); bar(); return true;
}
// src 를 주면 그 반대 방향으로 밀려나고 0.5초 경직된다 (§7.3).
// 경직이 없으면 피격 무적 1.4초 덕분에 오히려 좀비 사이를 걸어 나갈 수 있었다 —
// "3마리에게 둘러싸이면 못 빠져나온다"가 성립하려면 맞은 순간 못 움직여야 한다.
function hurt(dmg,src){
  if(N.inv>0) return;
  N.hp-=(dmg||1); N.inv=1.4; N.shake=.3; N.open=null;
  N.stun=STUN_T;
  if(src){
    const dx=N.p.x-src.x, dy=N.p.y-src.y, l=Math.max(.001,Math.hypot(dx,dy));
    step(N.p, dx/l*KNOCK, dy/l*KNOCK, .6, MAP-.6);     // 벽을 뚫고 밀려나면 안 된다
  }
  if(N.hp<=0){
    // 죽으면 그 자리에 시체가 남고, 들고 있던 게 전부 그 안에 들어간다 (§7.5)
    dropCorpse();
    ME.hurtDay=ME.day+1;                        // 다음 날 낮 −20%
    say('<span class="bad">쓰러졌습니다. 가방을 잃었습니다 — 보관 주머니만 남습니다.</span>');
    G.bag=[]; endNight(false);
  }
  else say('<span class="bad">좀비에게 당했습니다. HP '+N.hp+'</span>');
  bar();
}
// 플레이어 시체 = 컨테이너. 검색 프로그레스가 똑같이 적용된다.
function dropCorpse(){
  if(!G.bag.length) return;
  N.cont.push({x:N.p.x, y:N.p.y, kind:'corpse', state:'new', bldg:'-', time:1.5, pick:-1,
    slots:G.bag.map(it=>({item:it, rev:0, taken:false, moving:0}))});
}
// 보관 주머니로 옮기기 — 2칸, 죽어도 유지 (§7.5)
function toPouch(i){
  const it=G.bag[i];
  if(!it) return false;
  if(ME.pouch.length>=POUCH_CAP){ say('<span class="bad">보관 주머니가 찼습니다.</span>'); return false; }
  ME.pouch.push(it); G.bag.splice(i,1); bar();
  say('<b>'+it.label+'</b> 을(를) 보관 주머니에 넣었습니다 — 죽어도 유지됩니다.');
  return true;
}

function updateNight(dt){
  if(G.phase!=='night') return;             // updateDay 와 같은 이유 (endNight 도 멱등하지 않다)
  G.t-=dt; timerBar(G.t/NIGHT_SEC);
  N.inv=Math.max(0,N.inv-dt); N.shake=Math.max(0,N.shake-dt);
  N.stun=Math.max(0,N.stun-dt);
  N.noise=0;

  const v=moveVec();
  N.moving=!!(v[0]||v[1]) && N.stun<=0;      // 경직 중에는 입력이 먹지 않는다
  // 검색 중에는 이동 불가. 움직이면 중단되고, 공개된 데까지는 유지된다. (§7.2-2)
  if(N.open && N.moving){ N.open=null; say('수색을 중단했습니다. 공개된 슬롯은 남습니다.'); }

  if(N.moving && !N.open){
    N.run=held('ShiftLeft')||held('ShiftRight');
    const l=Math.hypot(v[0],v[1]), sp=N.run?4.6:2.4;
    step(N.p, v[0]/l*sp*dt, v[1]/l*sp*dt, .6, MAP-.6);
    const sdx=v[0]-v[1]; if(sdx) N.dir=sdx>0?1:-1;
    N.noise=N.run?NOISE.run:NOISE.walk;
  }
  focus(N.p.x,N.p.y);

  // 남의 카페 발견
  RIVAL.forEach((s,i)=>{
    const b=BOTS[i];
    if(ME.found.indexOf(b.name)<0 && dist(N.p,{x:s[0],y:s[1]})<2.2){
      ME.found.push(b.name);
      say('<b>'+esc(b.name)+'</b>의 카페를 발견했습니다. 위치는 계속 기억됩니다.');
    }
  });

  // 근접 컨테이너 (§7.2-1) — 아웃라인은 손전등 반경 안에서만
  let near=null;
  N.cont.forEach(c=>{ const d=dist(N.p,c); if(d<1.5 && (!near||d<dist(N.p,near))) near=c; });

  // 숫자키로 슬롯 선택 — 마우스 없이도 되게
  if(N.open) for(let i=0;i<9;i++) if(hit('Digit'+(i+1))) pickSlot(i);

  // E는 홀드다 (§10). 탭하면 줍기·귀환, 홀드하면 수색.
  if(near && near.state!=='done' && held('KeyE') && !N.open){
    N.searchHold+=dt;
    if(N.searchHold>=0.18){ N.open=near; near.state='busy'; N.searchHold=0; }
  } else if(!held('KeyE')) N.searchHold=0;
  // 홀드를 떼면 수색이 즉시 멈춘다
  if(N.open && !held('KeyE')){ N.open=null; say('수색을 멈췄습니다. 공개된 슬롯은 남습니다.'); }
  if(hit('KeyE') && !near && dist(N.p,{x:HOME[0],y:HOME[1]})<2.0){ endNight(true); return; }
  // 붕대 — R 홀드 3초로 1칸 회복. 이동하면 취소된다 (§7.3).
  // 3초 동안 못 움직인다는 게 전부다 — 좀비 앞에서 감으면 그대로 맞는다.
  const bi=G.bag.findIndex(x=>x.kind==='bandage');
  if(held('KeyR') && bi>=0 && N.hp<3 && !N.moving && !N.open){
    N.heal+=dt;
    if(N.heal>=BANDAGE_T){
      N.heal=0; G.bag.splice(bi,1); N.hp=Math.min(3,N.hp+1);
      say('<span class="ok">붕대를 감았습니다 — 체력 '+N.hp+'/3</span>'); bar();
    }
  } else N.heal=0;
  // 미끼 — F 로 앞쪽 4타일에 던진다. 떨어진 자리를 좀비가 '들은 소리'로 받아
  // 그쪽으로 몰린다. 낮에 번 골드로 밤의 동선을 사는 것이다 (§6.5 행상).
  if(hit('KeyF') && N.baits>0){
    N.baits--;
    const vx=(N.dir>0?1:-1);                  // 화면 기준 좌우 -> 타일 좌표
    N.lure={x:Math.max(1,Math.min(MAP-1,N.p.x+vx*2.8)),
            y:Math.max(1,Math.min(MAP-1,N.p.y-vx*2.8)), t:LURE_T};
    say('미끼를 던졌습니다 — 좀비가 그쪽으로 갑니다.');
  }
  if(N.lure){
    N.lure.t-=dt;
    if(N.lure.t<=0) N.lure=null;
  }
  // 가방에서 버리기 — Z. 6칸이 선착순으로 차 버리면 '무엇을 가져갈까'가 사라진다.
  if(hit('KeyZ') && G.bag.length){
    const it=G.bag.pop(); bar();
    say('<b>'+it.label+'</b>을(를) 버렸습니다.');
  }
  // 보관 주머니 — Q로 가방 첫 칸을 옮긴다
  if(hit('KeyQ') && G.bag.length) toPouch(0);

  // 근접 공격 (§7.3). 뒤에서 치면 즉사 — 정면 난투보다 잠입이 유리해야 한다.
  N.swing=Math.max(0,N.swing-dt);
  if(hit('Space') && !N.open){
    N.swing=0.25; N.noise=Math.max(N.noise,NOISE.melee);
    let best=null, bd=1.3;
    N.zomb.forEach(z=>{ const d=dist(N.p,z); if(d<bd){ bd=d; best=z; } });
    if(best){
      // 나를 못 본 상태(추격 아님) = 뒤를 잡은 것으로 본다
      const backstab = best.chase<=0;
      best.hp = backstab ? 0 : best.hp-1;
      if(best.hp<=0){
        N.zomb.splice(N.zomb.indexOf(best),1);
        say(backstab?'<span class="ok">뒤에서 한 방 — 조용히 처리했습니다.</span>':'좀비를 쓰러뜨렸습니다.');
      } else { best.chase=best.T.forget; say('맞췄지만 살아 있습니다. 이쪽을 봤습니다.'); }
    }
  }

  // 검색 프로그레스 — 슬롯이 순차로 공개된다 (§7.2-2)
  if(N.open){
    const c=N.open;
    // 검색 중에는 소음이 발생한다. 단 가만히 보고만 있으면 조용하다.
    if(c.slots.some(s=>s.rev<1) || (c.slots[c.pick] && c.slots[c.pick].moving>0))
      N.noise=Math.max(N.noise,NOISE.search);
    // 슬롯 공개는 자동으로 계속된다 (§7.2-2)
    const nxt=c.slots.find(s=>s.rev<1);
    if(nxt) nxt.rev=Math.min(1,nxt.rev+dt/(c.time*searchMul()));

    // 가방으로 옮기는 건 플레이어가 고른 것만. 진행도는 슬롯별로 남아서
    // 중간에 다른 걸 골랐다 돌아와도 처음부터 다시 하지 않는다.
    const mv=c.slots[c.pick];
    if(mv && mv.rev>=1 && mv.item && !mv.taken){
      mv.moving=Math.min(1,mv.moving+dt/(0.55*searchMul()));
      if(mv.moving>=1){
        if(bagAdd(mv.item)){ mv.taken=true;
          say(mv.item.kind==='stock'?'원두를 챙겼습니다.':'<b>'+mv.item.label+'</b> 확보');
        } else { mv.moving=0; say('<span class="bad">가방이 가득 찼습니다.</span>'); }
        c.pick=-1;                                   // 하나 끝나면 다시 고르게 한다
      }
    }
    if(!nxt && !c.slots.some(s=>s.item && !s.taken)){ c.state='done'; N.open=null; say('다 털었습니다.'); }
  }

  // 습격 — E 홀드로 메뉴판을 베낀다. 원본은 남는다 (§7.3)
  let tgt=null;
  RIVAL.forEach((s,i)=>{ if(dist(N.p,{x:s[0],y:s[1]})<2.0) tgt=BOTS[i]; });
  N.target=tgt;
  if(tgt && held('KeyE') && !N.open){
    N.hold+=dt;
    if(N.hold>=2.6){
      N.hold=0;
      const ri=tgt.menu.slice().sort((a,b)=>RECIPES[b].m-RECIPES[a].m)[0];
      // 레시피는 **사본**을 가져오고 원본은 남는다(§7.3). 대신 상대의 생산 능력을 깎는다 —
      // 예전엔 골드만 뺏고 tier 를 올려 줘서, 습격할수록 상대가 강해지는 역인센티브였다.
      tgt.tier=Math.max(TIER_MIN, tgt.tier-RAID_TIER); tgt.knows=true;
      // 재료도 털어 온다 (§6.5 "재료는 원본이 사라진다"). 이게 없으면 습격은
      // 파밍을 **대체**하는데 보상은 파밍의 1/3이라 아무도 안 한다 — 실측 승률 0%.
      ME.stock+=RAID_BEANS;
      const hurtG=Math.round(RAID_TIER*35);
      if(ME.owned.indexOf(ri)<0 && bagAdd({kind:'recipe',id:ri,label:RECIPES[ri].n}))
        say('<span class="ok">'+esc(tgt.name)+'의 <b>'+RECIPES[ri].n+'</b> 사본을 베끼고 가게를 부쉈습니다 — 상대 일 매출 −'+hurtG+'G</span>');
      else say('<span class="ok">'+esc(tgt.name)+'의 가게를 부쉈습니다 — 상대 일 매출 −'+hurtG+'G</span>');
      hud();
    }
  } else N.hold=0;

  // 좀비 — 소리에 반응한다. 자기 건물 주변을 벗어나면 돌아간다
  N.zomb.forEach(z=>{
    const d=dist(N.p,z), T=z.T;
    // 감지는 둘 중 하나 — 소리(내 소음 반경 안) 또는 시야(제 시야 반경 안).
    // 좌표는 **감지되는 그 프레임에만** 찍힌다 (§9.2). 실시간 추격이면 소리를 내고
    // 옆으로 빠질 수가 없어서 Shift 가 순수 손해였다. 시야 안이면 매 프레임 갱신되므로
    // 눈앞에서는 예전과 똑같이 따라붙는다.
    // 소리는 전방향이지만 **눈은 전방 100도만** 본다 (§7.3). 이 둘이 갈라져야
    // 뒤로 돌아가는 것이 전략이 되고, 백스탭이 운이 아니라 계획이 된다.
    // 미끼는 소리다 — 전방향이고 벽을 넘는다. 반경은 뛰기(8)와 같게 잡는다.
    const ld = N.lure ? Math.hypot(N.lure.x-z.x, N.lure.y-z.y) : 1e9;
    if(ld < Math.min(NOISE.run, T.hear)){
      z.hx=N.lure.x; z.hy=N.lure.y; z.chase=T.forget; z.probe=0;
      const l=Math.max(.001,ld); z.fx=(N.lure.x-z.x)/l; z.fy=(N.lure.y-z.y)/l;
    }
    const heard = N.noise>0 && d<Math.min(N.noise,T.hear);
    // 시야는 벽에 막힌다. **소리는 안 막힌다** — 그래서 벽 뒤에 숨는 것과
    // 조용히 있는 것이 서로 다른 대응이 된다. 둘 다 안 막으면 벽이 장식이다.
    const seen  = d<T.sight && ((N.p.x-z.x)*z.fx + (N.p.y-z.y)*z.fy) >= d*FOV_COS
                  && losClear(z.x,z.y,N.p.x,N.p.y);
    if(heard || seen){
      z.hx=N.p.x; z.hy=N.p.y; z.chase=T.forget; z.probe=0;
      if(heard && !seen){ const l=Math.max(.001,d); z.fx=(N.p.x-z.x)/l; z.fy=(N.p.y-z.y)/l; }
    }
    z.chase=Math.max(0,z.chase-dt);
    z.cd=Math.max(0,z.cd-dt);
    if(z.probe>0){
      // 소리 난 자리에 도착했는데 아무도 없다 — 3초간 그 부근을 뒤진다
      z.probe-=dt; z.wt-=dt;
      if(z.wt<=0){ z.wt=.5+Math.random()*.6;
        const a=Math.random()*6.2832; z.vx=Math.cos(a)*.7; z.vy=Math.sin(a)*.7; }
      faceVel(z); step(z, z.vx*dt, z.vy*dt, .5, MAP-.5);
    } else if(z.chase>0){
      const hd=Math.hypot(z.hx-z.x,z.hy-z.y);
      if(hd<0.5){ z.probe=3; z.path=null; }
      else pathStep(z, z.hx, z.hy, T.spd, dt);         // 벽을 돌아서 온다
    } else {
      // 놓치면 스폰 지점으로 복귀하고, 도착하면 반경 4타일 안을 배회한다 (§9.2)
      const hd=Math.hypot(z.sx-z.x, z.sy-z.y);
      if(hd>4){
        pathStep(z, z.sx, z.sy, T.spd*0.6, dt);
      } else {
        z.wt-=dt;
        if(z.wt<=0){ z.wt=3+Math.random()*3;
          const a=Math.atan2(z.sy-z.y,z.sx-z.x)+(Math.random()-.5)*2.4;
          z.vx=Math.cos(a)*.7; z.vy=Math.sin(a)*.7; }
        faceVel(z); step(z, z.vx*dt, z.vy*dt, .5, MAP-.5);
      }
    }
    if(d<.8 && z.cd<=0){ z.cd=T.atk; hurt(1,z); }
  });

  // 루팅 UI 동기화 — N.open 이 바뀌는 경로가 여러 군데라 여기서 한 번에 맞춘다
  if(N.open){ if(LOOT.forId!==N.open) openLoot(N.open); else updateLoot(N.open); }
  else if(LOOT.forId) closeLoot();

  if(G.t<=0){
    const home=dist(N.p,{x:HOME[0],y:HOME[1]})<2.0;
    if(!home){
      say('<span class="bad">탈출 실패. 가방을 잃습니다 — 보관 주머니는 남습니다.</span>');
      G.bag=[];                                   // 주머니(ME.pouch)는 건드리지 않는다
    }
    endNight(home);
  }
}

// 좀비 시야 부채꼴. 아이소 평면에서 정확한 부채꼴을 그리려면 변환이 필요한데,
// **타일 좌표에서 부채꼴을 샘플해 각 점을 iso() 로 옮기면** 변환이 공짜로 따라온다.
// 추격 중이면 붉게, 아니면 흐릿하게 — 색 하나로 상태까지 읽힌다.
function fovCone(z){
  const dp=dist(N.p,z);
  if(dp>FL_R[0]/HW+2) return;                        // 손전등 밖은 안 그린다
  const R0=z.T.sight, half=FOV_DEG*Math.PI/360, a0=Math.atan2(z.fy,z.fx);
  const hot=z.chase>0, col=hot?R.blood[3]:R.toxic[1];
  const fade=Math.max(.35,1-dp/(FL_R[0]/HW+2));
  // 점을 흩뿌리지 않고 폴리곤 하나로 채운다. 점 샘플링은 반지름이 커질수록
  // 성기게 흩어져서 부채꼴이 아니라 먼지처럼 보였다. 아이소 변환은 꼭짓점마다
  // iso() 를 태우면 공짜로 따라온다.
  ctx.save();
  ctx.beginPath();
  const o=iso(z.x,z.y,0); ctx.moveTo(o[0],o[1]);
  for(let a=-half;a<=half+1e-6;a+=half/12){
    // 광선마다 벽에 부딪히는 데까지만 — 콘이 벽을 뚫으면 화면이 거짓말을 한다
    const ca=Math.cos(a0+a), sa=Math.sin(a0+a);
    let r=R0;
    for(let t=.5;t<R0;t+=.35) if(blocked(z.x+ca*t, z.y+sa*t)){ r=t; break; }
    const q=iso(z.x+ca*r, z.y+sa*r, 0);
    ctx.lineTo(q[0],q[1]);
  }
  ctx.closePath();
  ctx.globalAlpha=(hot?.13:.09)*fade; ctx.fillStyle=col; ctx.fill();
  ctx.globalAlpha=(hot?.85:.50)*fade; ctx.strokeStyle=col; ctx.lineWidth=1; ctx.stroke();
  ctx.restore();
}
function drawNight(t){
  ctx.fillStyle=R.dusk[0]; ctx.fillRect(0,0,W,H);
  ctx.save();
  if(N.shake>0) ctx.translate((Math.random()*5-2)|0,(Math.random()*5-2)|0);

  for(let ty=0;ty<MAP;ty++) for(let tx=0;tx<MAP;tx++){
    const p=iso(tx,ty,0);
    if(p[0]<-64||p[0]>W+64||p[1]<-64||p[1]>H+64) continue;
    const even=((tx+ty)&1)===0;
    const homeLit=Math.abs(tx+.5-HOME[0])+Math.abs(ty+.5-HOME[1])<3.2;
    const inB=BLDG.some(b=>inBldg(b,tx,ty));
    // 내 카페 주변만 난색 — 밤 화면에서 유일하게 따뜻한 곳
    const warm = homeLit ? .34 : 0;
    const dim  = homeLit ? 0 : (inB ? .18 : .40);
    if(!floorTile('tile_zone_floor',p[0],p[1],dim,!even,warm)){
      let col;
      if(homeLit) col = even?R.amber[2]:R.dusk[4];
      else if(inB) col = even?R.dusk[4]:R.dusk[3];
      else col = even?R.dusk[2]:R.dusk[1];
      diamond(ctx,p[0],p[1],64,col);
    }
  }
  // 정렬은 engine.js 의 DepthRenderer 가 한다. push 는 (깊이, 레이어, 그리기)를
  // 그대로 넘기는 얇은 껍데기 — 호출부 30여 곳의 시그니처를 유지하려고 남겼다.
  // 큐는 프레임마다 비우고 시작한다. 예전 `const q=[]` 는 매 프레임 새 배열이라
  // 공짜로 보장되던 건데, 공유 큐로 바꾸면서 중간에 예외가 나면 다음 프레임으로
  // 샌다. 여기서 비워야 그 성질이 유지된다.
  depthQ.clear();
  const push=(d,z,f)=>depthQ.pushDepth(d,z,f);

  // 건물 외벽 — 좀비 있는 건물은 폐허 벽, 빈 건물은 낮은 벽
  BLDG.forEach(b=>{
    for(let i=-1;i<b.w;i++){                              // i=-1 은 모서리 기둥 — WALLS 와 같아야 한다
      push(b.x+i+b.y-1,0,()=>{ const p=iso(b.x+i,b.y-1,0); box(ctx,p[0],p[1],64,b.rich?56:34,R.dusk); });
    }
    for(let j=0;j<b.h;j++){
      push(b.x-1+b.y+j,0,()=>{ const p=iso(b.x-1,b.y+j,0); box(ctx,p[0],p[1],64,b.rich?56:34,R.dusk); });
    }
  });
  // 내 카페 + 남의 카페
  push(HOME[0]+HOME[1],0,()=>{
    const p=iso(HOME[0],HOME[1],0);
    if(!drawSpr(BLDG_ASSET.home,p[0],p[1],false,'base')) box(ctx,p[0],p[1],64,36,R.wood);
    const m=iso(HOME[0],HOME[1],76); diamond(ctx,m[0],m[1],32,R.amber[5]);
    ctx.fillStyle=R.amber[5]; ctx.fillRect(m[0],m[1]-14,2,14);
  });
  RIVAL.forEach((s,i)=>{
    if(ME.found.indexOf(BOTS[i].name)<0) return;         // 발견 전에는 그리지 않는다
    push(s[0]+s[1],0,()=>{
      const p=iso(s[0],s[1],0);
      if(!drawSpr(BLDG_ASSET.rival,p[0],p[1],false,'base')) box(ctx,p[0],p[1],64,36,R.soot);
      const m=iso(s[0],s[1],76); diamond(ctx,m[0],m[1],32,R.blood[3]);
      ctx.fillStyle=R.blood[3]; ctx.fillRect(m[0],m[1]-14,2,14);
    });
  });
  // 컨테이너 + 근접 아웃라인 3상태 (§7.2-1)
  N.cont.forEach(c=>push(c.x+c.y,0,()=>{
    const p=iso(c.x,c.y,0), d=dist(N.p,c);
    const size=c.kind==='crate'?64:32;
    let h=c.kind==='corpse'?10:(c.kind==='random'?30:24);
    const CA=CONT_ASSET[c.kind];
    if(CA && spr(CA)){ drawSpr(CA,p[0],p[1],false,'base'); h=spr(CA).naturalHeight/2-HH; }
    else {
      box(ctx,p[0],p[1],size,h,c.kind==='corpse'?R.blood:(c.kind==='random'?R.steel:R.wood));
      if(c.kind==='random'){ metalSheen(ctx,p[0],p[1],size,h);
        dots(ctx,'?',p[0]-2,p[1]-h-9,R.toxic[1],2); }
      if(c.kind==='drawer') woodGrain(ctx,p[0],p[1],size,h);
    }
    if(d<OUTLINE_R){                                     // 손전등 반경 안에서만
      const col = contOutlineCol(c.state);
      const wpx = c.state==='done'?1:2;
      const blink = c.state==='busy' && (((t*8)|0)&1);
      if(!blink){
        ctx.save(); ctx.globalAlpha=Math.max(.25,1-d/OUTLINE_R);
        const half=size/2, rows=size/2, hh=rows/2;
        ctx.fillStyle=col;
        for(let r=0;r<rows;r++){
          const hw=(r<hh?(r+1):(rows-r))*2, y=p[1]-h-hh+r;
          ctx.fillRect(p[0]-hw,y,wpx,1); ctx.fillRect(p[0]+hw-wpx,y,wpx,1);
        }
        ctx.restore();
      }
    }
  }));
  N.zomb.forEach(z=>push(z.x+z.y,1000,()=>{
    const p=iso(z.x,z.y,0);
    actor(ctx,p[0],p[1],'zombie',((t*(z.chase>0?7:3))|0)&3,(z.x-z.y)<(N.p.x-N.p.y),null);
    // 추격자는 붉은 표식 — 속도가 2배라 미리 구분돼야 한다
    if(z.kind==='runner') dots(ctx,'R',p[0]-2,p[1]-CH_H-30,R.blood[3],1);
    if(z.chase>0) dots(ctx,'!',p[0]-2,p[1]-CH_H-18,R.blood[3],2);
  }));
  push(N.p.x+N.p.y,1000,()=>{
    const p=iso(N.p.x,N.p.y,0);
    const hurtFlash=N.inv>0 && ((t*14)|0)%2;
    actor(ctx,p[0],p[1],hurtFlash?'zombie':'raider',
          N.open?-1:(N.moving?((t*(N.run?12:7))|0)&3:-1),N.dir<0,null);
    if(N.open){                                          // 수색 자세 표시
      ctx.fillStyle=R.amber[5]; ctx.fillRect(p[0]-8,p[1]-CH_H-10,16,2);
    }
  });
  depthQ.flush();

  const pp=iso(N.p.x,N.p.y,0);
  if(N.noise>0) ring(ctx,pp[0],pp[1],N.noise,N.run?R.blood[3]:R.toxic[1],N.run?.5:.22,false);
  if(N.target && N.hold>0){
    const f=Math.min(1,N.hold/2.6);
    ctx.fillStyle=R.soot[0]; ctx.fillRect(240,H-30,160,8);
    ctx.fillStyle=R.blood[3]; ctx.fillRect(240,H-30,(160*f)|0,8);
  }
  ctx.restore();

  flashlight(pp[0],pp[1]-18);
  if(N.lure) ring(ctx,iso(N.lure.x,N.lure.y,0)[0],iso(N.lure.x,N.lure.y,0)[1],NOISE.run,R.toxic[1],.45,false);
  // 시야 콘은 **손전등 어둠 위에** 그린다. 깊이 큐에 넣었더니 어둠에 묻혀서 안 보였고,
  // 안 보이는 판정은 없느니만 못하다 — 백스탭이 계획이 아니라 운이 된다.
  // 반경 게이트가 이미 걸려 있어서 위에 그려도 먼 좀비 위치가 새지는 않는다.
  N.zomb.forEach(z=>fovCone(z,t));

  const hd=dist(N.p,{x:HOME[0],y:HOME[1]});
  if(hd<2.0 && !N.open){ dots(ctx,'E',pp[0]-2,pp[1]-CH_H-30,R.amber[5],2); }
  homeGuide(t,hd);
}
// 손전등 — 하드 컷 3단 계단 (아트컨셉 §7). 그라디언트도, destination-out도 쓰지 않는다.
// destination-out은 어둠만이 아니라 그 아래 씬까지 지워버린다.
const FL_R=[112,172,242], FL_A=['rgba(5,7,16,.42)','rgba(5,7,16,.76)','rgba(5,7,16,.94)'];
function flashlight(cx,cy){
  const B=N.flBoost||1;                    // 배터리를 사면 반경이 넓어진다 (§6.5 행상)
  const seg=(x0,x1,y,col)=>{ if(x1<=x0) return; ctx.fillStyle=col; ctx.fillRect(x0,y,x1-x0,1); };
  for(let y=0;y<H;y++){
    const dy=(y-cy)*2;                                  // 아이소 2:1 — 세로를 2배로 봐야 원이 된다
    const hw=r=>{ const t=r*r-dy*dy; return t<=0?0:(Math.sqrt(t)|0); };
    const w1=hw(FL_R[0]*B), w2=hw(FL_R[1]*B), w3=hw(FL_R[2]*B);
    seg(0, Math.max(0,cx-w3), y, FL_A[2]);
    seg(Math.max(0,cx-w3), Math.max(0,cx-w2), y, FL_A[1]);
    seg(Math.max(0,cx-w2), Math.max(0,cx-w1), y, FL_A[0]);
    seg(Math.min(W,cx+w1), Math.min(W,cx+w2), y, FL_A[0]);
    seg(Math.min(W,cx+w2), Math.min(W,cx+w3), y, FL_A[1]);
    seg(Math.min(W,cx+w3), W, y, FL_A[2]);
  }
}
/* ══ 루팅 UI — 타르코프식 2패널 (기획서 §7.2) ══════════════════════
   캔버스가 아니라 DOM/CSS다. 그리드·테두리·카운터는 CSS가 훨씬 싸고,
   열 때 한 번 만들고 매 프레임에는 진행 바 폭만 갱신한다. */
const LOOT = {el:null, box:null, bagCells:[], boxCells:[], forId:null};
const KIND_LABEL = {crate:'금속 컨테이너', drawer:'서랍장', random:'랜덤 박스', corpse:'시체'};

// 12×12 아이콘을 셀 안에 3배로 그린다 — 기존 icon() 재사용, 새 아트 없음
function cellIcon(kind){
  const cv2 = document.createElement('canvas');
  cv2.width = cv2.height = 36;
  const c2 = cv2.getContext('2d');
  c2.imageSmoothingEnabled = false;
  c2.setTransform(3,0,0,3,0,0);
  icon(c2, kind, 0, 0);
  return cv2;
}
// 아이템 -> 아이콘. 호출부가 각자 종류를 갈라 보면 종류를 하나 추가할 때마다
// 빠뜨린 곳에서 터진다 — 붕대(id 없음)를 넣자 실제로 그랬다. 여기 한 곳만 고친다.
function itemIcon(it){
  if(!it) return null;
  if(it.kind==='stock')   return beanIcon();
  if(it.kind==='bandage') return bandageIcon();
  return cellIcon(RECIPES[it.id].t);
}
function bandageIcon(){
  const cv2 = document.createElement('canvas');
  cv2.width = cv2.height = 36;
  const c2 = cv2.getContext('2d');
  c2.setTransform(3,0,0,3,0,0);
  c2.fillStyle = R.bone[3]; c2.fillRect(1,4,10,4);      // 흰 붕대 띠
  c2.fillStyle = R.bone[1]; c2.fillRect(1,4,10,1);
  c2.fillStyle = R.blood[3]; c2.fillRect(5,2,2,8);      // 붉은 십자
  c2.fillRect(3,5,6,2);
  return cv2;
}
function beanIcon(){
  const cv2 = document.createElement('canvas');
  cv2.width = cv2.height = 36;
  const c2 = cv2.getContext('2d');
  c2.setTransform(3,0,0,3,0,0);
  c2.fillStyle = R.wood[2]; c2.fillRect(2,3,8,7);
  c2.fillStyle = R.wood[3]; c2.fillRect(2,3,8,2);
  c2.fillStyle = R.amber[1]; c2.fillRect(4,6,4,3);
  return cv2;
}
function mkCell(cls, idx){
  const d = document.createElement('div');
  d.className = 'cell ' + cls;
  const bar = document.createElement('div');
  bar.className = 'bar';
  bar.appendChild(document.createElement('i'));
  d.appendChild(bar);
  if(idx !== undefined){                       // 컨테이너 슬롯 — 번호 + 클릭 선택
    const k = document.createElement('span');
    k.className = 'k'; k.textContent = idx + 1;
    d.appendChild(k);
    d.onclick = function(){ pickSlot(idx); };
  }
  return d;
}
// 고르기 — 공개됐고 아직 안 챙긴 아이템만
function pickSlot(i){
  const c = N.open;
  if(!c || !c.slots[i]) return;
  const s = c.slots[i];
  if(s.rev < 1 || !s.item || s.taken) return;
  if(G.bag.length >= bagCap()){ say('<span class="bad">가방이 가득 찼습니다.</span>'); return; }
  c.pick = (c.pick === i) ? -1 : i;             // 다시 누르면 취소
  updateLoot(c);
}
function openLoot(c){
  LOOT.el = LOOT.el || $('loot');
  LOOT.box = c; LOOT.forId = c;
  $('lootBoxTtl').textContent = KIND_LABEL[c.kind] || '컨테이너';
  const bag = $('lootBagGrid'), boxg = $('lootBoxGrid');
  bag.innerHTML = ''; boxg.innerHTML = '';
  LOOT.bagCells = []; LOOT.boxCells = [];
  for(let i=0;i<bagCap();i++){ const d=mkCell('empty'); bag.appendChild(d); LOOT.bagCells.push(d); }
  c.slots.forEach(function(_,i){ const d=mkCell('hidden',i); boxg.appendChild(d); LOOT.boxCells.push(d); });
  LOOT.el.hidden = false;
  updateLoot(c);
}
function closeLoot(){ if(LOOT.el){ LOOT.el.hidden = true; } LOOT.forId = null; }

function paint(cell, cls, node, barFrac, moving){
  cell.className = 'cell ' + cls + (moving ? ' moving' : '');
  // 아이콘/글자는 바뀔 때만 교체 (매 프레임 DOM 재생성 금지)
  const want = node ? node.tagName : (cls === 'hidden' ? '?' : '');
  if(cell.dataset.k !== cls + '|' + want){
    cell.dataset.k = cls + '|' + want;
    Array.prototype.slice.call(cell.childNodes).forEach(function(n){
      const cn = n.className;
      if(cn !== 'bar' && cn !== 'k') cell.removeChild(n);   // 진행바와 슬롯 번호는 남긴다
    });
    if(node) cell.insertBefore(node, cell.firstChild);
    else if(cls === 'hidden') cell.insertBefore(document.createTextNode('?'), cell.firstChild);
  }
  const i = cell.querySelector('.bar i');
  if(i) i.style.width = Math.round(Math.max(0, Math.min(1, barFrac || 0)) * 100) + '%';
}
function updateLoot(c){
  if(!LOOT.el || LOOT.el.hidden) return;
  // 내 가방
  const cap = bagCap();
  $('lootBagCap').textContent = G.bag.length + ' / ' + cap;
  $('lootBagCap').className = 'cap' + (G.bag.length >= cap ? ' full' : '');
  LOOT.bagCells.forEach(function(cell, i){
    const it = G.bag[i];
    if(!it) return paint(cell, 'empty', null, 0);
    paint(cell, 'item' + (it.kind==='recipe' && RECIPES[it.id].r==='전전' ? ' rare' : ''),
          itemIcon(it), 0);
  });
  // 컨테이너
  const left = c.slots.filter(function(s){ return s.item && !s.taken; }).length;
  $('lootBoxCap').textContent = left + ' / ' + c.slots.length;
  $('lootBoxCap').className = 'cap';
  c.slots.forEach(function(s, i){
    const cell = LOOT.boxCells[i];
    if(!cell) return;
    if(s.rev < 1) return paint(cell, 'hidden', null, s.rev);          // 아직 가려짐
    if(!s.item)   return paint(cell, 'empty', null, 0);               // 빈 통
    if(s.taken)   return paint(cell, 'taken', null, 0);
    const cls = 'item can'
      + (s.item.kind==='recipe' && RECIPES[s.item.id].r==='전전' ? ' rare' : '')
      + (c.pick === i ? ' pick' : '');
    paint(cell, cls, itemIcon(s.item),
          s.moving, c.pick === i);
  });
}

// 탈출 지점 안내 — 화면 밖이면 가장자리에 방향과 남은 거리를 띄운다.
// 시간 안에 못 돌아오면 가방을 전부 잃는다. 어디로 갈지 모르게 두면 그건 설계 결함이다.
function homeGuide(t,hd){
  const hp=iso(HOME[0],HOME[1],0), m=30;
  const on = hp[0]>m && hp[0]<W-m && hp[1]>m && hp[1]<H-m;
  if(on) return;                                    // 보이면 표식으로 충분하다
  const cx=W/2, cy=H/2;
  let dx=hp[0]-cx, dy=hp[1]-cy;
  const k=Math.max(Math.abs(dx)/(cx-m), Math.abs(dy)/(cy-m)) || 1;
  const x=(cx+dx/k)|0, y=(cy+dy/k)|0;
  // 남은 시간이 촉박하면 붉게 점멸한다
  const urgent = G.t<18;
  const col = (urgent && (((t*5)|0)&1)) ? R.blood[3] : R.amber[5];
  diamond(ctx,x,y,32,col);
  ctx.fillStyle=R.soot[0]; diamond(ctx,x,y,16,R.soot[0]);
  // 진행 방향 삼각형
  const a=Math.atan2(dy,dx);
  ctx.fillStyle=col;
  for(let i=0;i<9;i++) ctx.fillRect((x+Math.cos(a)*(10+i))|0,(y+Math.sin(a)*(10+i)/2)|0,2,2);
  const s=String(Math.round(hd));
  dots(ctx,s,(x-dotsW(s,1)/2)|0,y+12,col,1);        // 남은 타일 거리
}

/* ══════════════════ 페이즈 전환 ══════════════════ */
function endDay(){
  BOTS.forEach(b=>{ b.sales += 190+Math.random()*190+b.tier*95; });
  // 마지막 날에는 밤이 없다 — 낮이 끝나면 바로 최종 결산이다 (§4).
  // 마지막 밤에 턴 레시피는 팔 날이 없고, 그 밤은 1위를 린치하는 시간이 되기 때문.
  if(ME.day>=DAYS){
    G.phase='settle'; hud(); bar(); timerBar(1);
    settleUI(true,[],null,true);
    return;
  }
  G.phase='dusk'; hud(); bar(); timerBar(1);
  duskUI();
}
function duskUI(){
  const menuBtns=ME.owned.map(ri=>{
    const on=ME.menu.slice(0,menuCap()).indexOf(ri)>=0;
    return '<button class="chip" data-reg="'+ri+'" aria-pressed="'+on+'">'+RECIPES[ri].n
      +'<span class="r">'+RECIPES[ri].t.split('.')[1]+' x'+RECIPES[ri].m+'</span></button>';
  }).join('');
  const facBtns=FACIL.map(f=>{
    const has=ME.fac[f.id], can=ME.gold>=f.c;
    return '<button class="chip" data-fac="'+f.id+'"'+(has||!can?' disabled':'')
      +' aria-pressed="'+!!has+'" title="'+f.d+'">'+f.n
      +'<span class="c">'+(has?'보유':f.c+'G')+'</span></button>';
  }).join('');
  const known=ME.found.length;
  ov.innerHTML='<h2>DAY '+ME.day+' — 황혼</h2>'
   +'<p>오늘 <b>'+Math.round(G.daySales)+'G</b> 판매. 보유 <b>'+Math.round(ME.gold)+'G</b> · 단골 '+ME.regulars+' · 원두 '+ME.stock+'</p>'
   +'<div class="sec">메뉴판 — '+menuCap()+'칸. 등록한 것만 팔리고, 등록한 것만 털립니다</div>'
   +'<div class="chips">'+menuBtns+'</div>'
   +'<div class="sec">시설 투자 — 키울수록 매출도 동선도 늘어납니다</div><div class="chips">'+facBtns+'</div>'
   +'<div class="sec">행상 — 오늘 밤에 쓸 것을 삽니다. 남기면 사라집니다</div><div class="chips">'
     +WARES.map(function(w){
        const n=ME.bought.filter(function(x){return x===w.id;}).length;
        return '<button class="chip" data-ware="'+w.id+'"'+(ME.gold<w.c?' disabled':'')
          +' aria-pressed="'+(n>0)+'" title="'+w.d+'">'+w.n
          +'<span class="c">'+(n?'×'+n+' · ':'')+w.c+'G</span></button>';
      }).join('')
   +'</div>'
   +'<div class="sec">이사 — 내 위치를 아는 상대가 '+BOTS.filter(b=>b.knows).length+'명</div><div class="chips">'
     +'<button class="chip" id="moveBtn"'+(ME.gold<moveCost()?' disabled':'')
     +' title="상대가 알아낸 내 위치를 전부 무효화합니다. 그날 영업을 접는 값이 붙습니다.">'
     +'카페 이전<span class="c">'+moveCost()+'G</span></button>'
   +'</div>'
   +'<div class="sec">출격</div><div class="chips">'
     +'<button class="chip" data-s="farm" aria-pressed="'+(G.sortie==='farm')+'">좀비 구역 파밍<span class="c">중위험</span></button>'
     +'<button class="chip" data-s="raid" aria-pressed="'+(G.sortie==='raid')+'"'+(known?'':' disabled')+'>남의 카페 습격<span class="c">'+(known?'고위험':'위치 미상')+'</span></button>'
     +'<button class="chip" data-s="stay" aria-pressed="'+(G.sortie==='stay')+'">카페에 남는다<span class="c">무위험</span></button>'
   +'</div><button class="go" id="goNight">출격</button>';
  ov.hidden=false;
  ov.querySelectorAll('[data-reg]').forEach(b=>b.onclick=()=>{
    const ri=+b.dataset.reg, at=ME.menu.slice(0,menuCap()).indexOf(ri);
    if(at>=0) ME.menu[at]=-1;
    else { const e=ME.menu.slice(0,menuCap()).indexOf(-1);
            if(e<0){ say('<span class="bad">메뉴판이 가득 찼습니다.</span>'); return; } ME.menu[e]=ri; }
    duskUI();
  });
  ov.querySelectorAll('[data-fac]').forEach(b=>b.onclick=()=>{
    const f=FACIL.find(x=>x.id===b.dataset.fac);
    if(ME.fac[f.id]||ME.gold<f.c) return;
    ME.gold-=f.c; ME.fac[f.id]=true; say('<b>'+f.n+'</b> — '+f.d); hud(); duskUI();
  });
  ov.querySelectorAll('[data-ware]').forEach(b=>b.onclick=()=>{
    const w=WARES.find(x=>x.id===b.dataset.ware);
    if(ME.gold<w.c) return;
    ME.gold-=w.c; ME.bought.push(w.id);
    say('<b>'+w.n+'</b> — '+w.d); hud(); duskUI();
  });
  const mb=$('moveBtn');
  if(mb) mb.onclick=()=>{
    if(!relocate()) return;
    say('<b>카페를 옮겼습니다.</b> 상대가 알던 내 위치가 전부 무효가 됐습니다.');
    hud(); duskUI();
  };
  ov.querySelectorAll('[data-s]').forEach(b=>b.onclick=()=>{ G.sortie=b.dataset.s; duskUI(); });
  $('goNight').onclick=()=>{ if(G.sortie==='stay'){ G.bag=[]; endNight(true,true); } else startNight(); };
}
function endNight(extracted,stayed){
  closeLoot();
  const gained=[];
  // 보관 주머니는 탈출 성공 여부와 무관하게 항상 회수된다 (§7.5)
  // 회수 규칙을 한 곳에 둔다. 예전엔 두 루프가 각자 "stock 이 아니면 레시피"로 갈라서,
  // 붕대(id 가 없다)를 들고 나오면 ME.owned 에 undefined 가 들어가고 RECIPES[undefined]
  // 에서 터졌다. 종류를 명시적으로 본다.
  const collect=(x,tag)=>{
    if(x.kind==='stock'){ ME.stock+=(x.n||1); return; }
    if(x.kind!=='recipe') return;              // 붕대 등 밤 소모품은 안 넘어온다
    if(ME.owned.indexOf(x.id)<0){ ME.owned.push(x.id); gained.push(RECIPES[x.id].n+tag); }
  };
  ME.pouch.forEach(x=>collect(x,' (주머니)'));
  ME.pouch=[];
  if(extracted) G.bag.forEach(x=>collect(x,''));
  let raided=null;
  BOTS.forEach(b=>{
    if(ME.day<2 || stayed) return;
    if(!b.knows && Math.random()<.35) b.knows=true;
    if(!b.knows) return;
    if(Math.random()<RAID_P){
      // 골드가 아니라 **내 생산 능력**을 깎는다. 골드는 시설 몇 개 사고 나면 갈 곳이
      // 없어서 뺏겨도 안 아팠다. 좌석이 있으면 좌석부터, 없으면 등록 메뉴가 한 장 뜯긴다.
      if(ME.fac.seat){ ME.fac.seat=false; raided={n:b.name,what:'좌석'}; }
      else {
        const at=ME.menu.slice(0,menuCap()).findIndex(v=>v>=0);
        if(at>=0){ raided={n:b.name,what:RECIPES[ME.menu[at]].n+' 메뉴판'}; ME.menu[at]=-1; }
        else raided={n:b.name,what:null};
      }
      ME.regs.splice(0,2); ME.regulars=ME.regs.length;   // 가게가 털리면 단골도 떨어진다
      b.tier+=RAID_TIER;                            // 턴 쪽은 그만큼 강해진다
    }
  });
  G.phase='settle'; hud(); settleUI(extracted,gained,raided,stayed);
}
function settleUI(extracted,gained,raided,stayed){
  const rows=ranked().map((p,i)=>
    '<div class="row'+(p.me?' self':'')+'"><span class="rk">'+(i+1)+'</span><span>'+esc(p.name)
    +'</span><span class="dy">DAY '+ME.day+'</span><span class="sc">'+Math.round(p.sales)+'G</span></div>').join('');
  const last=ME.day>=DAYS;
  ov.innerHTML='<h2>'+(last?'최종 결산 — '+DAYS+'일차':'DAY '+ME.day+' — 결산')+'</h2>'
   +'<p>오늘 판매 <b>'+Math.round(G.daySales)+'G</b> · 누적 <b>'+Math.round(ME.sales)+'G</b> · 보유 <b>'+Math.round(ME.gold)+'G</b></p>'
   +(last?'<p>마지막 날에는 밤이 없습니다. 낮 판매로 승부가 끝났습니다.</p>'
     :stayed?'<p>카페에 남아 밤을 지켰습니다. 얻은 것도 잃은 것도 없습니다.</p>'
     :extracted?'<p>귀환 성공. '+(gained.length?'<b>'+gained.join(', ')+'</b> 확보.':'새 레시피는 없었습니다.')+'</p>'
     :'<p style="color:#D9463C">귀환 실패. 가방을 전부 잃었습니다.</p>')
   +(raided?'<p style="color:#D9463C"><b>'+esc(raided.n)+'</b>이(가) 내 카페를 부쉈습니다 — '
      +(raided.what?'<b>'+esc(raided.what)+'</b>을(를) 잃었습니다':'가져갈 게 없었습니다')
      +'. 단골도 떨어졌습니다.</p>':'')
   +'<div class="sec">순위 — 누적 판매 골드</div><div class="rank">'+rows+'</div>'
   +'<button class="go" id="goNext">'+(last?'다시 시작':'DAY '+(ME.day+1)+' 시작')+'</button>';
  ov.hidden=false;
  $('goNext').onclick=()=>{ if(last){ location.reload(); return; } ME.day++; startDay(); };
}

function bar(){
  const b=$('bar');
  if(G.phase==='day'){
    const it=D.carry;
    const held = !it ? '빈손'
      : RECIPES[it.r].n + (isDone(it) ? ' · 완성' : ' · 다음 ' + PROC[nextProc(it)].ok);
    const busy=COOK.filter(s=>D.st[s.id].state!=='idle').length;
    b.innerHTML='<div class="slot'+(it?' armed':'')+'"><span class="key">손</span><span class="nm">'+held+'</span></div>'
      +'<div class="slot'+(busy?' armed':'')+'"><span class="key">가동</span><span class="nm">'+busy+' / '+COOK.length+' 스테이션</span></div>'
      +'<div class="slot'+(D.served.length>=SERVE_CAP?' hot':' armed')+'"><span class="key">서빙대</span><span class="nm">'
        +D.served.length+' / '+SERVE_CAP+(D.served.length?' — '+D.served.map(function(c){return RECIPES[c.r].n;}).join(', '):'')+'</span></div>'
      +'<div class="slot'+(ME.stock?' armed':' empty')+'"><span class="key">원두</span><span class="nm">'+ME.stock+'</span></div>'
      +'<div class="slot'+(D.queue.length?' armed':' empty')+'"><span class="key">다음</span><span class="nm">'
        +(D.queue.length?D.queue.slice(0,3).map(function(t){return t.split('.')[1];}).join(' → '):'끝')+'</span></div>'
      +ME.menu.slice(0,menuCap()).map((ri,i)=>ri<0
        ? '<div class="slot empty"><span class="key">'+(i+1)+'</span><span class="nm">빈 메뉴판</span></div>'
        : '<div class="slot'+(i===D.recipe?' armed':'')+'"><span class="key">'+(i+1)+'</span><span class="nm">'
          +RECIPES[ri].n+'</span><span class="tg">'+(stepsOf(ri).map(k=>PROC[k].ok).join('→')||'즉시')+'</span></div>').join('');
    $('help').innerHTML='<span><kbd>WASD</kbd> 이동</span><span><kbd>E</kbd> 스테이션</span>'
      +'<span>'+ME.menu.slice(0,menuCap()).map((_,i)=>'<kbd>'+(i+1)+'</kbd>').join('')+' 만들 메뉴</span>'
      +'<span><kbd>Shift</kbd> 대시</span>'
      +'<span>서빙대는 <b>'+SERVE_CAP+'칸</b>. 빈손으로 <kbd>E</kbd> 하면 되가져옵니다</span>'
      +'<span>레시피마다 공정이 다릅니다. 그라인더·머신은 <b>2대</b>라 여러 잔을 겹칠 수 있습니다</span>';
  } else if(G.phase==='night'){
    b.innerHTML='<div class="slot armed"><span class="key">'+G.bag.length+'/'+bagCap()+'</span><span class="nm">'
      +(G.bag.length?G.bag.map(x=>x.label).join(' · '):'가방이 비어 있음')+'</span></div>'
      +(N.baits?'<div class="slot armed"><span class="key">미끼</span><span class="nm">'+N.baits+'개 — <b>F</b></span></div>':'')
      +'<div class="slot'+(N.hp<2?' hot':' armed')+'"><span class="key">HP</span><span class="nm">'
      +'■'.repeat(Math.max(0,N.hp))+'□'.repeat(Math.max(0,3-N.hp))+'</span></div>'
      +'<div class="slot'+(ME.pouch.length?' armed':'')+'"><span class="key">주머니</span><span class="nm">'
      +(ME.pouch.length?ME.pouch.map(x=>x.label).join(' · '):'비어 있음 — 죽어도 남습니다')
      +' ('+ME.pouch.length+'/'+POUCH_CAP+')</span></div>';
    $('help').innerHTML='<span><kbd>WASD</kbd> 이동</span><span><kbd>SHIFT</kbd> 달리기 · 소음↑</span>'
      +'<span><kbd>E</kbd> <b>홀드</b> 수색 / 습격 · <kbd>E</kbd> 탭 귀환</span>'
      +'<span><kbd>SPACE</kbd> 근접 — <b>뒤에서 치면 즉사</b></span>'
      +'<span><kbd>Q</kbd> 주머니에 넣기</span><span><kbd>Z</kbd> 버리기</span>'
      +'<span><kbd>R</kbd> 붕대(3초)</span><span><kbd>F</kbd> 미끼</span>'
      +'<span><b>수색 중에는 움직일 수 없습니다.</b></span>'
      +'<span>탈출 지점은 <b>내 카페(HOME)</b> — 화면 밖이면 가장자리에 방향과 거리가 뜹니다</span>';
  } else { b.innerHTML=''; $('help').innerHTML=''; }
}

/* ══════════════════ 루프 ══════════════════ */
let last=performance.now();
function loop(now){
  const dt=Math.min(.05,(now-last)/1000); last=now;
  const t=now/1000;
  if(G.phase==='day'){ updateDay(dt); drawDay(t); }
  else if(G.phase==='night'){ updateNight(dt); drawNight(t); }
  else drawDay(t);
  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);

function intro(){
  ov.innerHTML='<h2>BLOOD &amp; BEAN</h2>'
   +'<p>핵전쟁 이후, 살아남은 사람들에게 커피를 파는 <b>'+DAYS+'일</b>. 낮에는 카페를 운영하고, 밤에는 좀비 구역에서 레시피를 훔쳐옵니다. '
   +'<b>마지막 날에는 밤이 없습니다</b> — 낮 판매로 끝납니다.</p>'
   +'<p><b>승패는 하나뿐입니다 — 마지막 날 누적 판매 골드가 가장 높은 사람이 이깁니다.</b></p>'
   +'<div class="sec">낮 — 오버쿡</div><p>메뉴를 고르는 게 아니라 <b>좁은 주방을 뛰어다니는</b> 게임입니다. '
   +'<kbd>1</kbd><kbd>2</kbd><kbd>3</kbd>으로 만들 메뉴를 고르고 <kbd>WASD</kbd>로 이동, <kbd>E</kbd>로 조작. '
   +'<b>레시피마다 거치는 공정이 다릅니다</b> — 에스프레소는 분쇄→추출, 라떼는 마무리가 더 붙고, '
   +'콜드브루는 오래 걸리지만 타지 않습니다. <b>그라인더와 머신이 2대씩</b>이라 여러 잔을 동시에 굴려야 합니다. '
   +'다 된 걸 방치하면 <b>탑니다</b>.</p>'
   +'<div class="sec">밤 — 타르코프</div><p><kbd>E</kbd>로 컨테이너를 열면 내용물이 <b>가려진 채</b> 시작해 슬롯이 하나씩 공개됩니다. '
   +'<b>수색 중에는 움직일 수 없고</b>, 소음이 좀비를 부릅니다. 좀비는 <b>건물에만</b> 있고, 좋은 물자도 거기 있습니다. '
   +'시간 안에 <b>HOME</b>으로 못 돌아오면 가방을 전부 잃습니다.</p>'
   +'<button class="go" id="goStart">개업</button>'
   +'<button class="chip" id="goSkip" style="align-self:flex-start;margin-top:-4px">'
   +'낮을 건너뛰고 밤부터 보기<span class="c">DEBUG</span></button>';
  ov.hidden=false;
  $('goStart').onclick=()=>startDay();
  // 낮이 70초라 밤까지 가는 데 시간이 걸린다. 확인용 지름길.
  $('goSkip').onclick=()=>{ ov.hidden=true; ME.found=[BOTS[0].name]; startNight(); };
}
hud(); bar(); intro();

/* ══════════════════ 셀프 체크 ══════════════════ */
(function selftest(){
  const e=[];
  const cols=new Set(ALL);
  if(cols.size!==46) e.push('팔레트 '+cols.size+'색 (46 아님)');
  if(ALL.length!==46) e.push('램프 색 합 '+ALL.length);
  // 램프는 어두움 → 밝음 순이어야 한다 (§3 규칙 5)
  Object.keys(R).forEach(k=>{
    const lum=R[k].map(h=>parseInt(h.slice(1,3),16)+parseInt(h.slice(3,5),16)+parseInt(h.slice(5,7),16));
    for(let i=1;i<lum.length;i++) if(lum[i]<=lum[i-1]) e.push(k+' 램프 '+i+'단이 더 어두움');
  });
  // 모든 손님 태그에 아이콘이 있어야 한다 — 월드에 한글을 그리지 않으므로
  const probe=document.createElement('canvas'); probe.width=probe.height=16;
  const pc=probe.getContext('2d',{willReadFrequently:true});
  TAGS.forEach(tg=>{
    pc.clearRect(0,0,16,16); icon(pc,tg,0,0);
    const d=pc.getImageData(0,0,16,16).data;
    let n=0; for(let i=3;i<d.length;i+=4) if(d[i]) n++;
    if(n<20) e.push('아이콘 비어 있음: '+tg);
  });
  ['st.empty','st.busy','st.done','st.burnt'].forEach(k=>{
    pc.clearRect(0,0,16,16); icon(pc,k,0,0);
    const d=pc.getImageData(0,0,16,16).data;
    let n=0; for(let i=3;i<d.length;i+=4) if(d[i]) n++;
    if(n<10) e.push('아이콘 비어 있음: '+k);
  });
  // 다이아가 실제로 64×32 (2:1)
  const pb=document.createElement('canvas'); pb.width=pb.height=64;
  const bc=pb.getContext('2d',{willReadFrequently:true});
  diamond(bc,32,32,64,'#ffffff');
  const dd=bc.getImageData(0,0,64,64).data;
  let wide=0, rows=0;
  for(let y=0;y<64;y++){ let n=0; for(let x=0;x<64;x++) if(dd[(y*64+x)*4+3]) n++; if(n){rows++; if(n>wide)wide=n;} }
  if(wide!==64||rows!==32) e.push('다이아 '+wide+'x'+rows+' (64x32 아님)');
  // 캔버스가 640×360
  if(cv.width!==1280||cv.height!==720) e.push('캔버스 '+cv.width+'x'+cv.height);
  // 에셋을 file://에서 읽으면 메인 캔버스가 오염돼 getImageData가 막힌다.
  // 셀프체크는 아래처럼 별도 프로브 캔버스에서만 픽셀을 읽는다.
  // 에셋이 주입됐다면 이름이 매핑과 맞아야 한다 — 오타 나면 조용히 폴백돼서 안 보인다
  const known=assets.names;
  if(known.length){
    Object.keys(ASSET_OF).forEach(function(k){ if(known.indexOf(ASSET_OF[k])<0) e.push('에셋 없음: '+ASSET_OF[k]); });
    [CONT_ASSET,BLDG_ASSET].forEach(function(m){
      Object.keys(m).forEach(function(k){ if(known.indexOf(m[k])<0) e.push('에셋 없음: '+m[k]); });
    });
    Object.keys(ST_ASSET).forEach(function(ty){
      if(!ST_ASSET[ty].some(function(n){ return known.indexOf(n)>=0; }))
        e.push(ty+' 스테이션 에셋 없음'); });
    Object.keys(WALK_SET).forEach(function(k){
      var have=WALK_SET[k].filter(function(n){ return known.indexOf(n)>=0; }).length;
      if(have>0 && have<WALK_SET[k].length)
        e.push(k+' 걷기 프레임 '+have+'/'+WALK_SET[k].length+' — 일부만 있으면 재생 안 됨');
    });
  }
  // 오버쿡 체인이 끊기지 않았는지 — 스테이션 4종이 모두 있어야 한 잔이 나온다
  ['shelf','grind','brew','serve'].forEach(ty=>{ if(!ST.find(s=>s.type===ty)) e.push('스테이션 없음: '+ty); });
  // 오버쿡의 전제 = 병렬. 종류당 1대뿐이면 여러 잔을 동시에 못 굴린다.
  ['grind','brew'].forEach(ty=>{
    if(ST.filter(s=>s.type===ty).length<2) e.push(ty+' 스테이션이 1대뿐 — 병렬 불가');
  });
  // 모든 레시피의 공정이 실제 스테이션으로 존재해야 한다
  RECIPES.forEach(function(r){
    (STEPS[r.t]||[]).forEach(function(k){
      if(!PROC[k]) e.push(r.n+' 공정 정의 없음: '+k);
      else if(!ST.find(s=>s.type===k)) e.push(r.n+' 공정 스테이션 없음: '+k);
    });
  });
  // 방향키가 화면 기준으로 도는지. 회전이 빠지면 조작이 45도 어긋난다.
  [['ArrowRight',1,0],['ArrowLeft',-1,0],['ArrowUp',0,-1],['ArrowDown',0,1]].forEach(function(t){
    keys[t[0]]=true;
    var v=moveVec(), sdx=v[0]-v[1], sdy=v[0]+v[1];   // 타일 -> 화면
    keys[t[0]]=false;
    if(Math.sign(sdx)!==t[1]||Math.sign(sdy)!==t[2])
      e.push(t[0]+' 이동 방향 어긋남 ('+sdx+','+sdy+')');
  });
  // 좀비는 건물에만 스폰된다 (§7.2-4)
  if(!BLDG.some(b=>b.z>0)) e.push('좀비 스폰 건물이 없음');
  if(!BLDG.some(b=>b.z===0)) e.push('빈 건물이 없음');
  const el=$('selftest');
  el.textContent=e.length?('SELFTEST FAIL - '+e[0])
    :'SELFTEST OK - 1280x720 / '+assets.names.length+' ASSETS / '+ST.length+' STATIONS / OVERCOOKED+TARKOV';
  el.className=e.length?'bad':'';
  if(e.length) console.error(e);
})();
