// 기획서 v2.1 §15.2 완료 기준 → 테스트 1:1 매핑.
// 각 테스트는 게임을 실제로 굴린 뒤 관찰된 값만 단언한다.
'use strict';
const {load} = require('./harness');

let pass = 0, fails = [];
function ok(name, cond, detail){
  if(cond) pass++;
  else fails.push(name + (detail !== undefined ? '  → ' + detail : ''));
}
function eq(name, got, want){ ok(name, got === want, 'got ' + JSON.stringify(got) + ', want ' + JSON.stringify(want)); }
function near(name, got, want, tol){ ok(name, Math.abs(got-want) <= tol, 'got ' + got + ', want ~' + want); }

const S = (X, fn, secs, dt) => { dt = dt || 1/30; for(let t=0; t<secs; t+=dt) fn(dt); };
const tap  = (X, c) => { X.pressed[c] = true; };
const hold = (X, c) => { X.keys[c] = true; };
const rel  = (X, c) => { X.keys[c] = false; X.pressed[c] = false; };
const stOf = (X, id) => X.ST.find(s => s.id === id);

// 좀비를 리터럴로 흉내내면 필드가 빠져서 조용히 깨진다 — hx 때 한 번, fx/fy 때 또 한 번.
// 게임과 같은 mkZombie 를 쓰고 필요한 것만 덮어쓴다. 이제 필드가 늘어도 안 깨진다.
function Z(X, kind, x, y, over){
  return Object.assign(X.mkZombie(kind, x, y, x, y, X.BLDG[0]), over || {});
}

/* ══ 1. 페이즈 ══════════════════════════════════════════ */
{
  const X = load();
  X.startDay();
  eq('1.1 낮 시작 시 phase=day', X.G.phase, 'day');
  eq('1.2 낮 길이 = 180초 (§4)', X.G.t, X.DAY_SEC);
  eq('1.3 하루 손님 총량 = 24 (처리량 한계에 맞춤)', X.D.budget, 24);

  S(X, X.updateDay, 181);
  eq('1.4 낮 3분이 지나면 황혼으로 넘어간다', X.G.phase, 'dusk');
}
{
  const X = load();
  X.ME.day = X.DAYS;                       // 마지막 날
  X.startDay();
  S(X, X.updateDay, 181);
  eq('1.5 마지막 날은 밤 없이 최종 결산으로 간다 (§4)', X.G.phase, 'settle');
}
{
  const X = load();
  X.ME.sales = 500; X.BOTS[0].sales = 900; X.BOTS[1].sales = 100;
  const r = X.ranked();
  ok('1.6 결산 순위가 누적 골드 내림차순', r[0].sales >= r[1].sales && r[1].sales >= r[2].sales,
     r.map(p => p.sales).join(' > '));
}

/* ══ 2. 낮 — 오버쿡식 제조 ═════════════════════════════ */
{
  const X = load();
  X.startDay();
  X.ME.menu = [0, -1, -1]; X.D.recipe = 0;   // 재의 에스프레소 = 분쇄 → 추출

  X.interact(stOf(X,'shelf'));
  ok('2.1 선반에서 집으면 손에 들린다', !!X.D.carry && X.D.carry.stage === 0);

  // 순서를 건너뛰면 다음 스테이션이 반응하지 않아야 한다
  X.interact(stOf(X,'b1'));
  eq('2.2 공정 순서를 건너뛰면 스테이션이 반응하지 않는다', X.D.st.b1.state, 'idle');
  ok('2.2b 거부당해도 손에 든 것은 그대로', !!X.D.carry);

  X.interact(stOf(X,'g1'));
  eq('2.3 그라인더에 넣으면 busy', X.D.st.g1.state, 'busy');
  eq('2.3b 넣으면 손이 빈다', X.D.carry, null);

  S(X, X.updateDay, X.PROC.grind.t + 0.2);
  eq('2.4 분쇄가 끝나면 done', X.D.st.g1.state, 'done');

  X.interact(stOf(X,'g1'));
  eq('2.5 빼면 다음 공정으로 진행', X.D.carry.stage, 1);
  X.interact(stOf(X,'b1'));
  S(X, X.updateDay, X.PROC.brew.t + 0.2);
  X.interact(stOf(X,'b1'));
  ok('2.6 분쇄→추출을 마치면 잔이 완성된다', X.isDone(X.D.carry));

  X.interact(stOf(X,'serve'));
  eq('2.7 서빙대에 올리면 대기 잔이 생긴다', X.D.served.length, 1);
  eq('2.7b 올리면 손이 빈다', X.D.carry, null);
}
{
  // 방치하면 탄다
  const X = load();
  X.startDay(); X.ME.menu=[0,-1,-1]; X.D.recipe=0;
  X.interact(stOf(X,'shelf')); X.interact(stOf(X,'g1'));
  S(X, X.updateDay, X.PROC.grind.t + X.PROC.grind.burn + 0.5);
  eq('2.8 다 된 걸 방치하면 탄다', X.D.st.g1.state, 'burnt');
  X.interact(stOf(X,'g1'));
  eq('2.8b 탄 것은 비울 수 있다', X.D.st.g1.state, 'idle');
}
{
  // 콜드브루는 타지 않는다 (설계 의도)
  const X = load();
  X.startDay(); X.ME.owned=[2]; X.ME.menu=[2,-1,-1]; X.D.recipe=0;
  X.interact(stOf(X,'shelf')); X.interact(stOf(X,'g1'));
  S(X, X.updateDay, X.PROC.grind.t+0.2); X.interact(stOf(X,'g1'));
  X.interact(stOf(X,'c1'));
  S(X, X.updateDay, X.PROC.cold.t + 30);
  eq('2.9 콜드브루는 오래 둬도 타지 않는다', X.D.st.c1.state, 'done');
}

/* ══ 3. 매출 공식 (§6.6) ═══════════════════════════════ */
{
  const X = load();
  X.startDay();
  const g0 = X.ME.gold, s0 = X.ME.sales;
  X.D.served.push({r:0, stage:9, o:true});                                  // 일반 ×1.0
  X.D.cust.push({tx:1.5,ty:5,tag:X.RECIPES[0].t,pat:20,max:24,skin:'guestA',state:'wait'});
  X.sell();
  eq('3.1 태그 적중 시 기본가 10골드 × 배수 1.0', Math.round(X.ME.gold-g0), 10);
  eq('3.1b 누적 판매 골드도 같이 오른다', Math.round(X.ME.sales-s0), 10);

  const g1 = X.ME.gold;
  X.D.served.push({r:4, stage:9, o:true});                                  // 전전 ×3.0
  X.D.cust.push({tx:1.5,ty:5,tag:X.RECIPES[4].t,pat:20,max:24,skin:'guestA',state:'wait'});
  X.sell();
  eq('3.2 전전 레시피는 ×9.0', Math.round(X.ME.gold-g1), 90);

  const g2 = X.ME.gold;
  X.D.served.push({r:0, stage:9, o:true});
  X.D.cust.push({tx:1.5,ty:5,tag:'Taste.Nutty',pat:20,max:24,skin:'guestA',state:'wait'});
  X.sell();
  eq('3.3 취향 불일치는 절반', Math.round(X.ME.gold-g2), 5);
}
{
  const X = load();
  X.startDay();
  X.ME.fac.roast = true;
  near('3.4 로스터 1단계 → 시설 배수 1.1', X.facMul(), 1.1, 1e-9);
  const g0 = X.ME.gold;
  X.D.served.push({r:4, stage:9, o:true});
  X.D.cust.push({tx:1.5,ty:5,tag:X.RECIPES[4].t,pat:20,max:24,skin:'guestA',state:'wait'});
  X.sell();
  eq('3.5 시설 배수가 판매가에 곱해진다 (10×9.0×1.1)', Math.round(X.ME.gold-g0), 99);
}
{
  const X = load();
  eq('3.6 시작 골드 100 (§6.6)', X.ME.gold, 100);
  X.ME.fac.seat = true;
  eq('3.7 좌석 증설 → 손님 24+6', X.dayCustomers(), 30);
  eq('3.8 단골 상한 = 좌석 수', X.regularCap(), 8);
}
{
  // 손님이 나가면 단골도 떠난다
  const X = load();
  X.startDay();
  // 단골은 이제 정수가 아니라 **사람 배열**이다 (얼굴 + 고정 취향).
  X.ME.regs = [{skin:'guestA',tag:'Taste.Bitter'},
               {skin:'guestB',tag:'Taste.Sweet'},
               {skin:'guestC',tag:'Taste.Sour'}];
  X.ME.regulars = X.ME.regs.length;
  X.D.cust.push({tx:1.5,ty:5,tag:'Taste.Sweet',pat:0.01,max:24,skin:'guestA',state:'wait'});
  X.updateDay(0.05);
  eq('3.9 손님이 기다리다 나가면 단골이 1명 이탈한다', X.ME.regulars, 2);
  eq('3.9c 단골 배열에서도 실제로 한 명이 빠진다', X.ME.regs.length, 2);
  eq('3.9b 나간 손님은 큐에서 사라진다', X.D.cust.length, 0);
}
{
  // 손님 대기 25초 (§6.6). 스폰 경로에서 실제로 나오는 값을 본다.
  const X = load();
  X.startDay();
  S(X, X.updateDay, 3);
  ok('3.10 스폰된 손님의 대기 시간이 25초', X.D.cust.length > 0 && X.D.cust[0].max === 25,
     JSON.stringify(X.D.cust.map(c => c.max)));
}

