from math import pi,tau,sin,cos,exp
from werk_mesh import Piece,rail,transform
from werk_items import star,petal

def face_depth(x,z):
    return .126-.55*abs(x)+.01*(.03-z)

def jerkin(m):
    n=56;layers=7;verts=[];faces=[];kinds=[];maps=[]
    def point(a,t,inside=0):
        sa,ca=sin(a),cos(a)
        neck=exp(-(abs(sa)/.37)**2)*(.061 if ca>0 else .035)
        arm=.148*abs(sa)**10
        top=.161-neck-arm
        low=-.194+.032*abs(sa)**2-.021*max(ca,0)**3
        z=low+(top-low)*t
        waist=(.157+.035*sin(pi*t*.86)**1.3)-inside*.007
        depth=.102+.034*sin(pi*t*.94)-inside*.007
        return (waist*sa,depth*ca+(max(0,ca)**2)*.01*sin(pi*t),z)
    for inside in (0,1):
        for j in range(layers):
            t=j/(layers-1)
            for i in range(n):verts.append(point(tau*i/n,t,inside))
    stride=layers*n
    def put(ids,k,uv):faces.append(ids);kinds.append(k);maps.append(uv)
    for layer in (0,1):
        off=layer*stride
        for j in range(layers-1):
            for i in range(n):
                k=(i+1)%n;idx=off+j*n
                put((idx+i,idx+k,idx+k+n,idx+i+n),'Leder',((i/n,j/(layers-1)),((i+1)/n,j/(layers-1)),((i+1)/n,(j+1)/(layers-1)),(i/n,(j+1)/(layers-1))))
    for row in (0,layers-1):
        for i in range(n):
            k=(i+1)%n;idx=row*n
            put((idx+i,idx+k,stride+idx+k,stride+idx+i),'Band',((i/n,0),((i+1)/n,0),((i+1)/n,1),(i/n,1)))
    m.mesh(verts,faces,kinds,maps,True)
    for which in (0,1):
        edge=[point(tau*i/n,which) for i in range(n)]
        m.tube(edge,.005,'Band' if which else 'Leder',7,True)
    for j in range(17):
        a=tau*j/17+.013
        points=[point(a+d,.976) for d in (0,.028,.055)]
        m.tube(points,.0020,'Leinen',5)
    # The front and rear hide panels are joined by visible diagonally stitched side seams.
    for s in (-1,1):
        for angle in (.92,1.91):
            aa=s*angle
            m.tube([point(aa+.14*sin(pi*t),t) for t in [j/16 for j in range(17)]],.0032,'Band',7)
            for i in range(8):
                t=.09+.10*i;a=aa+.14*sin(pi*t)
                m.tube([point(a-.022,t),point(a+.024,t+.020)],.0019,'Leinen',5)

def mask():
    m=Piece('R_Wangenmaske')
    for z in (.075,.114):
        m.shell([(z-.010,.112,.112),(z+.010,.112,.112)],'Leder',edge='Band',depth=.005,n=28)
    for side in [-1,1]:
        s=lambda pts:[(side*x,z) for x,z in pts]
        outline=s([(.014,.091),(.062,.123),(.101,.114),(.128,.089),(.146,.025),(.091,.044),(.055,.052),(.025,.050),(.011,.032)])
        m.plate(outline,'Grat',face_depth,.014,'Elfenbein',.085,.007)
        outline=s([(.047,.014),(.073,.015),(.130,.041),(.165,.018),(.148,-.041),(.129,-.096),(.091,-.116),(.066,-.094),(.066,-.052),(.036,-.033)])
        m.plate(outline,'Grat',face_depth,.015,'Elfenbein',.095,.010)
        m.plate(s([(.083,.014),(.121,.036),(.152,.017),(.133,-.04),(.116,-.069),(.130,-.018),(.101,-.03)]),'Schiefer',lambda x,z:face_depth(x,z)+.013,.008,'Grat',.10,.003)
        for x,z in [(side*.08,.085),(side*.126,-.02)]:m.rivet(x,face_depth(x,z)+.019,z,.0062)
        rail(m,s([(.029,.071),(.066,.092),(.093,.090),(.118,.071)]),lambda x,z:face_depth(x,z)+.014,'Ton',.0025)
    m.plate([(-.037,.096),(.022,.109),(.035,.063),(.024,-.007),(.012,-.066),(-.006,-.079),(-.028,-.049),(-.035,.022)],'Eisen',.130,.034,'Glockenguss',.10,.006)
    star(m,0,-.014,.145,.03,8)
    for z in (.085,.056):m.rivet(-.004,.144,z,.0048,'Messing')
    return m

