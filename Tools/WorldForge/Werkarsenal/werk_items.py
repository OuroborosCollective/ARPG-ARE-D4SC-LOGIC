from math import tau, pi, sin, cos
from werk_mesh import Piece, ellipse, rail, transform

def spindle(m,lower,upper,r=.023,wood='Leder',band='Messing',wrap=True):
    path=[(0,0,lower+(upper-lower)*t) for t in (0,.018,.07,.21,.55,.85,.97,1)]
    m.tube(path,[r*.67,r*.91,r,r*.95,r*1.09,r*.96,r,r*.82],wood,16)
    for z in (lower+.008,upper-.014):
        m.tube([(0,0,z-.009),(0,0,z),(0,0,z+.009)],[r*.98,r*1.16,r*1.02],band,16)
    if wrap:
        a=max(lower+.024,-.098);b=min(upper-.022,.14)
        if b>a:
            count=10 if b-a>.14 else 6
            for off in (0,.42):
                p=[]
                for j in range(count*8+1):
                    t=j/(count*8);angle=count*tau*t+off
                    p.append(((r*1.025)*cos(angle),(r*1.025)*sin(angle),a+(b-a)*t))
                m.tube(p,r*.109,'Band',5)

def star(m,cx,cz,y,r,n=7):
    for j in range(n):
        a=tau*j/n+.35
        path=[]
        for t in (0,.28,.62,1):
            q=r*(.18+.82*t);a2=a+.17*sin(pi*t)
            path.append((cx+sin(a2)*q,y+.003+.004*sin(t*pi),cz+cos(a2)*q))
        m.tube(path,[.072*r,.156*r,.132*r,.03*r],'Elfenbein',7)
    m.rivet(cx,y+.009,cz,r*.16,'Glockenguss')

def petal(cx,cz,dx,dz,w):
    l=(dx*dx+dz*dz)**.5;nx=-dz/l;nz=dx/l
    return [(cx+dx*t+nx*w*s,cz+dz*t+nz*w*s) for t,s in [(0,-.32),(.24,-1),(.65,-.79),(1,0),(.81,.45),(.31,.97),(.08,.68)]]

def ironfast(m,x,z,y):
    for side in (-1,1):
        m.rivet(x+.017*side,y,z,.007,'Messing')