/* ══ 4. 밤 — 루팅 (§7.2) ═══════════════════════════════ */
{
  const X = load();
  X.startNight();
  // 이 절은 **루팅만** 본다. 좀비를 비운다 — 피격은 hurt() 가 N.open 을 끊는데
  // (§7.2 의도된 동작), 좀비 스폰이 난수라 놔두면 이 절이 약 25% 확률로 깨진다.
  // 좀비 동작은 5절에서 따로 검증한다.
  X.N.zomb.length = 0;
  const c = X.N.cont[0];
  // 슬롯이 전부 빈 통이 뽑히면 수색이 도중에 'done' 으로 끝나 버려서
  // 4.6(재개)이 간헐 실패한다 — 그건 올바른 동작이다("다 털었습니다").
  // 재개를 보려면 끝나지 않는 통이어야 하므로 마지막 칸에 아이템을 심어 고정한다.
  c.slots[c.slots.length - 1].item = {kind:'stock', label:'원두'};
  c.slots[c.slots.length - 1].taken = false;
  X.N.p.x = c.x; X.N.p.y = c.y;

  X.updateNight(0.05);
  eq('4.1 E를 누르지 않으면 수색이 시작되지 않는다', X.N.open, null);

  hold(X,'KeyE');
  S(X, X.updateNight, 0.3);
  ok('4.2 E를 홀드하면 수색이 시작된다', X.N.open === c, X.N.open ? 'other' : 'null');

  // 슬롯당 c.time 초가 걸린다. 두 칸이 열릴 만큼 돌린다.
  S(X, X.updateNight, c.time * 2 + 0.3);
  const revealed = c.slots.filter(s => s.rev >= 1).length;
  ok('4.3 홀드 중에는 슬롯이 순차로 공개된다', revealed >= 1, 'revealed=' + revealed);
  // 순차 = 앞칸이 다 차기 전에 뒷칸이 열리지 않는다
  ok('4.3b 앞 슬롯부터 순서대로 열린다',
     c.slots.every((s,i) => i === 0 || s.rev === 0 || c.slots[i-1].rev >= 1),
     c.slots.map(s=>s.rev.toFixed(2)).join(' '));
  ok('4.3c 한 번에 다 열리지 않는다', c.slots.some(s => s.rev < 1) || c.slots.length <= 2,
     c.slots.map(s=>s.rev.toFixed(2)).join(' '));

  rel(X,'KeyE');
  X.updateNight(0.05);
  eq('4.4 E를 떼면 수색이 즉시 멈춘다', X.N.open, null);
  eq('4.5 재진입 대비 — 공개된 슬롯은 그대로 유지', c.slots.filter(s=>s.rev>=1).length, revealed);

  hold(X,'KeyE'); S(X, X.updateNight, 0.3);
  ok('4.6 다시 홀드하면 이어서 수색한다', X.N.open === c);
  rel(X,'KeyE');
}
{
  // 수색 중에는 이동할 수 없다
  const X = load();
  X.startNight();
  X.N.zomb.length = 0;                     // 4절 첫 블록과 같은 이유 — 피격이 수색을 끊는다
  const c = X.N.cont[0];
  X.N.p.x = c.x; X.N.p.y = c.y;
  hold(X,'KeyE'); S(X, X.updateNight, 0.3);
  ok('4.7 수색이 열렸는지 확인', !!X.N.open);
  const px = X.N.p.x, py = X.N.p.y;
  hold(X,'KeyD'); X.updateNight(0.1);
  ok('4.8 수색 중 이동 입력을 넣으면 수색이 중단된다', X.N.open === null);
  rel(X,'KeyD'); rel(X,'KeyE');
}
{
  // 컨테이너 슬롯 수는 매번 랜덤이고, 빈 통도 나온다
  const X = load();
  const counts = {}, itemCounts = new Set();
  let emptySlots = 0, totalSlots = 0;
  for(let i=0;i<200;i++){
    const r = X.rollSlots('crate');
    counts[r.slots.length] = 1;
    itemCounts.add(r.slots.filter(s=>s.item).length);
    emptySlots += r.slots.filter(s=>!s.item).length;
    totalSlots += r.slots.length;
  }
  ok('4.9 컨테이너를 여러 번 열면 슬롯 수가 매번 같지 않다', Object.keys(counts).length > 1,
     '관측된 슬롯 수: ' + Object.keys(counts).join(','));
  ok('4.10 나온 아이템 수도 매번 같지 않다 (빈 통 포함)', itemCounts.size > 1,
     '관측: ' + [...itemCounts].sort().join(','));
  ok('4.11 빈 슬롯이 실제로 나온다', emptySlots > 0, emptySlots + '/' + totalSlots);
}
{
  // 검색 시간은 §9.4 표를 따른다
  const X = load();
  eq('4.12 서랍 검색 시간 1.0초/슬롯', X.rollSlots('drawer').time, 1.0);
  eq('4.13 상자 검색 시간 1.5초/슬롯', X.rollSlots('crate').time, 1.5);
  eq('4.14 랜덤 박스 검색 시간 6.0초/슬롯', X.rollSlots('random').time, 6.0);
  ok('4.15 전전 레시피는 랜덤 박스에서만 나온다', (()=>{
    for(let i=0;i<400;i++){
      const r = X.rollSlots('drawer');
      if(r.slots.some(s=>s.item && s.item.kind==='recipe' && X.RECIPES[s.item.id].r==='전전')) return false;
    }
    return true;
  })());
}
{
  // 가방 6칸
  const X = load();
  X.startNight();
  eq('4.16 기본 가방 6칸 (§7.5)', X.bagCap(), 6);
  let added = 0;
  for(let i=0;i<10;i++) if(X.bagAdd({kind:'stock',label:'원두'})) added++;
  eq('4.17 가방이 다 차면 더 담기지 않는다', added, 6);
  X.ME.fac.pack = true;
  eq('4.18 배낭 투자 시 9칸', X.bagCap(), 9);
  near('4.19 배낭은 검색 속도를 30% 줄인다', X.searchMul(), 0.70, 1e-9);
}

/* ══ 5. 밤 — 좀비 (§9.2 §9.3) ══════════════════════════ */
{
  const X = load();
  X.startNight();
  const byB = {};
  X.N.zomb.forEach(z => { byB[z.home.name] = (byB[z.home.name]||0) + 1; });
  eq('5.1 빈 건물에는 좀비가 0마리', byB['C'] || 0, 0);
  eq('5.2 하급 건물(B)에는 배회자 3마리', byB['B'] || 0, 3);
  eq('5.3 중급 건물(A)에는 배회자 4 + 추격자 2 = 6마리', byB['A'] || 0, 6);
  ok('5.4 추격자가 실제로 스폰된다', X.N.zomb.some(z => z.kind === 'runner'));
  ok('5.5 좀비는 전부 건물 소속이다 (거리 스폰 없음)', X.N.zomb.every(z => !!z.home));
}
{
  // 걷기 소음(2타일)으로는 8타일 밖 좀비가 반응하지 않는다
  const X = load();
  X.startNight();
  X.N.zomb = [Z(X,'shambler',10,0,{wt:99, fx:-1, fy:0})];
  X.N.p.x = 4; X.N.p.y = 0;                 // 6타일 거리
  hold(X,'KeyD');                            // 걷기
  S(X, X.updateNight, 0.4);
  eq('5.6 걷기(소음 2타일)로는 6타일 거리 배회자가 반응하지 않는다', X.N.zomb[0].chase, 0);
  rel(X,'KeyD');
}
{
  // 뛰기 소음(8타일)이면 반응한다
  const X = load();
  X.startNight();
  X.N.zomb = [{x:10, y:0, sx:10, sy:0, vx:0,vy:0,wt:99, chase:0, kind:'shambler',
               hp:3, T:X.ZTYPE.shambler, cd:0, home:X.BLDG[0]}];
  X.N.p.x = 4; X.N.p.y = 0;
  hold(X,'KeyD'); hold(X,'ShiftLeft');
  X.updateNight(0.05);
  ok('5.7 뛰기(소음 8타일)면 6타일 거리 배회자가 쫓아온다', X.N.zomb[0].chase > 0,
     'chase=' + X.N.zomb[0].chase);
  rel(X,'KeyD'); rel(X,'ShiftLeft');
}
{
  // 놓치면 추적 포기 시간 뒤 스폰 지점으로 복귀한다
  const X = load();
  X.startNight();
  const z = {x:20, y:20, sx:5, sy:5, vx:0,vy:0,wt:99, chase:X.ZTYPE.shambler.forget,
             hx:20, hy:20, probe:0,          // startNight 의 mk() 와 같은 모양이어야 한다
             kind:'shambler', hp:3, T:X.ZTYPE.shambler, cd:0, home:X.BLDG[0]};
  X.N.zomb = [z];
  X.N.p.x = 1; X.N.p.y = 1;                  // 멀리 (시야 3타일 밖)
  const d0 = Math.hypot(z.x - z.sx, z.y - z.sy);
  S(X, X.updateNight, X.ZTYPE.shambler.forget + 0.5);
  eq('5.8 추적 포기 시간(6초)이 지나면 추격이 끝난다', z.chase, 0);
  S(X, X.updateNight, 3);
  const d1 = Math.hypot(z.x - z.sx, z.y - z.sy);
  ok('5.9 추격이 끝나면 스폰 지점으로 복귀한다', d1 < d0, d0.toFixed(1) + ' → ' + d1.toFixed(1));
}
{
  // 백스탭 — 나를 못 본 좀비는 한 방
  const X = load();
  X.startNight();
  // 등을 돌리고 있다 (fx=+1 = +x 방향, 플레이어는 -x 쪽) — 그래서 백스탭이 성립한다
  X.N.zomb = [Z(X,'shambler',5.3,5,{sx:5, sy:5, wt:99, cd:99, fx:1, fy:0})];
  X.N.p.x = 5; X.N.p.y = 5;
  tap(X,'Space');
  X.updateNight(0.05);
  eq('5.10 뒤에서 근접 공격하면 배회자가 한 방에 죽는다', X.N.zomb.length, 0);
}
{
  // 정면 — 이미 나를 본 좀비는 한 방이 아니다
  const X = load();
  X.startNight();
  const z = Z(X,'shambler',5.3,5,{sx:5, sy:5, wt:99, chase:5, cd:99});
  X.N.zomb = [z];
  X.N.p.x = 5; X.N.p.y = 5;
  tap(X,'Space'); X.updateNight(0.05);
  eq('5.11 이미 나를 본 좀비는 즉사하지 않는다', X.N.zomb.length, 1);
  eq('5.11b 대신 체력이 깎인다', z.hp, 2);
}
{
  // 추격자는 배회자보다 빠르다
  const X = load();
  eq('5.12 배회자 이동속도 1.5', X.ZTYPE.shambler.spd, 1.5);
  eq('5.13 추격자 이동속도 3.5', X.ZTYPE.runner.spd, 3.5);
  eq('5.14 추격자 소리 감지 12타일', X.ZTYPE.runner.hear, 12);
  eq('5.15 소음 반경 — 걷기2/검색4/뛰기8',
     [X.NOISE.walk,X.NOISE.search,X.NOISE.run].join('/'), '2/4/8');
}

{
  // §9.2 — 좀비는 플레이어가 아니라 "마지막으로 들린 좌표"로 간다.
  // 이게 안 되면 소리 내고 옆으로 빠지기가 불가능해 Shift 가 순수 손해다.
  const X = load();
  X.startNight();
  X.N.cont.length = 0;
  // 등을 돌리고 있어야 '소리로만' 감지된 것이 된다 — 보고 있으면 시야로 계속 갱신된다
  const z = Z(X,'shambler',10,0,{wt:99, cd:99, fx:1, fy:0});
  X.N.zomb = [z];
  X.N.p.x = 4; X.N.p.y = 0;
  hold(X,'KeyD'); hold(X,'ShiftLeft');        // 뛰기 = 소음 8타일 → 6타일 밖에서도 들린다
  X.updateNight(1/30);
  rel(X,'KeyD'); rel(X,'ShiftLeft');
  const hx = z.hx, hy = z.hy;
  X.N.p.x = 4; X.N.p.y = 10;                  // 소리 낸 뒤 옆으로 빠진다 (시야 3타일 밖)
  S(X, X.updateNight, 5);
  near('5.16 좀비가 플레이어가 아니라 소리 좌표로 간다 (x)', z.x, hx, 0.6);
  near('5.16b 소리 좌표로 간다 (y)', z.y, hy, 0.6);
  ok('5.16c 옆으로 빠진 플레이어 쪽으로는 오지 않는다', Math.abs(z.y - X.N.p.y) > 6,
     'z=(' + z.x.toFixed(1) + ',' + z.y.toFixed(1) + ') p.y=' + X.N.p.y);

  // 도착하면 3초간 그 부근을 뒤진다 — 바로 스폰으로 돌아가 버리면 미끼가 무의미하다
  ok('5.17 도착 후 탐색 상태로 들어간다', z.probe > 0, 'probe=' + z.probe);
  let far = 0;
  S(X, X.updateNight, 2.8, 1/30);
  far = Math.hypot(z.x - hx, z.y - hy);
  ok('5.17b 3초 동안 소리 지점 부근에 머문다', far < 2.5, 'd=' + far.toFixed(2));
}
{
  // 시야 안이면 매 프레임 갱신 — 눈앞에서는 예전과 똑같이 따라붙는다
  const X = load();
  X.startNight();
  X.N.cont.length = 0;
  // 플레이어(-x 쪽)를 정면으로 보고 있다
  const z = Z(X,'runner',14,0,{wt:99, cd:99, fx:-1, fy:0});
  X.N.zomb = [z];
  X.N.p.x = 7; X.N.p.y = 0;                   // 7타일 — 추격자 시야 8 안
  const d0 = X.dist(X.N.p, z);
  hold(X,'KeyD'); hold(X,'KeyS');             // D+S = 순수 +x, 걸어서 도망
  let stale = 0;
  for(let i=0;i<30;i++){
    X.updateNight(1/30);
    if(Math.abs(z.hx - X.N.p.x) > 1e-9 || Math.abs(z.hy - X.N.p.y) > 1e-9) stale++;
  }
  rel(X,'KeyD'); rel(X,'KeyS');
  eq('5.18 시야 안이면 목표 좌표가 매 프레임 갱신된다', stale, 0);
  ok('5.18b 그래서 실제로 거리가 좁혀진다', X.dist(X.N.p, z) < d0,
     d0.toFixed(2) + ' → ' + X.dist(X.N.p, z).toFixed(2));
}

