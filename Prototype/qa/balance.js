// 낮을 '완벽하게' 자동 플레이한다. QA 회전마다 같은 잣대로 재기 위해 분리해 둔다.
const {load}=require('./harness.js');
function autoDay(X){
  const at=t=>X.ST.find(s=>s.type===t);
  let guard=0;
  while(X.G.phase==='day' && guard++<4000){
    const c=X.D.cust.find(c=>c.state==='wait')||X.D.cust[0];
    let idx=X.ME.menu.findIndex(v=>v>=0); if(idx<0) idx=0;
    if(c) for(let i=0;i<X.menuCap();i++){ const r=X.ME.menu[i]; if(r>=0 && X.RECIPES[r].t===c.tag){ idx=i; break; } }
    X.D.recipe=idx;
    const sh=at('shelf'); X.D.p.x=sh.tx; X.D.p.y=sh.ty; X.interact(sh); X.updateDay(1/20);
    if(!X.D.carry){ X.updateDay(1/20); continue; }
    for(const k of X.stepsOf(X.D.carry.r)){
      const st=at(k); X.D.p.x=st.tx; X.D.p.y=st.ty; X.interact(st);
      for(let i=0;i<Math.ceil(X.PROC[k].t*20)+2;i++){ X.updateDay(1/20); if(X.G.phase!=='day') return; }
      X.interact(st);
    }
    const sv=at('serve'); X.D.p.x=sv.tx; X.D.p.y=sv.ty; X.interact(sv);
    if(X.D.carry) X.interact(at('trash'));      // 서빙대가 차 있으면 버린다
    X.updateDay(1/20);
  }
}
function run(sortie, runs){
  let win=0, mine=[], top=[];
  for(let r=0;r<runs;r++){
    const X=load(); X.ME.day=1; X.startDay();
    for(let g=0; g<80; g++){
      if(X.G.phase==='day') autoDay(X);
      else if(X.G.phase==='dusk'){
        for(const id of ['seat','pack','roast']) if(!X.ME.fac[id]){ const f=X.FACIL.find(v=>v.id===id); if(f&&X.ME.gold>=f.c){ X.ME.gold-=f.c; X.ME.fac[id]=true; } }
        // 주워 온 레시피를 배수 내림차순으로 등록한다. 이게 없으면 밤에 뭘 가져와도
        // 낮 매출이 안 변해서, 밤의 값을 0으로 두고 비교하게 된다.
        const best=X.ME.owned.slice().sort((a,b)=>X.RECIPES[b].m-X.RECIPES[a].m).slice(0,X.menuCap());
        for(let i=0;i<X.ME.menu.length;i++) X.ME.menu[i]=(i<best.length?best[i]:-1);
        if(sortie==='stay'){ X.G.bag=[]; X.endNight(true,true); }
        else { X.G.sortie=sortie; X.startNight(); }
      }
      else if(X.G.phase==='night'){
        X.N.zomb.length=0;   // 경제를 재는 시뮬이다 - 전투 실력이 아니라 루팅 경제를 본다
        if(sortie==='raid'){
          // 실제로 라이벌 카페까지 가서 E 를 홀드한다. 이게 없으면 'raid' 는
          // "밤에 나갔다가 아무것도 안 하고 돌아온 케이스"를 재게 된다.
          X.ME.found=X.BOTS.map(b=>b.name);
          for(const rv of X.RIVAL){
            X.N.p.x=rv[0]; X.N.p.y=rv[1]; X.keys['KeyE']=true;
            for(let t=0;t<200 && X.G.phase==='night';t++) X.updateNight(1/30);
            X.keys['KeyE']=false;
          }
        }
        if(sortie==='farm'||sortie==='farmsafe'){ // 컨테이너를 순회하며 턴다
          for(const c of X.N.cont.slice(0,8)){
            X.N.p.x=c.x; X.N.p.y=c.y; X.N.open=null; X.N.searchHold=0; X.keys['KeyE']=true;
            for(let t=0;t<200 && X.G.phase==='night';t++){
              X.updateNight(1/30);
              // 공개된 슬롯을 실제로 집는다. 이게 없으면 밤의 보상이 0이 된다.
              if(X.N.open===c && c.pick<0){
                const i=c.slots.findIndex(s=>s.rev>=1 && s.item && !s.taken);
                if(i>=0) X.pickSlot(i);
              }
              if(X.G.bag.length>=X.bagCap()) break;
            }
            X.keys['KeyE']=false;
            if(X.G.bag.length>=X.bagCap()) break;
          }
        }
        if(sortie==='farmsafe'){ X.endNight(true,true); continue; }   // 습격 판정만 제외
        X.N.p.x=X.HOME[0]; X.N.p.y=X.HOME[1]+0.7;
        X.pressed['KeyE']=true; X.updateNight(1/60);
        if(X.G.phase==='night'){ for(let t=0;t<180*30 && X.G.phase==='night';t++) X.updateNight(1/30); }
      }
      else if(X.G.phase==='settle'){ if(X.ME.day>=X.DAYS) break; X.ME.day++; X.startDay(); }
      else break;
    }
    const rk=X.ranked(); if(rk[0].me) win++;
    mine.push(X.ME.sales); top.push(Math.max(...X.BOTS.map(b=>b.sales)));
  }
  const avg=a=>Math.round(a.reduce((x,y)=>x+y,0)/a.length);
  return {sortie, mine:avg(mine), top:avg(top), win:(win/runs*100).toFixed(1)};
}
for(const s of (process.argv[2]?process.argv[2].split(','):['farm','farmsafe','raid','stay'])){
  const r=run(s, 40);
  console.log(('  '+r.sortie).padEnd(8), '내', String(r.mine).padStart(5)+'G', '/ 봇1위', String(r.top).padStart(5)+'G', '/ 승률', r.win+'%');
}