def rattler():
    builds=[]
    m=Piece('R_Gratkeule');spindle(m,-.23,.80,.024,'Grat','Band')
    for i in range(6):
        t=i/5;z=.25+i*.087;w=.042+.043*t
        for side in (-1,1):
            xp=side*(w*.34);m.plate(petal(xp,z,side*w*1.16,.10+.052*t,.018+.011*t),'Elfenbein',.009,.039,'Grat',.12,.009)
            m.tube([(xp,.026,z+.025),(side*w*.95,.027,z+.075),(side*w*1.15,.013,z+.10)], [.007,.004,.0017],'Ton',6)
        m.tube([(0,0,z),(0,0,z+.012)],[.037,.035],'Band',13)
    m.blade([(.70,-.012,.051,.06),(.80,-.005,.073,.08),(.87,.006,.073,.10),(.913,.015,.059,.063),(.968,.044,.034,.03),(1.016,.067,.004,.004)],'Schiefer','Grat','Schiefer',.003,.030)
    star(m,.0,.795,.04,.052);builds.append(m)

    m=Piece('R_Splitterzahn');spindle(m,-.105,.105,.021,'Leder','Grat')
    m.blade([(.082,0,.033,.029),(.133,-.008,.055,.040),(.197,-.008,.052,.044),(.269,-.003,.041,.041),(.293,.005,.047,.031),(.353,.022,.039,.032),(.428,.058,.026,.021),(.474,.094,.004,.003)],'Schiefer','Schiefer','Kante',.004,.019)
    for side in (-1,1):
        m.plate(petal(side*.025,.096,side*.081,.055,.019),'Grat',.011,.025,'Elfenbein',.13,.008)
    star(m,0,.119,.028,.028);builds.append(m)

    m=Piece('R_Scharrbeil');spindle(m,-.27,.75,.022,'Grat','Leder')
    for j in range(3):m.plate(petal(.008,.647+j*.048,.156-.030*j,.14-.017*j,.065-.014*j),'Schiefer',.003+.014*j,.039,'Grat',.13,.014)
    for j in range(2):m.plate(petal(-.005,.666+j*.052,-.089+j*.017,.071,.033),'Elfenbein',.003,.032,'Grat')
    m.plate([(-.044,.61),(.033,.617),(.044,.768),(.012,.823),(-.038,.777)],'Leder',.041,.067,'Band')
    for z in (.655,.702,.748):ironfast(m,0,z,.054)
    builds.append(m)
    for key,tower in [('R_Rippenschild',True),('R_Grabschild',False)]:
        m=Piece(key);h=.44 if tower else .216;w=.225 if tower else .173
        shape=[(-.79,.85),(-.28,1),(.46,.91),(.92,.74),(1,.17),(.88,-.49),(.54,-.84),(-.15,-1),(-.72,-.70),(-.94,-.22),(-1,.53)]
        contour=[(w*x,h*z) for x,z in shape]
        dome=lambda x,z:.075+.048*(1-min(1,abs(x/w))**1.5)
        m.plate(contour,'Leder',dome,.026,'Band',.065,.009)
        n=5 if tower else 4
        for i in range(n):
            t=i/(n-1);z=(-.66+1.13*t)*h
            if tower:
                for side in (-1,1):
                    x=.01*side;shape=petal(x,z,side*w*(.56+.28*sin(pi*t)),.09+.043*t,.027)
                    m.plate(shape,'Elfenbein',lambda x,z:dome(x,z)+.014,.026,'Grat',.13,.006)
                m.plate(petal(0,z-.035,.015,.125,.033),'Grat',.154,.026,'Elfenbein',.12,.010)
            else:
                for side in (-1,1):
                    x=side*w*(.20+.35*t)
                    m.plate(petal(x,z,side*(.045+.025*t),.12-.026*t,.033),'Schiefer',dome(x,z)+.021,.023,'Grat',.13,.010)
        star(m,0,-h*.01,.172 if tower else .154,.056 if tower else .044)
        for x,z in contour[::2]:m.rivet(x*.86,dome(x,z)+.017,z*.89,.012,'Band')
        m.tube([(-w*.36,.067,.04),(-w*.28,.007,.032),(0,-.001,0),(w*.28,.007,-.032),(w*.36,.065,-.04)],.015,'Band',10)
        builds.append(m)
    return builds

def vent(m,x,z,y,w=.040,h=.014):
    for j in range(3):
        dz=(j-1)*h*1.4
        m.plate([(x-w,z+dz),(x-w*.80,z+dz+h*.48),(x+w,z+dz+h*.48),(x+w*.78,z+dz)],'Schiefer',y,.003,'Schlacke',.055,.002)