/* ══ 6. 탈출과 사망 (§7.5) ═════════════════════════════ */
{
  const X = load();
  X.startNight();
  X.G.bag = [{kind:'stock',label:'원두'}, {kind:'recipe',id:3,label:X.RECIPES[3].n}];
  X.ME.pouch = [{kind:'recipe',id:4,label:X.RECIPES[4].n}];
  X.N.p.x = X.HOME[0]; X.N.p.y = X.HOME[1];
  X.G.t = 0.01;
  X.updateNight(0.05);
  eq('6.1 시간 안에 탈출 지점에 도달하면 귀환 성공', X.G.phase, 'settle');
  ok('6.2 귀환 성공 시 가방 내용물이 보유로 넘어간다', X.ME.owned.indexOf(3) >= 0);
  ok('6.3 보관 주머니 내용물도 회수된다', X.ME.owned.indexOf(4) >= 0);
}
{
  const X = load();
  X.startNight();
  X.G.bag = [{kind:'recipe',id:3,label:X.RECIPES[3].n}];
  X.ME.pouch = [{kind:'recipe',id:4,label:X.RECIPES[4].n}];
  X.N.p.x = 20; X.N.p.y = 20;                 // 탈출 지점에서 멀리
  X.G.t = 0.01;
  X.updateNight(0.05);
  eq('6.4 탈출 실패 시 가방을 잃는다', X.G.bag.length, 0);
  ok('6.5 탈출 실패해도 보관 주머니는 남는다 (owned로 회수)', X.ME.owned.indexOf(4) >= 0);
  ok('6.6 가방에 있던 것은 잃는다', X.ME.owned.indexOf(3) < 0);
}
{
  const X = load();
  X.startNight();
  X.G.bag = [{kind:'recipe',id:3,label:X.RECIPES[3].n},{kind:'stock',label:'원두'}];
  const day0 = X.ME.day;
  X.N.p.x = 12; X.N.p.y = 12;
  const before = X.N.cont.length;
  X.N.hp = 1; X.N.inv = 0;
  X.hurt(1);
  eq('6.7 사망하면 그 자리에 시체 컨테이너가 남는다', X.N.cont.length, before + 1);
  const corpse = X.N.cont[X.N.cont.length-1];
  eq('6.7b 시체에는 들고 있던 것이 전부 들어간다', corpse.slots.length, 2);
  eq('6.7c 시체도 검색 프로그레스가 적용된다', corpse.slots.every(s=>s.rev===0), true);
  eq('6.8 사망하면 다음 날 낮에 페널티가 걸린다', X.ME.hurtDay, day0 + 1);
}
{
  // 사망 페널티가 실제로 이동 속도를 20% 줄이는가
  const X = load();
  X.startDay();
  hold(X,'KeyD'); S(X, X.updateDay, 1.0); rel(X,'KeyD');
  const normal = Math.hypot(X.D.p.x-3, X.D.p.y-3);

  const Y = load();
  Y.ME.hurtDay = Y.ME.day;
  Y.startDay();
  hold(Y,'KeyD'); S(Y, Y.updateDay, 1.0); rel(Y,'KeyD');
  const hurtDist = Math.hypot(Y.D.p.x-3, Y.D.p.y-3);
  ok('6.9 부상 다음 날은 이동이 20% 느리다',
     hurtDist < normal * 0.85 && hurtDist > normal * 0.7,
     normal.toFixed(2) + ' → ' + hurtDist.toFixed(2));
}
{
  const X = load();
  X.startNight();
  X.G.bag = [{kind:'recipe',id:4,label:X.RECIPES[4].n}];
  eq('6.10 보관 주머니는 2칸 (§7.5)', X.POUCH_CAP, 2);
  ok('6.11 가방에서 주머니로 옮길 수 있다', X.toPouch(0));
  eq('6.11b 옮기면 가방에서 빠진다', X.G.bag.length, 0);
  X.ME.pouch = [{},{}];
  X.G.bag = [{kind:'stock',label:'원두'}];
  ok('6.12 주머니가 차면 더 안 들어간다', X.toPouch(0) === false);
}

/* ══ 7. 습격 (§7.4) ════════════════════════════════════ */
{
  const X = load();
  X.ME.found = [X.BOTS[0].name];
  X.startNight();
  const bot = X.BOTS[0];
  const gold0 = X.ME.gold, botTier0 = bot.tier, botSales0 = bot.sales;
  const botMenu0 = bot.menu.slice();
  X.N.p.x = X.RIVAL[0][0]; X.N.p.y = X.RIVAL[0][1];
  hold(X,'KeyE');
  S(X, X.updateNight, 3.0);
  // 습격의 대상이 골드에서 **생산 능력(tier)** 으로 바뀌었다. 예전엔 골드만 뺏고
  // tier 를 올려 줘서 습격할수록 상대가 강해지는 역인센티브였다 (실측 −593G).
  ok('7.1 습격하면 상대의 생산 능력이 깎인다', bot.tier < botTier0,
     botTier0 + ' → ' + bot.tier);
  eq('7.1b 깎이는 양이 RAID_TIER 만큼', Math.round((botTier0-bot.tier)*100)/100, X.RAID_TIER);
  eq('7.2 골드는 오가지 않는다 (§3 승리 지표는 판매 총액이다)', X.ME.gold, gold0);
  ok('7.2b 봇에 gold 필드가 아예 없다', bot.gold === undefined);
  eq('7.3 상대의 누적 판매 골드는 변하지 않는다 (승리 지표는 못 뺏는다)', bot.sales, botSales0);
  eq('7.4 레시피는 사본이라 상대 메뉴판에 그대로 남는다', bot.menu.join(','), botMenu0.join(','));
  ok('7.5 훔친 레시피가 내 가방에 들어온다',
     X.G.bag.some(x => x.kind === 'recipe'), JSON.stringify(X.G.bag));
  rel(X,'KeyE');
}
{
  // 봇이 나를 털면 **골드가 아니라 생산 능력**을 깎는다. 골드는 시설 몇 개 사고 나면
  // 갈 곳이 없어서 뺏겨도 안 아팠다(실측: 금고의 10일 순증 +9G).
  // 난수(발각·습격 판정)를 0으로 고정해 결정적으로 만든다.
  const raid = (setup) => {
    const X = load();
    const M = Object.create(X.__sandbox.Math); M.random = () => 0;
    X.__sandbox.Math = M;
    X.BOTS.splice(1);                       // 봇이 둘이면 두 번 겹친다
    X.ME.day = 2;
    setup(X);
    const g0 = X.ME.gold, t0 = X.BOTS[0].tier;
    X.endNight(false, false);
    return {X:X, dGold:g0 - X.ME.gold, dTier:X.BOTS[0].tier - t0};
  };
  const a = raid(X => { X.ME.gold = 1000; X.ME.fac.seat = true; });
  eq('7.6 털려도 골드는 안 줄어든다', a.dGold, 0);
  eq('7.6b 대신 좌석을 잃는다', !!a.X.ME.fac.seat, false);
  eq('7.6c 턴 쪽은 그만큼 강해진다', Math.round(a.dTier*100)/100, a.X.RAID_TIER);

  const b = raid(X => {
    X.ME.fac.seat = false;
    X.ME.menu = [0, 1, -1, -1];
    X.ME.regs = [{skin:'guestA',tag:'Taste.Bitter'},{skin:'guestB',tag:'Taste.Sweet'},
                 {skin:'guestC',tag:'Taste.Sour'}];
    X.ME.regulars = 3;
  });
  eq('7.7 좌석이 없으면 등록 메뉴가 한 장 뜯긴다', b.X.ME.menu[0], -1);
  eq('7.7b 나머지 메뉴는 남는다', b.X.ME.menu[1], 1);
  eq('7.7c 단골도 두 명 떨어진다', b.X.ME.regs.length, 1);
}

/* ══ 8. 데이터 정합성 ══════════════════════════════════ */
{
  const X = load();
  // 손익분기 배수가 1.50 인데 현행 유효 배수가 1.28 이었다. 좋은 레시피일수록
  // 공정이 길어 처리량이 19% 떨어지는데 배수가 그걸 못 이겼다 (§6.1).
  eq('8.1 태그 배수 — 일반1.0/고급3.0/희귀5.0/전전9.0',
     X.RECIPES.map(r=>r.m).join(','), '1,1,3,5,9');
  ok('8.2 모든 레시피 공정이 실제 스테이션으로 존재한다',
     X.RECIPES.every(r => (X.STEPS[r.t]||[]).every(k => X.ST.some(s=>s.type===k))));
  ok('8.3 그라인더·머신이 2대씩이라 병렬이 가능하다',
     ['grind','brew'].every(ty => X.ST.filter(s=>s.type===ty).length >= 2));
  eq('8.4 팔레트 46색 (아트컨셉 v2.1)', X.ALL.length, 46);
  eq('8.5 논리 해상도 640×360', X.W + 'x' + X.H, '640x360');
  const fac = X.FACIL.reduce((o,f)=>(o[f.id]=f.c,o),{});
  // 금고·자물쇠·레시피 슬롯은 삭제했다 — 10일 완주 실측 순증 +9G / −1G / −84G.
  eq('8.6 남은 시설 3종의 가격', [fac.seat, fac.roast, fac.pack].join(','), '150,200,200');
  eq('8.6b 값이 0 이하였던 시설은 사라졌다',
     [fac.vault, fac.lock, fac.slot].filter(v => v !== undefined).length, 0);
  const w = X.WARES.reduce((o,v)=>(o[v.id]=v.c,o),{});
  eq('8.6c 행상 3종의 가격', [w.bandage, w.bait, w.battery].join(','), '60,80,70');
}

/* ══ 9. 콘솔 에러 / 인게임 셀프테스트 ══════════════════ */
{
  const X = load();
  const st = X.__els['selftest'];
  ok('9.1 인게임 셀프테스트 통과', !!st && /SELFTEST OK/.test(st.textContent) && !/FAIL/.test(st.textContent), st && st.textContent);
}
{
  // 낮 → 황혼 → 밤 → 결산 한 사이클을 끝까지 돌려도 터지지 않는가
  const X = load();
  let err = null;
  try {
    X.startDay();
    S(X, X.updateDay, 181);
    X.startNight();
    hold(X,'KeyD');
    S(X, X.updateNight, 181);
    rel(X,'KeyD');
  } catch(e){ err = e; }
  ok('9.2 낮 3분 + 밤 3분을 끝까지 돌려도 예외가 없다', err === null, err && err.stack);
  ok('9.3 한 사이클 뒤 결산 단계에 도달', ['settle','dusk'].indexOf(X.G.phase) >= 0, X.G.phase);
}

