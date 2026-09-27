"""Blood & Bean 효과음 합성 도구 — 공용 함수. make_sfx.py가 이것을 쓴다."""
import os
import numpy as np
from scipy import signal
from scipy.io import wavfile
SR=44100
OUT=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..')
rng=np.random.default_rng(7)
def t(d): return np.arange(int(SR*d))/SR
def env(n,a=.005,d=.2,curve=6.0):
    x=np.arange(n)/SR; e=np.minimum(1,x/max(a,1e-4))*np.exp(-np.maximum(0,x-a)/max(d,1e-4)*1.0)
    return e
def adsr(n,a,h,r):
    x=np.arange(n)/SR; e=np.clip(x/a,0,1)
    e=np.where(x>a+h,np.exp(-(x-a-h)/r),e); return e
def sweep(f0,f1,d,k='exp'):
    x=t(d); 
    f=f0*(f1/f0)**(x/d) if k=='exp' else f0+(f1-f0)*x/d
    return np.cumsum(f)/SR*2*np.pi
def sine(ph): return np.sin(ph)
def fm_bell(f,d,ratio=3.5,idx=2.5,dec=.35,bright=1.0):
    x=t(d); ie=idx*np.exp(-x/(dec*.5))*bright
    return np.sin(2*np.pi*f*x+ie*np.sin(2*np.pi*f*ratio*x))*env(len(x),.002,dec)
def marimba(f,d=.45,dec=.18):
    x=t(d)
    s=np.sin(2*np.pi*f*x)*np.exp(-x/dec)+.35*np.sin(2*np.pi*f*4*x)*np.exp(-x/(dec*.25))+.12*np.sin(2*np.pi*f*9.9*x)*np.exp(-x/(dec*.1))
    return s*np.minimum(1,x/.002)
def noise(d): return rng.standard_normal(int(SR*d))
def bp(x,lo,hi,o=2): return signal.sosfilt(signal.butter(o,[lo,hi],'band',fs=SR,output='sos'),x)
def lp(x,f,o=2): return signal.sosfilt(signal.butter(o,f,'low',fs=SR,output='sos'),x)
def hp(x,f,o=2): return signal.sosfilt(signal.butter(o,f,'high',fs=SR,output='sos'),x)
def tv_lp(x,f0,f1):
    # time-varying lowpass by blocks
    n=len(x); out=np.zeros(n); B=512; zi=None
    for i in range(0,n,B):
        fc=f0*(f1/f0)**(i/n); sos=signal.butter(2,min(fc,SR*.45),'low',fs=SR,output='sos')
        if zi is None: zi=signal.sosfilt_zi(sos)*0
        out[i:i+B],zi=signal.sosfilt(sos,x[i:i+B],zi=zi)
    return out
def tv_bp(x,f0,f1,q=1.5):
    n=len(x); out=np.zeros(n); B=512; zi=None
    for i in range(0,n,B):
        fc=f0*(f1/f0)**(i/n); lo=fc/(1+1/q); hi=min(fc*(1+1/q),SR*.45)
        sos=signal.butter(2,[lo,hi],'band',fs=SR,output='sos')
        if zi is None: zi=np.zeros((sos.shape[0],2))
        out[i:i+B],zi=signal.sosfilt(sos,x[i:i+B],zi=zi)
    return out
def mix(*parts):
    n=max(len(p[1])+int(p[0]*SR) for p in parts); o=np.zeros(n)
    for off,s,*g in parts:
        i=int(off*SR); o[i:i+len(s)]+=s*(g[0] if g else 1)
    return o
def verb(x,wet=.18,dec=.9):
    ir=noise(dec)*np.exp(-t(dec)/(dec/5)); ir=lp(ir,6000); ir/=np.abs(ir).sum()**.5*8
    y=signal.fftconvolve(x,ir)[:len(x)+int(SR*dec)]
    o=np.zeros(len(y)); o[:len(x)]=x; return o+wet*y/ (np.abs(y).max()+1e-9)*np.abs(x).max()
def fade(x,ms=8):
    n=int(SR*ms/1000); x=x.copy(); x[-n:]*=np.linspace(1,0,n); return x
def save(name,x,peak_db=-3):
    a=np.abs(x)/(np.abs(x).max()+1e-9); idx=np.where(a>10**(-50/20))[0]; x=x[:idx[-1]+int(SR*.01)] if len(idx) else x
    x=fade(x); x=x-np.mean(x); x=x/(np.abs(x).max()+1e-9)*10**(peak_db/20)
    wavfile.write(os.path.join(OUT,f'{name}.wav'),SR,(x*32767).astype(np.int16))
def note(n): return 440*2**((n-69)/12)
