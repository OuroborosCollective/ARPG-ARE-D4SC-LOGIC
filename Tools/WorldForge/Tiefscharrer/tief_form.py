"""Continuous implicit anatomy of the blind quadruped. Coordinates are in metres."""
import math
import numpy as np

F=np.float32
axis_profile=np.array([
    (-1.46,.55,.028,.042),(-1.325,.58,.28,.24),(-1.12,.60,.45,.355),(-.87,.65,.545,.415),
    (-.59,.70,.606,.455),(-.25,.738,.637,.481),(.04,.747,.630,.488),(.27,.747,.549,.461),
    (.47,.727,.477,.414),(.64,.694,.429,.375),(.78,.674,.394,.342),(.92,.642,.366,.295),
    (1.053,.605,.332,.252),(1.160,.571,.282,.201),(1.253,.535,.212,.150),(1.329,.518,.133,.100),(1.375,.522,.057,.071)],dtype=F)
def interp(y,i):
    return np.interp(y,axis_profile[:,0],axis_profile[:,i]).astype(F)
def clamp(a):return np.clip(a,0,1)
def smooth(a):
    a=clamp(a);return a*a*(3-2*a)
def blend(a,b,k):
    h=clamp(.5+.5*(b-a)/k);return b+(a-b)*h-k*h*(1-h)
def ell(x,y,z,rx,ry,rz):
    return (np.sqrt((x/rx)**2+(y/ry)**2+(z/rz)**2)-1)*min(rx,ry,rz)
def segment(x,y,z,first,last,rad,tip,axes=(1,1,1),bulge=0):
    a,b=first,last;sx,sy,sz=axes
    ax,ay,az=(x-a[0])/sx,(y-a[1])/sy,(z-a[2])/sz
    dx,dy,dz=(b[0]-a[0])/sx,(b[1]-a[1])/sy,(b[2]-a[2])/sz
    t=clamp((ax*dx+ay*dy+az*dz)/max(dx*dx+dy*dy+dz*dz,.00001))
    r=rad+(tip-rad)*t+bulge*np.sin(math.pi*t)
    return (np.sqrt((ax-t*dx)**2+(ay-t*dy)**2+(az-t*dz)**2)-r)*min(sx,sy,sz)
def nodes(sign):
    # Each forepaw bears weight. A small asymmetry prevents coincident marks on both strides.
    d=.048 if sign<0 else -.018
    return {'shoulder':(sign*.413,.18,.758),'upper':(sign*.607,.26,.713),'elbow':(sign*.796,.396+d,.488),
      'wrist':(sign*.800,.792+d,.202),'palm':(sign*.765,.940+d,.149),'digits':(sign*.765,1.00+d,.102),
      'hip':(sign*.356,-.847,.65),'knee':(sign*.525,-.671,.425),'hock':(sign*.530,-1.026,.230),
      'heel':(sign*.543,-.974,.126),'hindpaw':(sign*.543,-.792,.080)}