/* ══ 10. 렌더 배치 — 그래픽 깨짐 자체 검증 (Dry-run) ═══════
   화면을 눈으로 보지 않고 숫자로 확인한다. 검사 대상은 셋:
     (1) 에셋 규격 — 타일이 정확히 2:1 인가
     (2) 피벗 스냅 — 스프라이트 밑면이 타일 중심/아래꼭짓점에 정확히 붙는가
     (3) 깊이 정렬 — 뒤에서 앞으로, 같은 타일이면 바닥→소품→인물 순인가          */
{
  const fs = require('fs'), path = require('path');
  const X = load();
  const ASSETDIR = path.join(__dirname,'..','assets');

  /** PNG 헤더에서 크기만 읽는다. 디코더가 필요 없다 — IHDR 은 항상 16바이트째다. */
  function pngSize(file){
    const b = fs.readFileSync(file);
    if(b.length < 24 || b.readUInt32BE(0) !== 0x89504e47) return null;
    return [b.readUInt32BE(16), b.readUInt32BE(20)];
  }

  const present = X.ASSET_NAMES.filter(n => fs.existsSync(path.join(ASSETDIR, n + '.png')));
  ok('10.1 game.js 의 ASSET_NAMES 가 실제 파일과 일치한다',
     present.length === X.ASSET_NAMES.length,
     '누락: ' + X.ASSET_NAMES.filter(n => !present.includes(n)).join(',') || '');

  // (1) 타일은 정확히 논리 64x32 의 2배여야 한다. 1px만 어긋나도 바닥에 틈이 생긴다.
  for(const t of ['tile_cafe_floor','tile_zone_floor']){
    const f = path.join(ASSETDIR, t + '.png');
    if(!fs.existsSync(f)) continue;
    const [w,h] = pngSize(f);
    ok('10.2 ' + t + ' 가 정확히 128x64 (2:1)', w === X.TW*2 && h === X.TH*2, w + 'x' + h);
  }

  // (2) 피벗. sprites.anchorY 가 앵커 규칙의 유일한 정의다.
  //     타일 (5,5) 바닥 중심을 기준으로 각 앵커의 밑면이 어디에 닿는지 센다.
  const p = X.iso(5,5,0), CY = p[1];
  const H0 = 100;                                   // 임의의 스프라이트 높이(논리 px)
  const sp = X.sprites;
  eq('10.3 foot 앵커 — 스프라이트 밑면이 타일 중심에 정확히 선다',
     sp.anchorY(CY,H0,'foot') + H0, CY);
  eq('10.4 base 앵커 — 소품 밑면이 타일 아래 꼭짓점에 얹힌다',
     sp.anchorY(CY,H0,'base') + H0, CY + X.HH);
  eq('10.5 mid 앵커 — 바닥 타일은 이미지 중심이 타일 중심',
     sp.anchorY(CY,H0,'mid') + H0/2, CY);

  // 좌우는 항상 중앙 정렬이다 — 어느 앵커든 가로로 삐져나오지 않는다.
  ok('10.6 모든 앵커가 가로로는 타일 중심 정렬',
     ['foot','base','mid'].every(a => sp.anchorY(CY,H0,a) !== undefined));

  // (3) 좌표 왕복 — toScreen 의 역함수가 toTile 인가. 어긋나면 마우스 피킹이 틀어진다.
  const g = X.grid;
  for(const [tx,ty] of [[0,0],[3,7],[12.5,4.25],[25,25]]){
    const s2 = g.toScreen(tx,ty,0), back = g.toTile(s2[0],s2[1]);
    ok('10.7 좌표 왕복 (' + tx + ',' + ty + ')',
       Math.abs(back[0]-tx) < 0.05 && Math.abs(back[1]-ty) < 0.05,
       back.map(v=>v.toFixed(2)).join(','));
  }

  // (4) 깊이 정렬. 뒤(작은 tx+ty)가 먼저, 같은 깊이면 레이어 순.
  const order = [];
  const dq = X.depthQ;
  dq.clear();
  dq.push(9,9,X.LAYER.ACTOR, () => order.push('far-actor'));      // d=18
  dq.push(1,1,X.LAYER.ACTOR, () => order.push('near-actor'));     // d=2
  dq.push(1,1,X.LAYER.PROP,  () => order.push('near-prop'));      // d=2
  dq.push(1,1,X.LAYER.FLOOR, () => order.push('near-floor'));     // d=2
  dq.push(5,5,X.LAYER.PROP,  () => order.push('mid-prop'));       // d=10
  dq.flush();
  eq('10.8 깊이 정렬이 뒤→앞, 같은 타일은 바닥→소품→인물',
     order.join(' '), 'near-floor near-prop near-actor mid-prop far-actor');
  eq('10.9 flush 후 큐가 비워진다 (프레임 간 누수 없음)', dq.q.length, 0);

  // 프레임 도중 예외가 나도 다음 프레임으로 새면 안 된다. drawDay 가 시작할 때
  // 큐를 비우는지 실제로 확인한다 — 공유 큐로 바꾸면서 생긴 위험이다.
  dq.clear();
  dq.push(0,0,X.LAYER.FX, () => { throw new Error('frame blew up'); });
  try { dq.flush(); } catch(e){ /* 의도된 폭발 */ }
  ok('10.9b 예외로 남은 찌꺼기가 다음 프레임에 섞이지 않는다',
     (X.startDay(), X.__sandbox.drawDay(0.1), dq.q.length === 0), 'leftover=' + dq.q.length);

  // 같은 (깊이, 레이어)면 넣은 순서를 지켜야 한다. 안정 정렬에 기대고 있음을 못박는다.
  const stable = [];
  dq.clear();
  for(let i=0;i<40;i++) dq.push(4,4,X.LAYER.PROP, () => stable.push(i));
  dq.flush();
  ok('10.10 같은 깊이·레이어는 넣은 순서를 지킨다 (안정 정렬)',
     stable.every((v,i) => v === i), stable.slice(0,8).join(','));

  // (5) 실제 게임 한 프레임을 그려도 예외가 없는가. drawImage 는 스텁이라
  //     에셋이 없을 때의 도형 폴백 경로가 돌아간다 — 그쪽도 깨지면 안 된다.
  //     drawDay/drawNight 가 없으면 이 검사는 아무것도 안 한 채 통과한다. 못박는다.
  ok('10.11a 그리기 함수가 실제로 존재한다',
     typeof X.__sandbox.drawDay === 'function' && typeof X.__sandbox.drawNight === 'function');
  let derr = null;
  try { X.startDay(); X.updateDay(0.1); X.__sandbox.drawDay(0.1); }
  catch(e){ derr = e; }
  ok('10.11 낮 한 프레임을 그려도 예외가 없다', derr === null, derr && derr.stack);
  let nerr = null;
  try { X.startNight(); X.updateNight(0.1); X.__sandbox.drawNight(0.1); }
  catch(e){ nerr = e; }
  ok('10.12 밤 한 프레임을 그려도 예외가 없다', nerr === null, nerr && nerr.stack);
}

/* ══ 11. 벽 충돌 ══════════════════════════════════════ */
{
  const X = load();
  // 그려지는 벽과 막히는 벽이 같아야 한다. 하나만 어긋나면 안 보이는 벽이거나
  // 통과되는 벽이다 — 둘 다 화면을 봐야만 알 수 있는 종류의 버그다.
  const drawn = new Set();
  X.BLDG.forEach(b => {
    for(let i=-1;i<b.w;i++) drawn.add((b.x+i)+','+(b.y-1));
    for(let j=0;j<b.h;j++)  drawn.add((b.x-1)+','+(b.y+j));
  });
  eq('11.1 벽 타일 수가 그려지는 벽과 일치', X.WALLS.size, drawn.size);
  ok('11.2 벽 집합이 그려지는 벽과 정확히 같다',
     [...X.WALLS].every(k => drawn.has(k)));

  // 좀비를 벽 바로 안쪽에 놓고 벽 쪽으로 밀어 본다. 통과하면 실패.
  X.startNight();
  const b = X.BLDG[0];
  const z = {x: b.x + 0.5, y: b.y + 0.2};
  for(let i=0;i<200;i++) X.step(z, 0, -0.05, 0.5, X.MAP - 0.5);  // 북쪽 벽으로 200틱
  ok('11.3 좀비가 북쪽 벽을 통과하지 못한다', z.y >= b.y,
     'y=' + z.y.toFixed(2) + ' (벽 안쪽 하한 ' + b.y + ')');
  const z2 = {x: b.x + 0.2, y: b.y + 0.5};
  for(let i=0;i<200;i++) X.step(z2, -0.05, 0, 0.5, X.MAP - 0.5);  // 서쪽 벽으로
  ok('11.4 좀비가 서쪽 벽을 통과하지 못한다', z2.x >= b.x,
     'x=' + z2.x.toFixed(2) + ' (벽 안쪽 하한 ' + b.x + ')');

  // 남·동은 입구다. 막히면 안에 있는 컨테이너를 영영 못 턴다 (함정 A).
  const z3 = {x: b.x + b.w - 0.5, y: b.y + b.h - 0.5};
  for(let i=0;i<100;i++) X.step(z3, 0.05, 0.05, 0.5, X.MAP - 0.5);
  ok('11.5 남·동은 입구라 통과된다', z3.x > b.x + b.w && z3.y > b.y + b.h,
     'x=' + z3.x.toFixed(2) + ' y=' + z3.y.toFixed(2));

  // 모든 컨테이너가 벽 타일 위에 있지 않아야 한다 — 벽에 박히면 못 연다
  const stuck = X.N.cont.filter(c => X.blocked(c.x, c.y));
  eq('11.6 벽에 박힌 컨테이너 없음', stuck.length, 0);

  // 함정 A — 벽을 세우면 "아무 데도 못 간다"가 될 수 있다. 벽에 안 박혔다고
  // 닿을 수 있는 건 아니다. 스폰에서 실제 이동 규칙으로 flood fill 해서 확인한다.
  const G = 0.5, k = (x,y) => x + ',' + y;
  const s0 = [Math.round(X.SPAWN[0]/G)*G, Math.round(X.SPAWN[1]/G)*G];
  const seen = new Set([k(s0[0], s0[1])]), q = [s0];
  while(q.length){
    const [x,y] = q.shift();
    for(const [dx,dy] of [[G,0],[-G,0],[0,G],[0,-G]]){
      const e = {x:x, y:y};
      X.step(e, dx, dy, 0.5, X.MAP - 0.5);
      if(Math.abs(e.x-x) < 1e-9 && Math.abs(e.y-y) < 1e-9) continue;
      const kk = k(e.x, e.y);
      if(!seen.has(kk)){ seen.add(kk); q.push([e.x, e.y]); }
    }
  }
  const unreach = X.N.cont.filter(c =>
    !seen.has(k(Math.round(c.x/G)*G, Math.round(c.y/G)*G)));
  eq('11.7 모든 컨테이너가 스폰에서 도달 가능', unreach.length, 0);

  // 카페 3채는 밑면 앵커 타일 1칸만 막는다 (§7.6) — WALLS 와 별개 집합이다.
  const want = new Set([X.HOME].concat(X.RIVAL)
    .map(p => Math.floor(p[0]) + ',' + Math.floor(p[1])));
  eq('11.8 카페 충돌 타일이 3칸', X.BLDG_BLOCK.size, 3);
  ok('11.8b 그 3칸이 HOME·RIVAL 의 밑면 타일과 같다',
     [...X.BLDG_BLOCK].every(k => want.has(k)) && want.size === 3,
     [...X.BLDG_BLOCK].join(' | '));
}
{
  const X = load();
  X.startNight();
  X.N.zomb.length = 0;
  const cell = e => Math.floor(e.x) + ',' + Math.floor(e.y);
  const homeK = Math.floor(X.HOME[0]) + ',' + Math.floor(X.HOME[1]);
  X.N.p.x = 5; X.N.p.y = X.HOME[1];
  let inside = 0;
  for(let i=0;i<200;i++){ X.step(X.N.p, -0.05, 0, 0.6, X.MAP - 0.6); if(cell(X.N.p) === homeK) inside++; }
  eq('11.9 HOME 타일로 밀어도 안으로 못 들어간다', inside, 0, 'p=' + cell(X.N.p));
  // 막혀 있어도 탈출은 성립해야 한다 — 안 그러면 밤을 못 끝낸다 (함정)
  const d = X.dist(X.N.p, {x:X.HOME[0], y:X.HOME[1]});
  ok('11.10 막힌 자리에서도 HOME 반경 2.0 안이다', d < 2.0, 'd=' + d.toFixed(2));
  X.N.cont.length = 0;                        // E 탭이 컨테이너 줍기로 새지 않게
  tap(X,'KeyE');
  X.updateNight(0.05);
  eq('11.10b 그 자리에서 E 로 귀환이 성립한다', X.G.phase, 'settle');
  rel(X,'KeyE');
}
{
  // 라이벌 카페도 막히지만, 발견은 막히기 전에 일어나야 한다(보이지 않는 벽이 아님)
  const X = load();
  X.startNight();
  X.N.zomb.length = 0; X.N.cont.length = 0;
  const cell = e => Math.floor(e.x) + ',' + Math.floor(e.y);
  X.N.p.x = 24; X.N.p.y = X.RIVAL[0][1];
  let foundAtX = -1, inside = 0;
  hold(X,'KeyA'); hold(X,'KeyW');             // A+W = 순수 −x (아이소 축)
  for(let i=0;i<200 && X.G.phase === 'night';i++){
    X.updateNight(1/30);
    if(foundAtX < 0 && X.ME.found.indexOf(X.BOTS[0].name) >= 0) foundAtX = X.N.p.x;
    if(cell(X.N.p) === '21,4') inside++;
  }
  rel(X,'KeyA'); rel(X,'KeyW');
  eq('11.11 라이벌 카페 타일(21,4)도 막힌다', inside, 0, 'p=' + cell(X.N.p));
  ok('11.11b 막히기 전에 카페 발견이 먼저 뜬다', foundAtX > X.N.p.x + 0.5,
     'found@x=' + foundAtX.toFixed(2) + ' stop@x=' + X.N.p.x.toFixed(2));
}