def breast():
    m=Piece('R_Gratharnisch')
    jerkin(m)
    m.shell([(-.240,.158,.113),(-.213,.150,.108),(-.195,.153,.110)],'Leder',edge='Band',depth=.006,n=32)
    for j in range(9):
        a=(j-4)*.41;x=.13*sin(a);y=.111*cos(a)
        m.plate([(x-.029,-.204),(x+.024,-.199),(x+.046,-.276),(x+.02,-.32+.014*(j%3)),(x-.024,-.307),(x-.033,-.252)],'Leder',y,.007,'Band',.065,.004)
        m.rivet(x,y+.009,-.213,.006,'Eisen')
    for s in (-1,1):
        # A continuous iron shoulder joint under the low, overlapping excavating plates.
        coords=[(s*x,z) for x,z in [(.095,.115),(.128,.148),(.162,.132),(.193,.095),(.201,.058),(.186,.037),(.163,.040),(.139,.092)]]
        m.plate(coords,'Eisen',lambda x,z:.125-.35*abs(x),.034,'Glockenguss',.08,.005)
        for i in range(3):
            xx=.165+i*.009;zz=.097-i*.021
            co=[(s*x,z) for x,z in [(xx-.018,zz+.023),(xx+.009,zz+.03),(xx+.035,zz+.005),(xx+.025,zz-.022),(xx+.009,zz-.014),(xx-.011,zz+.002)]]
            m.plate(co,'Grat',.079+i*.003,.012,'Elfenbein',.12,.004)
            m.rivet(s*(xx+.008),.090+i*.003,zz+.009,.0045)
        for i in range(4):
            p=.025*i;zz=-.056-.023*i
            co=[(s*x,z) for x,z in [(p+.022,zz+.015),(p+.047,zz+.028),(p+.077,zz+.023),(p+.105,zz+.044),(p+.091,zz+.008),(p+.058,zz-.005),(p+.039,zz-.007)]]
            m.plate(co,'Leder',lambda x,z:.138-.40*abs(x),.010,'Ton',.08,.003)
        for i in range(3):
            z=.086-i*.050;x=s*.161
            coords=petal(x,z,-s*(.097+.043*i/2),-.072+.008*i,.026-.002*i)
            fy=lambda x,z:.163-.41*abs(x)
            m.plate(coords,'Grat',fy,.012,'Elfenbein',.095,.010)
            rail(m,[(s*.151,z),(s*.118,z-.023),(s*.089,z-.040)],lambda x,z:fy(x,z)+.014,'Ton',.0032)
        for i in range(2):
            x=s*(.151+i*.017);z=.111-.028*i
            m.plate(petal(x,z,s*(.019+.007*i),.056-.005*i,.015),'Grat',lambda x,z:.104-.29*abs(x),.015,'Elfenbein',.08,.004)
    for z in (-.153,-.183):
        m.shell([(z,.164,.124),(z+.013,.164,.124)],'Band',span=pi,depth=.006,edge='Leder',n=28)
    m.plate([(-.035,.093),(0,.119),(.044,.084),(.033,.016),(.009,-.054),(-.010,-.074),(-.035,-.012)],'Schiefer',.156,.014,'Grat',.09,.006)
    star(m,0,.046,.164,.034,8)
    m.plate([(-.042,-.146),(.048,-.146),(.037,-.193),(-.026,-.194)],'Glockenguss',.135,.021,'Messing',.085,.004)
    return m

def armour_limb(calf,side):
    key=('R_Gratschiene_' if calf else 'R_Scharrstulpe_')+side;m=Piece(key)
    radius=.068 if calf else .054;length=.33 if calf else .238
    # Local anchor is the start of the lower limb. Plates follow the limb downwards.
    top=-.017 if calf else -.023
    rows=[(top-d*length,radius*w,.97*radius*w) for d,w in [(0,.93),(.10,1.05),(.29,1.10),(.64,.86),(1,.67)]]
    m.shell(rows,'Leder',depth=.006,edge='Band',n=24)
    if calf:
        for i in range(3):
            d=.16+i*.21;rr=radius*(1.04-.24*i/2)
            m.shell([(top-d*length,rr+.003,rr+.003),(top-(d+.070)*length,rr+.004,rr+.004)],'Glockenguss' if i==1 else 'Leder',edge='Band',depth=.004,n=22)
        face=lambda x,z:.075-.35*abs(x)-.035*min(1,abs(z)/length)
        for s in [-1,1]:
            coords=petal(s*.032,top-.06,s*.006,-length*.73,.021 if s<0 else .025)
            m.plate(coords,'Grat',face,.012,'Elfenbein',.12,.005)
        m.plate([(-.046,top-.034),(-.015,top+.012),(.035,top+.003),(.058,top-.035),(.021,top-.085),(-.007,top-.11),(-.040,top-.076)],'Schiefer',face,.014,'Grat',.12,.008)
        star(m,.004,top-.044,.078,.023)
        for s in (-1,1):
            for z in [top-.144,top-.203]:m.rivet(s*.032,face(s*.03,z)+.013,z,.0055)
    else:
        for i in range(4):
            p=.06+i*.19;r=radius*(1.08-.24*i/3)
            m.shell([(top-length*p,r,r),(top-length*(p+.14),r*.98,r*.98)],'Grat' if i in [0,3] else 'Schiefer',span=.71*pi,edge='Elfenbein' if i==0 else 'Eisen',depth=.005,n=21)
            m.rivet(.030*(1-i*.05),r*.84+.004,top-length*(p+.06),.0047)
        for i in range(2):
            x=-.021+i*.039
            m.plate(petal(x,top-.176,.005*(-1+2*i),-.066+.013*i,.019),'Grat',.044,.016,'Elfenbein',.11,.004)
    if side=='R':transform(m,-1)
    return m

def armour():
    return [mask(),breast()]+[armour_limb(calf,side) for calf in (False,True) for side in ('L','R')]
