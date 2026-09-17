import json, t5gen
N=12
d=json.load(open('t5maps.json'))['maps']
maps=[]; cur=None
for line in open('scene_dump.txt'):
    f=line.split()
    if not f: continue
    if f[0]=='M': cur={'walls':set(),'doors':{}}; maps.append(cur)
    elif f[0]=='W':
        k,a,b=int(f[2]),int(f[3]),int(f[4])
        for i in range(a,b+1):
            if f[1]=='V':
                if 0<k<N: cur['walls'].add(t5gen.ekey((i,k-1),(i,k)))
            else:
                if 0<k<N: cur['walls'].add(t5gen.ekey((k-1,i),(k,i)))
    elif f[0]=='D':
        k,i=int(f[3]),int(f[4])
        e=t5gen.ekey((i,k-1),(i,k)) if f[2]=='V' else t5gen.ekey((k-1,i),(k,i))
        cur['doors'][e]=f[1]
    elif f[0]=='S':
        cur['s']=(int(f[1]),int(f[2])); cur['g']=(int(f[3]),int(f[4]))
allok=True
for i,(m,ref) in enumerate(zip(maps,d)):
    open_edges={e for e in t5gen.ALL_EDGES if e not in m['walls']}
    refw={tuple(map(tuple,e)) for e in ref['walls']}
    refd={t5gen.ekey(tuple(x['a']),tuple(x['b'])):x['color'] for x in ref['doors']}
    same = refw==m['walls'] and refd==m['doors'] and tuple(ref['start'])==m['s'] and tuple(ref['goal'])==m['g']
    res={}
    for label,cmap in t5gen.MAPPINGS:
        sw=t5gen.min_switches(open_edges,m['doors'],cmap,m['s'],m['g'])
        lo,hi=res.get(label,(99,-1)); res[label]=(min(lo,sw if sw is not None else -1),max(hi,sw if sw is not None else -1))
    solo=t5gen.min_switches(open_edges,{},{},m['s'],m['g'])
    ok= same and all(8<=a and b<=12 for a,b in res.values()) and solo==0
    allok&=ok
    print(f"Map_{i+1:02d} matchM1={same} doors={len(m['doors'])} sw={res} solo={solo} -> {'PASS' if ok else 'FAIL'}")
print('ALL PASS' if allok else 'SOME FAIL')