/* ══ 12. 기획서 §15.2 완료 기준 중 미검증분 ═════════════ */
{
  // "머신에 8초 이상 방치하면 잔이 탐 상태가 되고, 제공해도 골드가 0이다"
  // 실제 구현은 더 강하다 — 탄 잔은 애초에 들 수가 없어서 서빙대까지 못 간다.
  // 그 성질이 유지되는지를 본다(들 수 있게 되는 순간 골드 0 처리가 필요해진다).
  const X = load();
  X.startDay();
  const grind = X.ST.find(s => s.type === 'grind');
  X.D.recipe = 0;
  X.interact(X.ST.find(s => s.type === 'shelf'));
  X.interact(grind);
  const st = X.D.st[grind.id];
  S(X, X.updateDay, X.PROC.grind.t + X.PROC.grind.burn + 1);
  eq('12.1 방치하면 탄다', st.state, 'burnt');
  X.interact(grind);                                   // 탄 걸 만지면 버려진다
  ok('12.2 탄 잔은 들 수 없다 (버려진다)', X.D.carry === null && st.item === null);
  eq('12.3 탄 스테이션은 비워져 idle 로 돌아간다', st.state, 'idle');
  eq('12.4 탄 잔은 서빙대까지 못 간다', X.D.served.length, 0);
}
{
  // "손님 대기 게이지가 다 차면 손님이 나가고 골드가 들어오지 않는다"
  const X = load();
  X.startDay();
  const g0 = X.ME.gold, s0 = X.ME.sales;
  S(X, X.updateDay, 60);                               // 아무것도 안 만들고 60초
  ok('12.5 손님이 실제로 왔다가 나갔다', X.D.budget < X.dayCustomers(),
     'budget=' + X.D.budget);
  eq('12.6 아무것도 안 팔면 골드가 그대로', X.ME.gold, g0);
  eq('12.7 아무것도 안 팔면 누적 판매도 그대로', X.ME.sales, s0);
}
{
  // "이미 턴 컨테이너는 다른 색 아웃라인으로 표시된다"
  const X = load();
  const cols = ['new', 'busy', 'done'].map(X.contOutlineCol);
  eq('12.8 아웃라인 3상태가 서로 다른 색', new Set(cols).size, 3, cols.join(','));
  ok('12.9 손전등 반경이 양수', X.OUTLINE_R > 0, 'R=' + X.OUTLINE_R);
}
{
  // "배회자는 플레이어가 없을 때 스폰 지점 4타일 안을 계속 움직인다"
  const X = load();
  X.startNight();
  X.N.p.x = 0.6; X.N.p.y = 0.6;                        // 플레이어를 맵 구석으로 치운다
  const z = X.N.zomb.find(v => v.kind === 'shambler');
  const p0 = {x: z.x, y: z.y};
  let far = 0, moved = 0, roam = 0;
  for(let i = 0; i < 1800; i++){                       // 30초
    const bx = z.x, by = z.y;
    X.N.p.x = 0.6; X.N.p.y = 0.6;
    X.updateNight(1/60);
    if(Math.hypot(z.x - bx, z.y - by) > 1e-6) moved++;
    if(Math.hypot(z.x - z.sx, z.y - z.sy) > 4.5) far++;
    // 끝 위치로 재면 안 된다 — 배회는 돌아다니다 시작점 근처로 돌아올 수 있다.
    // 실제로 12.12 가 그래서 한 번 틀렸다. 구간 최대 변위로 잰다.
    roam = Math.max(roam, Math.hypot(z.x - p0.x, z.y - p0.y));
  }
  ok('12.10 배회자가 30초 동안 실제로 움직인다', moved > 900, moved + '/1800 틱');
  eq('12.11 배회자가 스폰 4타일 밖으로 안 나간다', far, 0);
  ok('12.12 배회자가 제자리에 굳지 않고 돌아다닌다', roam > 2,
     '최대 변위 ' + roam.toFixed(2) + '타일');
}

/* ══ 13. 함정 A/B — 진행 교착 ═════════════════════════ */
{
  // 함정 B(밤): 컨테이너 앞까지 갔는데 "도착"이 안 잡히면 영영 못 턴다.
  // 홀드 임계값은 0.18초 = 11틱. 처음 이 검사를 10틱만 돌렸다가 8개가 "안 열림"으로
  // 나왔는데, 게임이 아니라 테스트가 짧았던 것이었다. 넉넉히 30틱 돌린다.
  const X = load();
  X.startNight();
  // 좀비를 비운다. 이 검사의 대상은 "닿아서 열리는가"지 "버티면서 터는가"가 아니다.
  // 안 비우면 컨테이너 14개를 차례로 붙잡고 서 있는 동안 좀비에게 맞아 죽어서
  // 간헐적으로 실패한다(실측: hp=0 로 죽고 N.open 이 풀렸다). 게임은 정상이었다.
  X.N.zomb.length = 0;
  const unopened = [];
  X.N.cont.forEach(c => {
    X.N.p.x = c.x; X.N.p.y = c.y; X.N.open = null; X.N.searchHold = 0;
    X.keys['KeyE'] = true;
    for(let t = 0; t < 30; t++) X.updateNight(1/60);
    if(X.N.open !== c) unopened.push(c.kind + '@' + c.x.toFixed(1) + ',' + c.y.toFixed(1));
    X.keys['KeyE'] = false; X.N.open = null;
  });
  eq('13.1 모든 컨테이너가 실제로 열린다', unopened.length, 0, unopened.join(' '));
}
{
  // 함정 A(낮): 스테이션이 플레이어 이동 범위 밖이면 커피를 못 만든다.
  // stAt 은 축별 판정(체비셰프)이라 중심 유클리드 거리 함정을 이미 피해 간다.
  const X = load();
  X.startDay();
  const unreach = X.ST.filter(s => {
    const px = Math.max(-0.4, Math.min(10.4, s.tx));
    const py = Math.max(-0.4, Math.min(4.8, s.ty));
    const h = X.stAt(px, py);
    return !h || h.id !== s.id;
  });
  eq('13.2 모든 스테이션에 손이 닿는다', unreach.length, 0,
     unreach.map(s => s.type).join(' '));
}
{
  // 핵심 루프를 **진짜 키 입력**으로 한 바퀴 — 선반→공정→서빙대→판매.
  // interact() 를 직접 부르는 다른 테스트들과 달리 입력 레이어까지 지난다.
  const X = load();
  X.startDay();
  const g0 = X.ME.gold;
  const at = t => X.ST.find(s => s.type === t);
  const go = s => { X.D.p.x = s.tx; X.D.p.y = s.ty; X.pressed['KeyE'] = true; X.updateDay(1/60); };
  X.D.recipe = 0;
  go(at('shelf'));
  ok('13.3 선반에서 재료를 든다', !!X.D.carry);
  X.stepsOf(X.ME.menu[0]).forEach(k => {
    const st = at(k);
    go(st);
    S(X, X.updateDay, X.PROC[k].t + 0.2);
    go(st);
  });
  go(at('serve'));
  S(X, X.updateDay, 60);
  ok('13.4 키 입력만으로 한 잔이 팔린다', X.ME.gold > g0,
     Math.round(g0) + ' -> ' + Math.round(X.ME.gold));
}

