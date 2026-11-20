"""Generate the Grubenlicht's hollow ceramic shell and worn surfaces in object space."""
from pathlib import Path
import numpy as np
import vtk
from vtk.util import numpy_support as vn

ROOT = Path(__file__).resolve().parent
work = ROOT / "work"
work.mkdir(exist_ok=True)
step = .011
xs, ys, zs = [np.arange(a, b, step, dtype=np.float32) for a,b in
              [(-1.12,1.12),(-.94,.94),(-.99,1.09)]]
X, Y, Z = np.meshgrid(xs,ys,zs,indexing="ij",sparse=True)

def ell(x,y,z,rx,ry,rz):
    return (np.sqrt((x/rx)**2+(y/ry)**2+(z/rz)**2)-1)*np.minimum(np.minimum(rx,ry),rz)

def blend(a,b,k):
    h=np.clip(.5+.5*(b-a)/k,0,1)
    return b*(1-h)+a*h-k*h*(1-h)

def eye(x,y,z,side):
    dx=side*x-.39
    return ell(dx*.954-(z-.02)*.3, y+.57,dx*.3+(z-.02)*.954,.282,.49,.197)

outer=ell(X,Y-.055,Z-.255,.637,.545,.615)
outer=blend(outer,ell(X,Y+.11,Z-.34,.58,.488,.386),.11)
outer=blend(outer,ell(X,Y+.31+.10*(-Z),Z+.190,.319+.082*np.clip(Z+.45,0,1),.40,.545),.158)
for side in [-1,1]:
    outer=blend(outer,ell(side*X-.432,Y+.142,Z+.05,.142,.382,.432),.105)
    outer=blend(outer,ell(side*X-.363,Y+.31,Z-.167-.170*(side*X-.36),.293,.305,.173),.061)
    outer=blend(outer,ell(side*X-.135,Y+.406,Z+.418,.143,.220,.281),.038)
interior=blend(ell(X,Y-.035,Z-.205,.520,.46,.522),ell(X,Y+.292,Z+.248,.236,.265,.407),.11)
shell=np.maximum(outer,-interior)
for side in [-1,1]:
    shell=np.maximum(shell,-eye(X,Y,Z,side))
    shell=np.maximum(shell,-ell(side*X-.099,Y+.605,Z+.475,.072,.24,.130))
    shell=np.maximum(shell,-ell(side*X-.543,Y-.157,Z+.028,.203,.160,.208))
    for i in range(4):
        shell=np.maximum(shell,-ell(side*X-(.248+.023*i),Y+.525-.013*i,Z+.183+i*.060,
                                     .025+.004*np.sin(i),.10,.019))
shell+=.006*np.sin(13*X+.8)*np.sin(17*Y+.1)*np.sin(11*Z)+.0028*np.sin(37*X-2*Y)*np.sin(29*Z+7*Y)
img=vtk.vtkImageData()
img.SetDimensions(len(xs),len(ys),len(zs));img.SetSpacing(step,step,step);img.SetOrigin(xs[0],ys[0],zs[0])
img.GetPointData().SetScalars(vn.numpy_to_vtk(np.ascontiguousarray(shell.transpose(2,1,0)).ravel(),deep=True))
mesh=vtk.vtkFlyingEdges3D();mesh.SetInputData(img);mesh.SetValue(0,0);mesh.ComputeNormalsOff();mesh.Update()
cc=vtk.vtkPolyDataConnectivityFilter();cc.SetInputConnection(mesh.GetOutputPort());cc.SetExtractionModeToLargestRegion();cc.Update()
def skin(n):
    dec=vtk.vtkQuadricDecimation();dec.SetInputConnection(cc.GetOutputPort())
    dec.SetTargetReduction(max(0,1-n/cc.GetOutput().GetNumberOfPolys()));dec.Update()
    cutter=vtk.vtkCleanPolyData();cutter.SetInputConnection(dec.GetOutputPort());cutter.Update()
    poly=cutter.GetOutput();v=vn.vtk_to_numpy(poly.GetPoints().GetData()).copy()
    f=vn.vtk_to_numpy(poly.GetPolys().GetConnectivityArray()).copy().reshape(-1,3)
    return v,f

def linear(s):
    c=np.clip(s/255,0,1)
    return np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)

def weather(v):
    x,y,z=v.T
    grain=np.sin(82*x+31*y)*np.sin(73*z+21*y)*.25+np.sin(x*24+np.sin(y*18))*np.sin(29*z)*.4
    tide=.44*np.sin(6*x+.8)*np.sin(8*y+2*z)+.24*np.sin(14*x+10*z)*np.sin(9*y)
    smoke=np.clip((.62-z)*.49+np.clip(y,0,.4)*.60,0,.9)
    erosion=np.zeros(len(v));inset=np.zeros(len(v))
    for s in [-1,1]:
        d=eye(x,y,z,s)
        erosion=np.maximum(erosion,np.exp(-((d-.009)/.031)**2)*(y<-.235))
        inset=np.maximum(inset,np.exp(-((d+.04)/.045)**2)*(y>-.35))
    # The cap was fired in a shallow mould and thumb-finished after repair.
    swale=(.33+.62*np.sin((z+np.abs(x)*.33)*37+1.4*np.sin(7*x+4*y))**2)*(z>.18)
    base=np.array([144,118,87],float)[None,:]+(31*tide+14*grain+5*swale)[:,None]
    base+=((z+.35)*14-44*smoke-32*inset+22*erosion)[:,None]*np.array([1.,.90,.73])
    if len(base):base+=np.array([9,11,13])[None,:]*np.clip(z-.22,0,.6)[:,None]
    return linear(base).astype(np.float32)

for n,tag in [(24800,'near'),(5800,'mid'),(1280,'far')]:
    v,f=skin(n);np.savez_compressed(work/f'shell-{tag}.npz',v=v,f=f,c=weather(v))
    print(tag,len(v),len(f))
