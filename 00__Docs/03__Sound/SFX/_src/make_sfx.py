"""Blood & Bean 효과음 생성 — 실행하면 상위 폴더(SFX/)에 wav를 다시 만든다.
   python make_sfx.py            (numpy · scipy 필요)
   소리를 고치려면 해당 블록의 숫자만 바꾸고 다시 실행한다. 코드 번호는 효과음 리스트의 S 번호다."""
from sfx_synth import *
def resample(x,r): return signal.resample(x,int(len(x)*r))

# ── S-06 ~ S-08 판정 세트 (같은 악기, 음높이만 다르게)
p=mix((0,marimba(note(76),.5)),(0.075,marimba(note(83),.6)),(0.075,fm_bell(note(95),.7,ratio=2.0,idx=1.5,dec=.4),.35),
      (0.12,fm_bell(note(100),.5,ratio=3.01,idx=1,dec=.25),.2))
save('SFX_S06_Perfect',verb(p,.22),-2)
save('SFX_S07_Good',verb(marimba(note(76),.45),.15),-4)
save('SFX_S08_Miss',verb(lp(mix((0,marimba(note(72),.35,.12)),(0.09,marimba(note(67),.5,.16))),2500),.12),-5)

# ── S-10 판매 (코인 없이) · S-11 코인 흡수 — 따로 둔다. S-22는 S-10만 작게 울린다
sale=mix((0,fm_bell(note(84),.8,ratio=2.0,idx=2,dec=.35)),(0.06,fm_bell(note(88),.8,ratio=2.0,idx=2,dec=.35)),(0.12,fm_bell(note(91),1.0,ratio=2.0,idx=2,dec=.45)))
save('SFX_S10_Sale',verb(sale,.2),-2)
coin=mix(*[(i*0.045, fm_bell(note(98+(i%2)*3),.25,ratio=5.4,idx=3,dec=.08),.5) for i in range(4)])
save('SFX_S11_Coin',verb(coin,.15),-5)

# ── S-62 「안 됨」 (S-17 설비 사용 중도 이것)
x=t(.22); ph=sweep(220,120,.22)
s=np.sin(ph)*np.exp(-x/.06)+.3*np.sin(2*ph)*np.exp(-x/.03)+.15*lp(noise(.22),800)*np.exp(-x/.02)
save('SFX_S62_NotAllowed',s,-4)

# ── S-04 게이지 등장 · S-05 침 틱 (한 번. 게이지가 떠 있는 동안 코드가 반복 재생)
save('SFX_S04_GaugeDing',fm_bell(note(88),1.2,ratio=1.414,idx=1.2,dec=.5),-3)
tk=hp(noise(.012),3000)*np.exp(-t(.012)/.003)*.5+np.sin(2*np.pi*2400*t(.012))*np.exp(-t(.012)/.004)*.4
save('SFX_S05_GaugeTick',tk,-9)

# ── S-01 재료 담기 — 원본 3개 (재료별 음높이는 코드에서)
glug=np.sin(sweep(500,1100,.1))*np.exp(-t(.1)/.03)
hard=mix((0,np.sin(sweep(1800,1500,.06))*np.exp(-t(.06)/.012)),(0.035,np.sin(sweep(2300,2000,.05))*np.exp(-t(.05)/.01),.6),(0,bp(noise(.03),3000,8000)*np.exp(-t(.03)/.006),.4))
soft=np.sin(sweep(250,500,.15))*np.exp(-t(.15)/.05)+.3*lp(noise(.15),1200)*np.exp(-t(.15)/.04)
save('SFX_S01a_Ingredient_Liquid',glug,-5)
save('SFX_S01b_Ingredient_Hard',hard,-5)
save('SFX_S01c_Ingredient_Soft',soft,-5)

# ── S-14 쓰레기가 됨
n=noise(.35); s=tv_lp(n,6000,500)*adsr(len(n),.01,.03,.08)+.5*np.sin(sweep(400,150,.35))*np.exp(-t(.35)/.08)
save('SFX_S14_Spoiled',s,-5)

# ── S-16 건네기 / 받기 (맞바꾸기도 같은 소리)
h=mix((0,marimba(note(79),.3,.1)),(0.13,marimba(note(84),.4,.14)),(0,np.sin(sweep(600,1200,.15))*np.exp(-t(.15)/.05),.25))
save('SFX_S16_HandOff',verb(h,.12),-4)

# ── S-27 캐릭터 충돌 (S-60 벽 충돌은 이것을 pitch 0.8 + 저역 통과)
x=t(.3); s=np.sin(sweep(180,110,.3))*np.exp(-x/.09)*(1+.35*np.sin(2*np.pi*18*x))+.3*lp(noise(.3),500)*np.exp(-x/.02)
save('SFX_S27_Bump',s,-4)

# ── S-37 대시 (낮 · 밤 공용)
n=noise(.4); save('SFX_S37_Dash',tv_bp(n,500,3500,1.2)*np.sin(np.pi*np.linspace(0,1,len(n)))**1.5,-4)

# ── S-33 블러드 빈 등장 · S-58 블러드 빈 담기(첫 박동만 — 구워 둔다)
x=t(1.6)
boom=np.sin(sweep(90,38,1.6))*np.exp(-x/.5)+.4*lp(noise(1.6),200)*np.exp(-x/.3)
def beat():
    y=t(.25); return np.sin(sweep(70,45,.25))*np.exp(-y/.06)
hb=mix((0.35,beat()),(0.55,beat(),.7),(1.05,beat(),.9),(1.25,beat(),.6))
save('SFX_S33_BloodBean',verb(mix((0,boom),(0,hb),(0,fm_bell(note(61),1.6,ratio=1.5,idx=3,dec=.6)*.12)),.3,1.4),-1)
save('SFX_S58_BloodBeanPour',mix((0,beat()),(0,lp(noise(.25),150)*np.exp(-t(.25)/.05),.3)),-8)

# ── S-34 보석 벨 · S-26 보석 만료(거꾸로 — 구워 둔다). S-25 보석 적용은 S-34를 pitch로
gem=verb(mix((0,fm_bell(note(93),1.2,ratio=3.0,idx=2,dec=.5)),(0.05,fm_bell(note(100),1.0,ratio=3.0,idx=1.5,dec=.4),.6)),.3)
save('SFX_S34_Gem',gem,-3)
save('SFX_S26_GemExpire',gem[::-1],-6)

# ── S-12 오배송 — 임시. 목소리는 합성의 한계라 녹음 · AI 음성으로 바꿀 것
x=t(.5); f0=np.interp(x,[0,.15,.5],[180,240,140]); ph=np.cumsum(f0)/SR*2*np.pi
src=signal.sawtooth(ph)*adsr(len(x),.02,.25,.1)
v=bp(src,500,900)+bp(src,1000,1400)*.6+bp(src,2400,2800)*.25
buzz=lp(signal.square(2*np.pi*110*t(.35))*.25,900)*adsr(int(SR*.35),.01,.25,.05)
save('SFX_S12_Misdelivery_TEMP',mix((0,v),(0.25,buzz)),-4)