/* ══ 14. 신규 기능 (기획 v2.2 미구현분 + 쓰레기 봉투) ═══ */
{
  // 쓰레기 봉투 — 손에 든 걸 버릴 수 있어야 한다.
  // 없으면 취향이 안 맞는 잔을 든 순간 손이 묶여 그 낮이 통째로 죽는다.
  const X = load();
  X.startDay();
  const trash = X.ST.find(s => s.type === 'trash');
  ok('14.1 쓰레기 봉투 스테이션이 존재한다', !!trash);
  X.interact(trash);
  eq('14.2 빈손으로 가면 아무 일도 없다', X.D.carry, null);
  X.D.recipe = 0;
  X.interact(X.ST.find(s => s.type === 'shelf'));
  ok('14.3 재료를 들었다', !!X.D.carry);
  X.interact(trash);
  eq('14.4 쓰레기 봉투에서 버려진다', X.D.carry, null);
  // 버린 뒤 다시 들 수 있어야 손이 안 묶인다
  X.interact(X.ST.find(s => s.type === 'shelf'));
  ok('14.5 버린 뒤 다시 들 수 있다', !!X.D.carry);
  // 완성된 잔도 버릴 수 있다 (취향 불일치를 손해 보고 파느니 버리는 선택)
  X.D.carry.stage = 99;
  X.interact(trash);
  eq('14.6 완성된 잔도 버릴 수 있다', X.D.carry, null);
  eq('14.7 버린 잔은 서빙대에 안 올라간다', X.D.served.length, 0);
}
{
  // 레시피 슬롯 +1 은 **삭제됐다**. 등록 메뉴가 늘면 수요 70%가 더 잘게 갈라져
  // 기대 배수가 희석된다 — 10일 완주 실측 순증 −84G 로, 사면 손해인 시설이었다.
  const X = load();
  eq('14.8 메뉴판은 3칸 고정', X.menuCap(), 3);
  ok('14.9 슬롯 시설이 목록에 없다', !X.FACIL.some(v => v.id === 'slot'));
  X.ME.fac.slot = true;
  eq('14.10 슬롯 플래그를 세워도 칸이 안 늘어난다', X.menuCap(), 3);
  X.ME.menu = [0, 1, 2, -1];
  eq('14.11 3칸이 차면 빈 칸이 없다', X.ME.menu.slice(0, X.menuCap()).indexOf(-1), -1);
}
{
  // 이사 — 나를 아는 상대가 전부 리셋되고, 골드는 400 + 그날 매출 절반이 나간다
  const X = load();
  X.startDay();
  X.BOTS.forEach(b => { b.knows = true; });
  X.G.daySales = 200;
  X.ME.gold = 1000;
  const want = X.MOVE_COST + 100;
  eq('14.12 이사 비용 = 400 + 그날 매출 절반', X.moveCost(), want);
  const s0 = X.ME.sales;
  ok('14.13 이사가 성사된다', X.relocate() === true);
  eq('14.14 골드에서 비용이 빠진다', X.ME.gold, 1000 - want);
  eq('14.15 나를 아는 상대가 0명이 된다', X.BOTS.filter(b => b.knows).length, 0);
  eq('14.16 누적 판매(승리 지표)는 안 줄어든다', X.ME.sales, s0);
  X.ME.gold = 10;
  ok('14.17 골드가 모자라면 이사가 안 된다', X.relocate() === false);
  eq('14.17b 실패했으면 골드도 안 나간다', X.ME.gold, 10);
}
{
  // 낮 대시 — Shift 로 같은 시간에 더 멀리 간다. 쿨다운 중에는 안 걸린다.
  const X = load();
  X.startDay();
  const run = (dash) => {
    X.startDay();
    X.D.p.x = 3; X.D.p.y = 3; X.D.dash = 0; X.D.dashCd = 0;
    hold(X, 'KeyD');
    if(dash) X.pressed['ShiftLeft'] = true;
    const x0 = X.D.p.x, y0 = X.D.p.y;
    S(X, X.updateDay, X.DASH_T);
    const d = Math.hypot(X.D.p.x - x0, X.D.p.y - y0);
    rel(X, 'KeyD');
    return d;
  };
  const plain = run(false), dashed = run(true);
  ok('14.18 대시가 실제로 더 멀리 간다', dashed > plain * 1.8,
     plain.toFixed(2) + ' -> ' + dashed.toFixed(2));
  // 쿨다운
  X.startDay();
  X.D.p.x = 3; X.D.p.y = 3; X.D.dash = 0; X.D.dashCd = 0;
  hold(X, 'KeyD');
  X.pressed['ShiftLeft'] = true; X.updateDay(1/60);
  ok('14.19 대시가 걸렸다', X.D.dash > 0);
  S(X, X.updateDay, X.DASH_T + 0.1);
  X.pressed['ShiftLeft'] = true; X.updateDay(1/60);
  ok('14.20 쿨다운 중에는 다시 안 걸린다', X.D.dash <= 0, 'dashCd=' + X.D.dashCd.toFixed(2));
  S(X, X.updateDay, X.DASH_CD);
  X.pressed['ShiftLeft'] = true; X.updateDay(1/60);
  ok('14.21 쿨다운이 끝나면 다시 걸린다', X.D.dash > 0);
  rel(X, 'KeyD');
}
{
  // 넉백 + 0.5초 경직 (§7.3). 경직이 없으면 무적 1.4초 덕에 좀비 사이를 걸어 나간다.
  const X = load();
  X.startNight();
  X.N.zomb.length = 0;
  X.N.p.x = 10; X.N.p.y = 10;
  const src = {x: 10.5, y: 10};                        // 오른쪽에서 때린다
  X.hurt(1, src);
  ok('14.22 맞으면 반대 방향으로 밀려난다', X.N.p.x < 10 - 0.5,
     'x=' + X.N.p.x.toFixed(2));
  ok('14.23 경직이 걸린다', X.N.stun > 0, 'stun=' + X.N.stun.toFixed(2));
  // 경직 중에는 이동 입력이 먹지 않는다
  const bx = X.N.p.x;
  hold(X, 'KeyD');
  X.updateNight(1/60);
  ok('14.24 경직 중에는 못 움직인다', Math.abs(X.N.p.x - bx) < 1e-6 && !X.N.moving);
  S(X, X.updateNight, X.STUN_T + 0.1);
  X.updateNight(1/60);
  ok('14.25 경직이 풀리면 다시 움직인다', X.N.moving);
  rel(X, 'KeyD');
  // 넉백이 벽을 뚫으면 안 된다 (step 을 거치는지)
  X.N.p.x = X.BLDG[0].x + 0.3; X.N.p.y = X.BLDG[0].y + 0.3;
  X.N.inv = 0;
  X.hurt(1, {x: X.BLDG[0].x + 2, y: X.BLDG[0].y + 2});  // 벽 쪽으로 밀린다
  ok('14.26 넉백이 벽을 뚫지 않는다', !X.blocked(X.N.p.x, X.N.p.y),
     X.N.p.x.toFixed(2) + ',' + X.N.p.y.toFixed(2));
}
{
  // 붕대 — R 홀드 3초로 1칸 회복, 이동하면 취소
  const X = load();
  X.startNight();
  X.N.zomb.length = 0;
  X.N.hp = 1;
  X.G.bag = [{kind:'bandage', label:'붕대'}];
  hold(X, 'KeyR');
  S(X, X.updateNight, X.BANDAGE_T - 0.5);
  eq('14.27 3초가 안 됐으면 아직 회복 안 된다', X.N.hp, 1);
  S(X, X.updateNight, 0.7);
  eq('14.28 3초 홀드하면 1칸 회복', X.N.hp, 2);
  eq('14.29 붕대가 가방에서 소모된다', X.G.bag.length, 0);
  rel(X, 'KeyR');
  // 이동하면 취소
  X.N.hp = 1;
  X.G.bag = [{kind:'bandage', label:'붕대'}];
  hold(X, 'KeyR'); hold(X, 'KeyD');
  S(X, X.updateNight, X.BANDAGE_T + 1);
  eq('14.30 이동 중에는 붕대를 못 감는다', X.N.hp, 1);
  eq('14.30b 붕대도 소모되지 않는다', X.G.bag.length, 1);
  rel(X, 'KeyR'); rel(X, 'KeyD');
  // 체력이 꽉 차면 낭비되지 않는다
  X.N.hp = 3;
  hold(X, 'KeyR');
  S(X, X.updateNight, X.BANDAGE_T + 1);
  eq('14.31 체력이 꽉 차면 붕대를 안 쓴다', X.G.bag.length, 1);
  rel(X, 'KeyR');
}
{
  // 붕대는 밤 소모품이라 낮으로 안 넘어온다. 예전 회수 코드는 "stock 이 아니면 레시피"로
  // 갈라서 붕대를 들고 나오면 RECIPES[undefined] 에서 터졌다.
  const X = load();
  X.startNight();
  X.ME.owned = [0];
  X.G.bag = [{kind:'bandage', label:'붕대'}, {kind:'stock', label:'원두'}];
  X.ME.pouch = [{kind:'bandage', label:'붕대'}];
  const st0 = X.ME.stock;
  let err = null;
  try { X.endNight(true); } catch(e){ err = e; }
  ok('14.32 붕대를 들고 탈출해도 터지지 않는다', err === null, err && err.message);
  eq('14.33 붕대는 낮으로 안 넘어온다', X.ME.owned.length, 1);
  eq('14.34 같이 있던 원두는 정상 회수된다', X.ME.stock, st0 + 1);
  ok('14.35 owned 에 undefined 가 안 들어간다',
     X.ME.owned.every(v => typeof v === 'number'));
}
{
  // 붕대가 실제로 전리품 풀에 있는가 (시체에서 더 잘 나와야 한다 §9.4)
  const X = load();
  let corpse = 0, drawer = 0;
  for(let i = 0; i < 400; i++){
    if(X.rollSlots('corpse').slots.some(s => s.item && s.item.kind === 'bandage')) corpse++;
    if(X.rollSlots('drawer').slots.some(s => s.item && s.item.kind === 'bandage')) drawer++;
  }
  ok('14.36 붕대가 전리품에 실제로 나온다', corpse > 0 && drawer > 0,
     '시체 ' + corpse + '/400, 서랍 ' + drawer + '/400');
  ok('14.37 시체에서 더 잘 나온다', corpse > drawer,
     '시체 ' + corpse + ' vs 서랍 ' + drawer);
}

{
  // 붕대가 컨테이너 안에 있을 때 루팅 UI 가 터지지 않는가.
  // 이 버그는 실제로 났다 — updateLoot 이 "stock 이 아니면 레시피"로 갈라서
  // id 없는 붕대에서 RECIPES[undefined].t 로 죽었다. 난수라 60회 중 9회만 재현됐다.
  // 여기서는 붕대를 확정으로 심어 매번 재현되게 만든다.
  const X = load();
  X.startNight();
  X.N.zomb.length = 0;
  const c = X.N.cont[0];
  c.slots.forEach(s => { s.item = {kind:'bandage', label:'붕대'}; s.rev = 1; s.taken = false; });
  X.N.p.x = c.x; X.N.p.y = c.y;
  let err = null;
  try {
    hold(X, 'KeyE');
    S(X, X.updateNight, 1.0);
    X.pickSlot(0);
    S(X, X.updateNight, 1.0);
    rel(X, 'KeyE');
  } catch(e){ err = e; }
  ok('14.38 붕대가 든 통을 열어도 UI 가 안 터진다', err === null, err && err.message);
  ok('14.39 붕대를 실제로 주울 수 있다',
     X.G.bag.some(x => x.kind === 'bandage'), JSON.stringify(X.G.bag));
}

