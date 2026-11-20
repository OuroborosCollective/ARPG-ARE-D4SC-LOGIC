"""Projection bones and smoothly blended source weights in the creature's neutral stance."""
import numpy as np
from tief_form import all_regions, nodes, smooth, clamp

def basis(brand,brand_normal):
    root=[('TS_Root',None,(0,0,0),(0,0,.24)),
          ('TS_Pelvis','TS_Root',(0,-.83,.622),(0,-.39,.667)),
          ('TS_Spine','TS_Pelvis',(0,-.39,.667),(0,.06,.744)),
          ('TS_Chest','TS_Spine',(0,.06,.744),(0,.413,.73)),
          ('TS_Neck','TS_Chest',(0,.413,.73),(0,.731,.686)),
          ('TS_Head','TS_Neck',(0,.731,.686),(0,1.258,.551)),
          ('TS_Jaw','TS_Head',(0,.803,.514),(0,1.215,.455)),
          ('TS_Tail','TS_Pelvis',(0,-1.14,.642),(.103,-1.754,.470)),
          ('TS_Nose','TS_Head',(0,1.376,.528),(0,1.47,.528)),
          ('TS_Brand','TS_Neck',tuple(brand),tuple(np.asarray(brand)+np.asarray(brand_normal)*.11))]
    for side,s in [('L',-1),('R',1)]:
        p=nodes(s);pr='TS_';suffix='_'+side
        for bone,parent,start,end in [('ForeArm','Chest','shoulder','elbow'),('ForeElbow','ForeArm','elbow','wrist'),
                ('ForePaw','ForeElbow','wrist','digits'),('HindHip','Pelvis','hip','knee'),
                ('HindHock','HindHip','knee','hock'),('HindPaw','HindHock','hock','hindpaw')]:
            par=pr+parent+('' if parent in ['Chest','Pelvis'] else suffix)
            root.append((pr+bone+suffix,par,p[start],p[end]))
    return root

def influences(vertices,bones):
    p=np.asarray(vertices,np.float32);x,y,z=p.T
    regions=np.stack(all_regions(x,y,z),axis=1);rr=regions.min(axis=1)
    aff=np.exp(-np.clip((regions-rr[:,None])/.05,0,38));aff/=aff.sum(axis=1)[:,None]
    keys=[b[0] for b in bones];w=np.zeros((len(x),len(keys)),dtype=np.float32)
    def plus(name,a):w[:,keys.index('TS_'+name)]+=a
    longitudinal=[-1.55,-.90,-.29,.15,.54,.91]
    centers=['Tail','Pelvis','Spine','Chest','Neck','Head']
    a=aff[:,0]
    for j,name in enumerate(centers):
        left=1. if j==0 else smooth((y-longitudinal[j-1])/(longitudinal[j]-longitudinal[j-1]))
        right=1. if j==len(centers)-1 else 1-smooth((y-longitudinal[j])/(longitudinal[j+1]-longitudinal[j]))
        bw=a*left*right
        if name=='Head':
            jaw=smooth((.538-z)/.085)*smooth((y-.8)/.17)*(1-smooth((y-1.27)/.08))*.84
            plus('Jaw',bw*jaw);bw=bw*(1-jaw)
        plus(name,bw)
    plus('Tail',aff[:,1])
    for j,(s,side) in enumerate([(-1,'L'),(1,'R')]):
        n=nodes(s)
        for fore,region in [(True,2+2*j),(False,3+2*j)]:
            if fore:
                names=['ForeArm','ForeElbow','ForePaw'];fk=['shoulder','elbow','wrist','digits']
            else:
                names=['HindHip','HindHock','HindPaw'];fk=['hip','knee','hock','hindpaw']
            joints=np.array([n[k] for k in fk],dtype=np.float32);segments=np.diff(joints,axis=0)
            dims=np.linalg.norm(segments,axis=1)
            param=[];dist=[];base=0
            for k in range(3):
                t=clamp(((p-joints[k])@segments[k])/(dims[k]**2))
                dv=p-joints[k]-t[:,None]*segments[k]
                dist.append((dv*dv).sum(axis=1));param.append(base+t*dims[k]);base+=dims[k]
            d=np.stack(dist,axis=1);v=np.stack(param,axis=1)
            proximity=np.exp(-np.clip((d-d.min(axis=1)[:,None])/.018,0,30))
            h=(v*proximity).sum(axis=1)/proximity.sum(axis=1)
            a=aff[:,region];met1=smooth((h-dims[0]+.092)/.184);met2=smooth((h-dims[0]-dims[1]+.067)/.134)
            plus(names[0]+'_'+side,a*(1-met1))
            plus(names[1]+'_'+side,a*met1*(1-met2))
            plus(names[2]+'_'+side,a*met2)
    # The sensory organ has one rigid head reference, so the eight pads do not slide under a jaw event.
    nose=(y>1.311)&(np.abs(x)<.22)&(np.abs(z-.53)<.22)
    w[nose]=0;w[nose,keys.index('TS_Head')]=1
    ix=np.argsort(-w,axis=1,kind='stable')[:,:4];v=np.take_along_axis(w,ix,axis=1)
    v[v<.0015]=0;v/=v.sum(axis=1)[:,None]
    assert np.isfinite(v).all() and (v.max(axis=1)>.25-.0001).all()
    return ix,v