def guard():
    builds=[]
    m=Piece('G_Ofengabel');spindle(m,-.34,.88,.021,'Leder','Eisen')
    m.plate([(-.071,.73),(.068,.74),(.071,.84),(.039,.925),(.003,.903),(-.058,.921)],'Eisen',.016,.040,'Kante',.09,.009)
    for side,long in ((-1,.21),(1,.29)):
        xs=side*.056
        m.blade([(.855,xs,.024,.021),(.908,xs+side*.024,.020,.019),(.992,xs+side*.037,.022,.018),(1.00+long*.27,xs+side*.032,.013,.013),(.90+long,xs+side*.025,.003,.003)],'Eisen','Kante','Glockenguss')
    vent(m,0,.803,.031,.043,.012);m.rivet(0,.032,.758,.010);builds.append(m)
    m=Piece('G_Zollmesser');spindle(m,-.103,.094,.018,'Leder','Messing');m.blade([(.078,0,.027,.03),(.135,-.006,.039,.043),(.173,-.002,.038,.042),(.219,.002,.034,.041),(.286,.013,.037,.033),(.313,.021,.030,.024),(.359,.036,.016,.015),(.385,.050,.002,.002)],'Eisen','Kante','Eisen',.0018,.013)
    m.plate([(-.073,.073),(-.027,.078),(.014,.093),(.07,.13),(.082,.126),(.057,.094),(-.011,.067),(-.066,.052)],'Messing',.010,.022,'Glockenguss')
    for i in range(3):m.rivet(-.01,.016,.128+i*.036,.0048,'Glockenguss')
    builds.append(m)
    m=Piece('G_Hammerbeil');spindle(m,-.235,.715,.024,'Leder','Eisen')
    m.plate([(-.145,.607),(-.088,.612),(-.050,.641),(.066,.636),(.126,.60),(.195,.558),(.212,.653),(.192,.743),(.151,.786),(.092,.728),(.044,.709),(-.050,.713),(-.094,.752),(-.154,.735)],'Eisen',.021,.066,'Kante',.095,.014)
    m.plate([(-.155,.606),(-.103,.608),(-.103,.740),(-.156,.741)],'Schlacke',.037,.092,'Kante',.068,.006)
    for c in [(-.130,.637),(-.131,.707),(.008,.684)]:m.rivet(c[0],.06,c[1],.009)
    vent(m,.111,.685,.046,.037,.009);builds.append(m)
    m=Piece('G_Zollbuckler');outline=[]
    for i in range(12):
        a=tau*i/12+pi/12;r=.19*(1+.034*((i%3)-1));outline.append((r*cos(a),r*sin(a)*.93))
    dome=lambda x,z:.095-.025*(abs(x)+abs(z))/.19
    m.plate(outline,'Eisen',dome,.027,'Kante',.080,.008)
    q=[(.126*cos(tau*i/12+pi/12),.119*sin(tau*i/12+pi/12)) for i in range(12)]
    m.plate(q,'Schlacke',.107,.061,'Messing',.067,.006)
    vent(m,0,0,.128,.069,.014)
    for i,(x,z) in enumerate(outline):
        if i%2==0:m.rivet(x*.83,dome(x,z)+.019,z*.83,.012)
    m.tube([(-.066,.069,0),(-.047,.02,0),(.047,.02,0),(.066,.069,0)],.014,'Leder',10);builds.append(m)
    m=Piece('G_Zunderpfanne');spindle(m,-.121,.169,.019,'Leder','Messing')
    m.shell([(.121,.023,.021),(.149,.042,.035),(.173,.076,.050),(.184,.083,.061),(.201,.079,.058),(.209,.081,.060)],'Schlacke',edge='Kante',n=20)
    for i in range(6):
        a=tau*i/6
        m.tube([(.060*sin(a),.045*cos(a),.181),(.071*sin(a),.057*cos(a),.22),(.05*sin(a),.034*cos(a),.262),(.015*sin(a),.012*cos(a),.274)],[.006,.006,.005,.004],'Eisen',7)
    m.plate([(-.034,.194),(-.039,.22),(-.014,.239),(-.004,.270),(.009,.249),(.031,.25),(.025,.207),(.012,.19)],'Zunder',.008,.022,'Glockenguss')
    m.loop((0,0,.282),.015,.021,.0045,'Messing');builds.append(m)
    return builds

def bell(m,x,y,z,r,mat='Glockenguss'):
    org=len(m.v)
    m.shell([(0,r,r*.86),(.08*r,1.06*r,.91*r),(.20*r,.95*r,.80*r),(.48*r,.73*r,.62*r),(.94*r,.58*r,.49*r),(1.09*r,.39*r,.32*r)],mat,edge='Messing',depth=.003,n=14)
    for i in range(org,len(m.v)):m.v[i].x+=x;m.v[i].y+=y;m.v[i].z+=z
    m.tube([(x,y,z+.034),(x,y,z-.008),(x,y,z-.019)],[.003,r*.13,r*.08],'Eisen',8)

def weave_mark(m,x,z,y,sz):
    for j in (-1,0,1):
        rail(m,[(x-.45*sz,z+j*.16*sz),(x-.12*sz,z+(.1+j*.16)*sz),(x+.16*sz,z+(-.03+j*.16)*sz),(x+.44*sz,z+(.08+j*.16)*sz)],y,'Messing',sz*.026)