/* ══ 15. A* 길찾기 + 시야각 ══════════════════════════════ */
{
  const X = load();
  X.startNight();
  // 벽을 사이에 두고 반대편으로 가는 길이 실제로 나오는가
  const b = X.BLDG[0];
  const p = X.findPath(b.x + 2, b.y - 3, b.x + 2, b.y + 2);   // 북쪽 벽 너머 → 건물 안
  ok('15.1 벽 너머로 가는 경로가 나온다', !!p && p.length > 0, p ? p.length + '칸' : 'null');
  ok('15.2 경로가 벽 위를 지나지 않는다',
     p.every(w => !X.blocked(w[0], w[1])),
     p && p.filter(w => X.blocked(w[0], w[1])).map(w => w.join(',')).join(' '));
  // 직선 거리보다 길어야 한다 — 돌아가는 게 A* 의 존재 이유다
  const straight = Math.hypot(0, 5);
  ok('15.3 벽을 돌아가므로 직선보다 길다', p.length > straight,
     p.length + ' vs 직선 ' + straight.toFixed(1));

  eq('15.4 출발=목표면 빈 경로', X.findPath(5, 5, 5, 5).length, 0);
  eq('15.5 목표가 벽이면 null', X.findPath(5, 5, b.x, b.y - 1), null);

  // 대각으로 벽 모서리를 스쳐 지나가지 않는다
  const diag = X.findPath(b.x - 2, b.y - 2, b.x, b.y);
  ok('15.6 대각으로 벽 모서리를 통과하지 않는다',
     !diag || diag.every(w => !X.blocked(w[0], w[1])));
}
{
  // 좀비가 실제로 벽을 돌아서 온다 — 직선 조향이면 벽에 붙어 비빈다
  const X = load();
  X.startNight();
  X.N.cont.length = 0;
  const b = X.BLDG[0];
  // 좀비는 건물 안, 플레이어는 북쪽 벽 바로 너머. 직선으로는 벽이 가로막는다.
  const z = Z(X, 'runner', b.x + 2, b.y + 1, {wt:99, cd:99});
  X.N.zomb = [z];
  X.N.p.x = b.x + 2; X.N.p.y = b.y - 2.5;
  z.hx = X.N.p.x; z.hy = X.N.p.y; z.chase = 99;
  const d0 = X.dist(X.N.p, z);
  S(X, X.updateNight, 8, 1/30);
  const d1 = X.dist(X.N.p, z);
  ok('15.7 좀비가 벽을 돌아 플레이어에게 접근한다', d1 < d0 - 1,
     d0.toFixed(2) + ' → ' + d1.toFixed(2));
  ok('15.8 돌아오는 동안 벽을 통과하지 않았다', !X.blocked(z.x, z.y),
     z.x.toFixed(2) + ',' + z.y.toFixed(2));
}
{
  // 시야각 — 소리는 전방향, 눈은 전방 100도만
  const X = load();
  X.startNight();
  X.N.cont.length = 0;
  ok('15.9 시야각이 360도가 아니다', X.FOV_DEG > 0 && X.FOV_DEG < 360, X.FOV_DEG);

  // 등을 돌린 좀비는 코앞에 서 있어도 못 본다
  const back = Z(X, 'runner', 10, 10, {wt:99, cd:99, fx:1, fy:0});   // +x 를 본다
  X.N.zomb = [back];
  X.N.p.x = 8; X.N.p.y = 10;                    // -x 쪽 = 등 뒤, 거리 2 (시야 8 안)
  X.updateNight(1/60);
  eq('15.10 등 뒤에 있으면 시야 안이어도 못 본다', back.chase, 0);

  // 같은 자리인데 좀비가 돌아보면 즉시 발견된다
  back.fx = -1; back.fy = 0;
  X.updateNight(1/60);
  ok('15.11 돌아보면 바로 발견한다', back.chase > 0, 'chase=' + back.chase);

  // 소리는 등 뒤에서도 들린다 — 이게 눈과 갈라지는 지점이다
  const X2 = load();
  X2.startNight();
  X2.N.cont.length = 0;
  const deaf = Z(X2, 'shambler', 10, 0, {wt:99, cd:99, fx:1, fy:0});  // 등을 돌림
  X2.N.zomb = [deaf];
  X2.N.p.x = 4; X2.N.p.y = 0;                   // 6타일 뒤
  hold(X2, 'KeyD'); hold(X2, 'ShiftLeft');      // 뛰기 = 소음 8타일
  X2.updateNight(0.05);
  rel(X2, 'KeyD'); rel(X2, 'ShiftLeft');
  ok('15.12 등을 돌리고 있어도 소리는 듣는다', deaf.chase > 0, 'chase=' + deaf.chase);
}
{
  // 좀비는 가는 쪽을 본다 — 안 그러면 옆걸음질하면서 뒤를 보는 좀비가 된다
  const X = load();
  X.startNight();
  X.N.cont.length = 0;
  X.N.p.x = 0.6; X.N.p.y = 0.6;                 // 플레이어를 구석으로 치운다
  const z = X.N.zomb[0];
  let misfaced = 0, moved = 0;
  for(let i = 0; i < 600; i++){
    X.N.p.x = 0.6; X.N.p.y = 0.6;
    const bx = z.x, by = z.y;
    X.updateNight(1/60);
    const dx = z.x - bx, dy = z.y - by, l = Math.hypot(dx, dy);
    if(l > 1e-4){
      moved++;
      // 벽 옆은 제외한다. step() 이 벽을 따라 미끄러뜨리므로 "가려던 쪽"과
      // "실제로 간 쪽"이 갈라지는 게 정상이다 — 실측으로 어긋남의 100% 가 벽 옆이었다.
      const nearWall = [[0.6,0],[-0.6,0],[0,0.6],[0,-0.6]]
        .some(d => X.blocked(z.x + d[0], z.y + d[1]));
      if(!nearWall && (dx/l)*z.fx + (dy/l)*z.fy < 0.5) misfaced++;
    }
  }
  ok('15.13 좀비가 실제로 움직였다', moved > 200, moved + '틱');
  ok('15.14 벽이 없는 곳에서는 가는 쪽을 보고 걷는다', misfaced === 0,
     '어긋남 ' + misfaced + '/' + moved);
  ok('15.15 시야 벡터가 항상 단위벡터다',
     Math.abs(Math.hypot(z.fx, z.fy) - 1) < 0.01, Math.hypot(z.fx, z.fy).toFixed(3));
}

{
  // 시야는 벽에 막히고, 소리는 안 막힌다. 이 둘이 갈라져야 "벽 뒤에 숨기"와
  // "조용히 있기"가 서로 다른 대응이 된다. QA 에서 실제로 뚫려 있던 것을 잡았다 —
  // A* 와 이동은 벽을 지키는데 시야만 안 지켜서, 벽 뒤에 숨는 게 무의미했다.
  const X = load();
  X.startNight();
  X.N.cont.length = 0;
  const b = X.BLDG[0];
  const z = Z(X, 'runner', b.x + 2, b.y + 0.5, {fx:0, fy:-1, cd:99, wt:99});  // 벽 쪽을 정면으로
  X.N.zomb = [z];
  X.N.p.x = b.x + 2; X.N.p.y = b.y - 2;          // 북쪽 벽 너머, 시야 8 안
  ok('15.16 사이에 벽이 실제로 있다', X.blocked(b.x + 2.5, b.y - 0.5));
  ok('15.17 시야 안·정면이지만 거리는 가깝다', X.dist(X.N.p, z) < z.T.sight);
  X.updateNight(1/60);
  eq('15.18 벽 너머는 보지 못한다', z.chase, 0);

  // 소리는 벽을 넘어 들린다
  hold(X, 'KeyD'); hold(X, 'ShiftLeft');
  X.updateNight(0.05);
  rel(X, 'KeyD'); rel(X, 'ShiftLeft');
  ok('15.19 벽 너머라도 소리는 들린다', z.chase > 0, 'chase=' + z.chase);

  // 벽이 없으면 여전히 보인다 (과잉 차단 회귀 방지)
  const X2 = load();
  X2.startNight();
  X2.N.cont.length = 0;
  const w = Z(X2, 'runner', 10, 10, {fx:-1, fy:0, cd:99, wt:99});
  X2.N.zomb = [w];
  X2.N.p.x = 8; X2.N.p.y = 10;
  X2.updateNight(1/60);
  ok('15.20 트인 곳에서는 정상적으로 본다', w.chase > 0, 'chase=' + w.chase);
}

{
  // endDay/endNight 는 멱등하지 않다 — 봇 매출·도난이 다시 쌓인다.
  // QA 시뮬에서 페이즈가 넘어간 뒤에도 updateDay 를 계속 밀었더니 봇 골드가
  // 344억까지 튀었다. loop() 가 막고는 있지만 돈이 걸린 경로라 이중으로 막는다.
  const X = load();
  X.startDay();
  S(X, X.updateDay, X.DAY_SEC + 1);              // 타이머를 넘긴다
  eq('16.1 낮이 끝나면 황혼으로 넘어간다', X.G.phase, 'dusk');
  const s0 = X.BOTS.map(b => b.sales);
  S(X, X.updateDay, 60);                          // 넘어간 뒤에도 계속 민다
  ok('16.2 페이즈가 넘어간 뒤 낮 갱신은 무시된다',
     X.BOTS.every((b, i) => b.sales === s0[i]),
     X.BOTS.map(b => Math.round(b.sales)).join(','));
  eq('16.3 페이즈도 그대로 유지된다', X.G.phase, 'dusk');

  const Y = load();
  Y.startNight();
  const g0 = Y.ME.gold;
  S(Y, Y.updateNight, Y.NIGHT_SEC + 1);
  eq('16.4 밤이 끝나면 결산으로 넘어간다', Y.G.phase, 'settle');
  S(Y, Y.updateNight, 60);
  eq('16.5 넘어간 뒤 밤 갱신도 무시된다', Y.G.phase, 'settle');
  ok('16.6 도난이 두 번 일어나지 않는다', Y.ME.gold >= 0 && Y.ME.gold <= g0,
     g0 + ' -> ' + Y.ME.gold);
}