def core(x,y,z):
    cz,rx,rz=(interp(y,i) for i in (1,2,3))
    # A broad shoulder crest and a heavier right cheek are built into the uninterrupted animal surface.
    shift=.013*np.sin(y*4.3)*np.exp(-((y-.03)/1.0)**2)
    px=x-shift
    tone=1+.025*np.sin(7*y+.5)*np.maximum(0,(z-cz)/rz)
    c=(np.sqrt((px/(rx*tone))**2+((z-cz)/rz)**2)-1)*np.minimum(rx,rz)
    c=np.maximum(c,np.maximum(-1.465-y,y-1.384))
    for s in (-1,1):
        c=blend(c,ell(x-s*.195,y-.868,z-.637,.177,.31,.223),.067)
        c=blend(c,segment(x,y,z,(s*.261,.77,.750),(s*.153,1.14,.645),.067,.060,(1,1,.80),.013),.033)
    c=blend(c,ell(x,y-1.049,z-.474,.209,.212,.099),.055)
    # Neck folds run to the beard and break over the shoulders. Their relief persists in the real mesh.
    theta=np.arctan2(x,(z-cz)*1.24+.0001)
    neck=np.exp(-((y-.493)/.309)**4)*smooth((.40-np.abs(theta)*.017)/.37)
    phase=34.5*(y-.505)+3.2*abs(x)-2.0*(z-.65)+.92*np.sin(theta*2.24+1.1)+.27*np.sin(11*x)+.44*np.sin(y*10.7)
    neck*=.68+.32*(.5+.5*np.cos(theta*2.0-1.1))
    c+=neck*(.014*np.sin(phase)+.0065*np.sin(2*phase+.95))
    cheek=np.exp(-((y-.982)/.263)**4)
    c+=cheek*(.0036*np.sin(44*y+19*z+2*np.sin(8*x))+.0027*np.cos(41*x-9*y+3*z))
    flank=np.exp(-((y+.445)/.74)**4)*smooth((np.abs(x)-.29)/.28)
    c+=flank*.007*np.sin(21*y+.8*np.sin(z*12)+5*z)
    return c.astype(F)

def limb(x,y,z,s,fore):
    p=nodes(s)
    if fore:
        a=segment(x,y,z,p['shoulder'],p['upper'],.218,.254,(1.02,1,1.12),.022)
        a=blend(a,segment(x,y,z,p['upper'],p['elbow'],.246,.184,(1.,1.,1.02),.014),.095)
        a=blend(a,segment(x,y,z,p['elbow'],p['wrist'],.188,.130,(1.06,1,.94),.018),.074)
        a=blend(a,segment(x,y,z,p['wrist'],p['palm'],.131,.140,(1.39,1,.76),.022),.057)
        px,py,pz=p['palm'];a=blend(a,ell(x-px,y-py-.042,z-pz+.026,.202,.147,.092),.052)
        for j,(dx,dy,ra) in enumerate([(-.152,-.023,.052),(-.058,.048,.071),(.072,.030,.072),(.163,-.025,.046)]):
            a=blend(a,segment(x,y,z,(px+s*dx*.80,py+.002,.142),(px+s*dx,py+.094+dy,.096),ra*.96,ra,(1,1,.89),.008),.026)
        arm=smooth((y-.38)/.19)*smooth((.95-y)/.15)*smooth((.39-z)/.10)
        a+=(.0075*np.sin(51*y+16*z+4*s*x)+.003*np.cos(51*x+17*y))*arm
    else:
        a=segment(x,y,z,p['hip'],p['knee'],.213,.155,(1.,1.22,.98),.028)
        a=blend(a,segment(x,y,z,p['knee'],p['hock'],.151,.084,(1.,1,.95),.007),.070)
        a=blend(a,segment(x,y,z,p['hock'],p['hindpaw'],.083,.064,(1.27,1,.85),.011),.045)
        px,py,pz=p['hindpaw'];a=blend(a,ell(x-px,y-py-.032,z-pz,.123,.129,.066),.035)
        for dx in [-.070,.0,.075]:
            a=blend(a,segment(x,y,z,(px+s*dx*.72,py+.023,.081),(px+s*dx,py+.136,.060),.039,.029,(1,1,.86)),.018)
    return a.astype(F)

def tail(x,y,z):
    a=segment(x,y,z,(0,-1.13,.654),(.036,-1.45,.55),.175,.103,(1,1,.94))
    return blend(a,segment(x,y,z,(.036,-1.45,.55),(.102,-1.758,.466),.102,.027,(1,1,.84)),.038)

def nose(x,y,z):
    a=ell(x,y-1.309,z-.531,.155,.090,.106)
    for j in range(8):
        angle=math.tau*j/8+.15
        vx,vz=math.sin(angle),math.cos(angle)
        length=[.159,.178,.197,.177,.158,.181,.192,.164][j]
        q=(.041*vx,1.351,.53+.036*vz);r=(.119*vx,1.402+.013*math.sin(3*angle),.53+.102*vz)
        t=(length*vx,1.398+.019*math.sin(2*angle),.526+length*vz*.90)
        a=blend(a,segment(x,y,z,q,r,.031,.036,(1,.9,1),.004),.018)
        a=blend(a,segment(x,y,z,r,t,.036,.0155,(1,.92,1)),.011)
    for s in (-1,1):
        a=np.maximum(a,-ell(x-s*.032,y-1.404,z-.553,.017,.027,.015))
    return a