def mill():
    builds=[]
    m=Piece('M_Wehrsaebel');spindle(m,-.208,.145,.020,'Band','Glockenguss')
    m.blade([(.126,-.005,.037,.036),(.204,-.014,.051,.043),(.31,-.046,.051,.045),(.445,-.064,.052,.046),(.570,-.041,.059,.04),(.66,.007,.052,.042),(.734,.056,.048,.033),(.83,.082,.033,.025),(.91,.08,.020,.015),(.977,.061,.003,.003)],'Zinn','Kante','Glockenguss',.001,.016)
    for s in [-1,1]:m.tube([(0,0,.12),(s*.046,.002,.121),(s*.079,.004,.141),(s*.092,0,.177)], [.017,.013,.01,.005],'Glockenguss',9)
    for i in range(3):m.rivet(-.009-i*.006,.021,.17+i*.034,.0056,'Messing')
    builds.append(m)
    m=Piece('M_Lotklinge');spindle(m,-.113,.109,.017,'Leinen','Glockenguss')
    m.blade([(.089,0,.024,.024),(.135,0,.033,.033),(.18,.003,.030,.030),(.34,.002,.026,.024),(.477,-.009,.028,.020),(.546,-.011,.019,.014),(.612,0,.003,.003)],'Zinn','Kante','Glockenguss',.0007,.012)
    m.plate([(-.064,.093),(-.047,.122),(-.006,.135),(.048,.116),(.073,.087),(.039,.101),(0,.111),(-.037,.101)],'Glockenguss',.014,.024,'Messing',.09,.004)
    bell(m,-.018,.016,-.145,.018);builds.append(m)
    m=Piece('M_Jochbeil');spindle(m,-.275,.762,.022,'Band','Glockenguss')
    m.plate([(-.07,.642),(-.095,.693),(-.087,.74),(-.018,.713),(.05,.708),(.102,.724),(.152,.774),(.175,.718),(.169,.645),(.124,.547),(.089,.609),(.055,.65),(-.003,.66)],'Zinn',.020,.043,'Kante',.09,.008)
    rail(m,[(-.084,.743),(-.046,.781),(.01,.786),(.043,.763),(.055,.719)],.024,'Glockenguss',.012)
    bell(m,-.037,.018,.698,.018);weave_mark(m,.103,.680,.036,.06);builds.append(m)
    m=Piece('M_Wehrschild');coords=[(-.16,.226),(-.116,.279),(-.035,.286),(.035,.302),(.108,.27),(.155,.245),(.170,.166),(.133,.013),(.069,-.174),(.015,-.279),(-.044,-.232),(-.125,-.007),(-.170,.140)]
    f=lambda x,z:.078+.033*(1-min(1,abs(x)/.17))
    m.plate(coords,'Zinn',f,.026,'Glockenguss',.065,.009)
    for side in (-1,1):
        rail(m,[(side*.135,.194),(side*.110,.08),(side*.059,-.037),(.008,-.163)],.126,'Glockenguss',.007)
        rail(m,[(side*.128,.197),(side*.103,.205),(side*.062,.211),(side*.022,.191)],.127,'Messing',.006)
    weave_mark(m,.0,.075,.127,.082)
    for x,z in coords[::3]:m.rivet(.87*x,f(x,z)+.013,.85*z,.010)
    m.tube([(-.059,.07,.01),(-.04,.014,.01),(.05,.014,.005),(.066,.07,0)],.012,'Band',9);builds.append(m)
    m=Piece('M_Glockenring')
    points=[];r=[];count=33
    for i in range(count):
        t=i/(count-1);a=-.94*pi+t*1.71*pi
        points.append((.127*sin(a),.035+.014*cos(2*a),.14*cos(a)+.024));r.append(.026+.019*(.5+.5*cos(a-.1)))
    m.tube(points,r,'Glockenguss',12)
    for i in range(1,4):
        xx=-.054+i*.030
        m.plate([(xx-.014,-.071),(xx+.019,-.076),(xx+.032,-.172-i*.014),(xx+.002,-.155),(xx-.008,-.184-i*.01),(xx-.020,-.134)],'Leinen',.029+.007*i,.004,'Band',.025,.002)
    for a in (-2.0,-1.1,.54,1.47):
        m.rivet(.129*sin(a),.083,.14*cos(a)+.024,.010,'Messing')
    m.tube([(-.093,.039,-.048),(-.066,.003,-.064),(.05,.003,-.064),(.080,.036,-.066)],.012,'Band',9)
    builds.append(m)
    return builds
