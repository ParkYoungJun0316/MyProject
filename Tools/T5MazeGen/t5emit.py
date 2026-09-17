import json, sys
N=12; S=100/N
d=json.load(open('t5maps.json'))
out=[]
def cx(c): return -50+S*(c+0.5)
def lk(k): return -50+S*k
def runs(idx):
    idx=sorted(idx); res=[]
    for i in idx:
        if res and res[-1][1]==i-1: res[-1][1]=i
        else: res.append([i,i])
    return res
for mi,m in enumerate(d['maps']):
    out.append(f"MAP {mi+1}")
    V={k:set() for k in range(N+1)}; H={k:set() for k in range(N+1)}
    for r in range(N): V[0].add(r); V[N].add(r)
    for c in range(N): H[0].add(c); H[N].add(c)
    for a,b in m['walls']:
        (r1,c1),(r2,c2)=a,b
        if r1==r2: V[max(c1,c2)].add(r1)
        else: H[max(r1,r2)].add(c1)
    for k,rows in V.items():
        for r0,r1 in runs(rows):
            out.append(f"WALL WallV_{k:02d}_{r0:02d} {lk(k):.3f} {-50+S*(r0+r1+1)/2:.3f} 0.5 {S*(r1-r0+1)+0.5:.3f}")
    for k,cols in H.items():
        for c0,c1 in runs(cols):
            out.append(f"WALL WallH_{k:02d}_{c0:02d} {-50+S*(c0+c1+1)/2:.3f} {lk(k):.3f} {S*(c1-c0+1)+0.5:.3f} 0.5")
    cnt={}
    for dd in m['doors']:
        (r1,c1),(r2,c2)=dd['a'],dd['b']; col=dd['color']; cnt[col]=cnt.get(col,0)+1
        nm=f"Door_{col}_{cnt[col]:02d}"+("_Path" if dd['onPath'] else "")
        if r1==r2: out.append(f"DOOR {col} {nm} {lk(max(c1,c2)):.3f} {cx(r1):.3f} 0.6 8.233")
        else: out.append(f"DOOR {col} {nm} {cx(c1):.3f} {lk(max(r1,r2)):.3f} 8.233 0.6")
    for p in m['pads']:
        r,c=p['cell']; out.append(f"PAD {p['color']} {cx(c):.3f} {cx(r):.3f}")
    for r,c in m['spawns']: out.append(f"SPAWN {cx(c):.3f} {cx(r):.3f}")
    r,c=m['start']; out.append(f"START {cx(c):.3f} {cx(r):.3f}")
    r,c=m['goal']; out.append(f"GOAL {cx(c):.3f} {cx(r):.3f}")
open('t5layout.txt','w').write("\n".join(out)+"\n")
print(len(out))