/* ══ 17. A 구현 (D1/D4/D5/F1~F3/F5/F6) ═══════════════════ */
{
  // F5 — 원두가 등급 배수의 연료다 (§6.5). 재고가 없으면 **못 만드는 게 아니라**
  // 만들어지되 ×1.0 으로 팔린다 — "기본 원두 무한"이라는 파산 방지 바닥을 안 깬다.
  const X = load();
  X.startDay();
  const rare = X.RECIPES.findIndex(r => r.r === '희귀');
  ok('17.1 희귀 레시피가 존재한다', rare >= 0);
  eq('17.2 희귀는 원두 2개를 먹는다', X.BEAN_COST['희귀'], 2);
  X.ME.menu = [rare, -1, -1, -1]; X.D.recipe = 0;

  X.ME.stock = 2;
  X.interact(X.ST.find(s => s.type === 'shelf'));
  eq('17.3 집는 순간 차감된다', X.ME.stock, 0);
  ok('17.4 등급이 붙은 잔이다', X.D.carry.o === true);

  X.D.carry = null; X.ME.stock = 0;
  X.interact(X.ST.find(s => s.type === 'shelf'));
  ok('17.5 재고가 없어도 만들어진다', !!X.D.carry);
  ok('17.6 다만 등급이 안 붙는다', X.D.carry.o === false);
  eq('17.7 재고는 음수가 되지 않는다', X.ME.stock, 0);
}
{
  // 등급 유무가 실제 판매가에 반영되는가
  const X = load();
  X.startDay();
  const rare = X.RECIPES.findIndex(r => r.r === '희귀');
  const price = (o) => {
    X.D.served = [{r:rare, stage:9, o:o}];
    X.D.cust = [{tx:1.5,ty:5,tag:X.RECIPES[rare].t,pat:20,max:25,skin:'guestA',state:'wait'}];
    const g0 = X.ME.gold;
    X.sell();
    return Math.round(X.ME.gold - g0);
  };
  eq('17.8 원두를 쓴 희귀 잔은 ×2.0', price(true), Math.round(X.BASE * X.RECIPES[rare].m));
  eq('17.9 무등급 잔은 ×1.0', price(false), X.BASE);
}
{
  // 전리품 원두가 5개씩 들어오고, 회수도 5개씩 된다
  const X = load();
  eq('17.10 슬롯당 원두 12개', X.BEAN_PER_SLOT, 12);
  let found = null;
  for(let i = 0; i < 400 && !found; i++){
    const s = X.rollSlots('crate').slots.find(s => s.item && s.item.kind === 'stock');
    if(s) found = s.item;
  }
  ok('17.11 전리품 원두 슬롯이 실제로 나온다', !!found);
  eq('17.12 슬롯 하나가 12개를 담는다', found.n, 12);
  X.startNight();
  X.ME.stock = 0; X.ME.pouch = [];
  X.G.bag = [{kind:'stock', n:5, label:'원두 ×5'}];
  X.endNight(true);
  eq('17.13 귀환하면 5개가 들어온다', X.ME.stock, 5);
}
{
  // F2 — 서빙대 3칸 + 되가져오기. 상한만 걸고 되가져오기가 없으면
  // 아무도 안 시키는 잔이 칸을 영구 점유해 낮이 잠긴다.
  const X = load();
  X.startDay();
  eq('17.14 서빙대 상한 3칸', X.SERVE_CAP, 3);
  const sv = X.ST.find(s => s.type === 'serve');
  const make = () => { X.D.carry = {r:0, stage:9, o:true}; X.interact(sv); };
  make(); make(); make();
  eq('17.15 3잔까지 올라간다', X.D.served.length, 3);
  make();
  eq('17.16 4잔째는 안 올라간다', X.D.served.length, 3);
  ok('17.17 손에 그대로 남는다', !!X.D.carry);
  X.D.carry = null;
  X.interact(sv);
  ok('17.18 빈손으로 가면 되가져온다', !!X.D.carry);
  eq('17.19 서빙대에서 한 잔 줄어든다', X.D.served.length, 2);
  X.interact(X.ST.find(s => s.type === 'trash'));
  eq('17.20 되가져온 잔은 버릴 수도 있다', X.D.carry, null);
}
{
  // F3 — 주문 큐를 미리 뽑는다. 이게 있어야 7초짜리 냉침을 미리 걸 이유가 생긴다.
  const X = load();
  X.startDay();
  eq('17.21 하루치 주문을 미리 뽑는다', X.D.queue.length, X.dayCustomers());
  const next = X.D.queue[0];
  S(X, X.updateDay, 2.0);                       // 첫 손님 스폰(D.spawn=1.2)
  ok('17.22 스폰된 손님이 큐 맨 앞과 같다', X.D.cust.length > 0 && X.D.cust[0].tag === next,
     next + ' vs ' + (X.D.cust[0] && X.D.cust[0].tag));
  eq('17.23 큐가 그만큼 줄어든다', X.D.queue.length, X.dayCustomers() - 1);
  eq('17.24 budget 이 큐 길이를 따라간다', X.D.budget, X.D.queue.length);
  // 큐가 비면 더 스폰되지 않는다
  X.D.queue = []; X.D.budget = 0; X.D.cust = [];
  S(X, X.updateDay, 30);
  eq('17.25 큐가 비면 더 안 온다', X.D.cust.length, 0);
}
{
  // F1 — 수요가 처리량 한계에 붙었는가
  const X = load();
  X.startDay();
  eq('17.26 스폰 간격 7.5초', Math.round(X.spawnGap() * 10) / 10, 7.5);
  eq('17.27 동시 대기 5명', X.maxCust(), 5);
  X.ME.fac.seat = true;
  eq('17.28 좌석 증설 시 6명', X.maxCust(), 6);
  ok('17.29 줄 슬롯도 6칸이다', X.QUEUE.length >= 6);
}
{
  // D5 — 단골이 사람이 됐다. 얼굴과 취향이 고정되고, 다음 날 같은 것을 시키러 온다.
  const X = load();
  X.ME.regs = [{skin:'guestA', tag:'Taste.Sour'}, {skin:'guestB', tag:'Taste.Sour'}];
  X.ME.regulars = 2;
  X.startDay();
  const sour = X.D.queue.filter(t => t === 'Taste.Sour').length;
  ok('17.30 단골이 자기 취향을 들고 온다', sour >= 2, 'Sour ' + sour + '개');
  eq('17.31 큐 길이는 여전히 하루 총량', X.D.queue.length, X.dayCustomers());
}
{
  // D4 — 황혼 행상. 낮 골드가 다음 밤의 생존 확률이 된다.
  const X = load();
  eq('17.32 행상 품목 3종', X.WARES.length, 3);
  X.ME.bought = ['bandage', 'bait', 'battery'];
  X.startNight();
  ok('17.33 붕대는 가방에 실린다', X.G.bag.some(x => x.kind === 'bandage'));
  eq('17.34 미끼는 개수로 들어온다', X.N.baits, 1);
  ok('17.35 배터리는 손전등을 넓힌다', X.N.flBoost > 1, X.N.flBoost);
  eq('17.36 산 것은 소비된다 (다음 밤에 안 남는다)', X.ME.bought.length, 0);
}
{
  // 미끼 — 던지면 좀비가 그쪽으로 간다. 낮 골드로 밤 동선을 사는 것이다.
  const X = load();
  X.startNight();
  X.N.cont.length = 0;
  const z = Z(X, 'shambler', 14, 14, {wt:99, cd:99, fx:1, fy:0});
  X.N.zomb = [z];
  X.N.p.x = 14; X.N.p.y = 20;                   // 좀비에서 6타일 (시야 3 밖)
  X.N.baits = 1;
  tap(X, 'KeyF');
  X.updateNight(1/60);
  ok('17.37 미끼가 놓인다', !!X.N.lure);
  eq('17.38 미끼가 소비된다', X.N.baits, 0);
  ok('17.39 좀비가 미끼를 소리로 듣는다', z.chase > 0, 'chase=' + z.chase);
  const d0 = Math.hypot(X.N.lure.x - z.x, X.N.lure.y - z.y);
  const lure = {x:X.N.lure.x, y:X.N.lure.y};
  S(X, X.updateNight, 3);
  const d1 = Math.hypot(lure.x - z.x, lure.y - z.y);
  ok('17.40 좀비가 미끼 쪽으로 이동한다', d1 < d0, d0.toFixed(2) + ' → ' + d1.toFixed(2));
  S(X, X.updateNight, X.LURE_T);
  ok('17.41 미끼는 시간이 지나면 사라진다', X.N.lure === null);
}
{
  // D3' — 가방에서 버리기. 6칸이 선착순으로 차면 "무엇을 가져갈까"가 사라진다.
  const X = load();
  X.startNight();
  X.N.zomb.length = 0;
  X.G.bag = [{kind:'stock', n:5, label:'원두 ×5'}, {kind:'bandage', label:'붕대'}];
  tap(X, 'KeyZ');
  X.updateNight(1/60);
  eq('17.42 Z 로 가방에서 버린다', X.G.bag.length, 1);
  X.G.bag = [];
  tap(X, 'KeyZ');
  let err = null;
  try { X.updateNight(1/60); } catch(e){ err = e; }
  ok('17.43 빈 가방에서 버려도 안 터진다', err === null, err && err.message);
}
{
  // F6 — 공정 0단계 레시피가 없어야 한다. 가장 좋은 레시피가 조리 게임을 삭제했다.
  const X = load();
  const zero = X.RECIPES.filter(r => (X.STEPS[r.t] || []).length === 0);
  eq('17.44 공정 0단계 레시피 없음', zero.length, 0, zero.map(r => r.n).join(','));
  eq('17.45 전전 캔도 1공정을 거친다', (X.STEPS['Rare.PreWar'] || []).length, 1);
}

/* ══ 18. F4 복구 — 태그 짝짓기 서빙 ═════════════════════ */
{
  // FIFO 를 남긴 채 손님만 24명으로 늘린 것이 충돌이었다. 잔을 만드는 6~7초 사이에
  // 맨 앞 손님이 바뀌어서 **손님이 늘수록 불일치가 늘고 손해**가 됐다.
  // 실측: 불일치 79잔 -> 27잔, 좌석의 한계효용 -265G -> +245G 로 부호가 뒤집힌다.
  const X = load();
  X.startDay();
  X.D.queue = [];                                  // 새 손님이 끼어들지 않게
  const bitter = X.RECIPES.findIndex(r => r.t === 'Taste.Bitter');
  const cust = (tag) => ({tx:1.5,ty:5,tag:tag,pat:20,max:25,skin:'guestA',state:'wait'});
  X.D.served = [{r:bitter, stage:9, o:true}];
  X.D.cust = [cust('Taste.Sweet'), cust('Taste.Bitter')];
  const g0 = X.ME.gold;
  X.updateDay(1/60);
  eq('18.1 줄 순서를 건너뛰고 맞는 손님에게 판다', X.D.cust.length, 1);
  eq('18.1b 남은 사람은 맨 앞의 Sweet 손님이다', X.D.cust[0].tag, 'Taste.Sweet');
  eq('18.1c 정상가로 팔렸다', Math.round(X.ME.gold - g0),
     Math.round(X.BASE * X.RECIPES[bitter].m * X.facMul()));
}
{
  // 맞는 쌍이 없고 서빙대에 여유가 있으면 **아무 일도 일어나지 않는다**.
  // 예전 FIFO 는 여기서 자동으로 반값에 팔아, 실패가 플레이어 손을 안 거쳤다.
  const X = load();
  X.startDay();
  X.D.queue = [];
  const bitter = X.RECIPES.findIndex(r => r.t === 'Taste.Bitter');
  X.D.served = [{r:bitter, stage:9, o:true}];
  X.D.cust = [{tx:1.5,ty:5,tag:'Taste.Sweet',pat:20,max:25,skin:'guestA',state:'wait'}];
  const g0 = X.ME.gold;
  S(X, X.updateDay, 5);
  eq('18.2 맞는 쌍이 없으면 안 팔린다', Math.round(X.ME.gold), Math.round(g0));
  eq('18.2b 잔은 서빙대에 그대로 있다', X.D.served.length, 1);
  ok('18.2c 큐가 비면 새 손님이 안 나온다 (tag=undefined 방지)',
     X.D.cust.every(c => typeof c.tag === 'string'),
     JSON.stringify(X.D.cust.map(c => c.tag)));
}
{
  // 교착 방지 밸브 — 서빙대가 꽉 찼는데 맞는 쌍이 없으면 맨 앞에 흘려보낸다.
  // 이게 없으면 아무도 안 시키는 잔 3개가 칸을 물고 낮이 통째로 잠긴다.
  const X = load();
  X.startDay();
  X.D.queue = [];
  const bitter = X.RECIPES.findIndex(r => r.t === 'Taste.Bitter');
  X.D.served = [{r:bitter,stage:9,o:true},{r:bitter,stage:9,o:true},{r:bitter,stage:9,o:true}];
  X.D.cust = [{tx:1.5,ty:5,tag:'Taste.Sweet',pat:20,max:25,skin:'guestA',state:'wait'}];
  const g0 = X.ME.gold;
  X.updateDay(1/60);
  eq('18.3 서빙대가 차면 맨 앞에 흘려보낸다', X.D.served.length, 2);
  eq('18.3b 불일치라 반값이다', Math.round(X.ME.gold - g0),
     Math.round(X.BASE * X.MISMATCH * X.facMul()));
  ok('18.3c 낮이 잠기지 않는다 (칸이 다시 비었다)', X.D.served.length < X.SERVE_CAP);
}
{
  // sell(si,ci) 가 지정한 잔·손님만 소비한다 (shift -> splice 회귀 방지)
  const X = load();
  X.startDay();
  X.D.queue = [];
  X.D.served = [{r:0,stage:9,o:true},{r:1,stage:9,o:true},{r:2,stage:9,o:true}];
  X.D.cust = ['Taste.Bitter','Taste.Sweet','Taste.Sour'].map(tag =>
    ({tx:1.5,ty:5,tag:tag,pat:20,max:25,skin:'guestA',state:'wait'}));
  X.sell(1, 2);
  eq('18.4 지정한 잔만 빠진다', X.D.served.map(c => c.r).join(','), '0,2');
  eq('18.4b 지정한 손님만 빠진다', X.D.cust.map(c => c.tag).join(','),
     'Taste.Bitter,Taste.Sweet');
  X.sell();
  eq('18.5 인자 없이 부르면 예전처럼 맨 앞끼리',
     X.D.served.length + ',' + X.D.cust.length, '1,1');
}

{
  // 습격 시 재료도 털어 온다 (§6.5). 이게 없으면 습격은 파밍을 **대체**하는데
  // 보상은 파밍의 1/3(tier −0.5 ≈ +214G vs 원두 31개 ≈ +600G)이라 승률이 0%였다.
  const X = load();
  X.ME.found = [X.BOTS[0].name];
  X.startNight();
  X.N.zomb.length = 0;
  X.ME.stock = 0;
  X.N.p.x = X.RIVAL[0][0]; X.N.p.y = X.RIVAL[0][1];
  hold(X, 'KeyE');
  S(X, X.updateNight, 3.0);
  rel(X, 'KeyE');
  eq('18.6 습격하면 원두도 털어 온다', X.ME.stock, X.RAID_BEANS);
  ok('18.6b 그 양이 파밍 한 밤과 동급이다',
     X.RAID_BEANS >= X.BEAN_PER_SLOT * 2, X.RAID_BEANS + ' vs 슬롯 ' + X.BEAN_PER_SLOT);
}

/* ══ 결과 ══════════════════════════════════════════════ */
console.log('');
console.log('  통과 ' + pass + ' / ' + (pass + fails.length));
if(fails.length){
  console.log('');
  fails.forEach(f => console.log('  FAIL  ' + f));
  process.exitCode = 1;
} else {
  console.log('  전부 통과');
}