def all_regions(x,y,z):
    r=[core(x,y,z),tail(x,y,z)]
    for s in (-1,1):
        r.extend((limb(x,y,z,s,True),limb(x,y,z,s,False)))
    return r

def surface(x,y,z):
    regions=all_regions(x,y,z);a=regions[0]
    for b,k in zip(regions[1:],[.16,.153,.126,.153,.126]):a=blend(a,b,k)
    a=blend(a,nose(x,y,z),.019)
    # Ground contact has rounded pad edges and no buried floating claws.
    a=np.maximum(a,.016-z)
    return a+.0018*np.sin(32*x+7*y)*np.sin(46*y+29*z)

def tessellate(step):
    axes=[np.arange(a,b+step*.6,step,dtype=F) for a,b in [(-1.18,1.18),(-1.86,1.58),(-.035,1.36)]]
    dims=tuple(len(a) for a in axes)
    field=np.empty(dims,dtype=F)
    # Blocks keep even the high-resolution level reproducible on a small CPU worker.
    for i in range(0,dims[0],10):
        field[i:i+10]=surface(axes[0][i:i+10,None,None],axes[1][None,:,None],axes[2][None,None,:])
    offsets=np.array([[i,j,k] for i in (0,1) for j in (0,1) for k in (0,1)],dtype=np.int32)
    cells=np.stack([field[a:dims[0]-1+a,b:dims[1]-1+b,c:dims[2]-1+c]<0 for a,b,c in offsets],axis=0)
    ijk=np.column_stack(np.nonzero(np.any(cells,axis=0)&~np.all(cells,axis=0))).astype(np.int32);del cells
    corners=ijk[:,None,:]+offsets
    values=field[corners[...,0],corners[...,1],corners[...,2]]
    points=np.zeros_like(ijk,dtype=F);cnt=np.zeros(len(ijk),dtype=F)
    for i in range(8):
        for j in range(i+1,8):
            if np.sum(np.abs(offsets[i]-offsets[j]))!=1:continue
            a,b=values[:,i],values[:,j];active=(a<0)!=(b<0)
            t=a[active]/(a[active]-b[active])
            points[active]+=offsets[i]+t[:,None]*(offsets[j]-offsets[i]);cnt[active]+=1
    points=(points/cnt[:,None]+ijk)*step+np.array([a[0] for a in axes],dtype=F)
    ids=np.full(np.array(dims)-1,-1,np.int32);ids[ijk[:,0],ijk[:,1],ijk[:,2]]=np.arange(len(ijk))
    faces=[]
    for dim in range(3):
        a=np.take(field,range(dims[dim]-1),axis=dim);b=np.take(field,range(1,dims[dim]),axis=dim)
        cuts=np.column_stack(np.nonzero((a<0)!=(b<0))).astype(np.int32)
        others=[(dim+1)%3,(dim+2)%3]
        k0,k1=others
        use=(cuts[:,k0]>0)&(cuts[:,k1]>0)&(cuts[:,k0]<dims[k0]-1)&(cuts[:,k1]<dims[k1]-1)
        cuts=cuts[use];oo=[]
        for l,r in [(0,0),(-1,0),(-1,-1),(0,-1)]:
            cc=cuts.copy();cc[:,k0]+=l;cc[:,k1]+=r;oo.append(ids[tuple(cc.T)])
        f=np.array(oo).T.copy();assert np.all(f>=0)
        rev=field[tuple(cuts.T)]>0;f[rev]=f[rev,::-1];faces.append(f)
    return points.astype(F),np.concatenate(faces)
