"""Build reproducible volume surfaces using only NumPy. Blender reads the resulting arrays."""
from pathlib import Path
import sys
import numpy as np
from tief_form import tessellate
root=Path(sys.argv[1]);(root/'cache').mkdir(parents=True,exist_ok=True)
for i,step in enumerate((.019,.036,.063)):
    v,f=tessellate(step)
    np.savez_compressed(root/'cache'/f'koerper{i}.npz',v=v,f=f)
    print('LOD',i, 'resolution_m',step,'vertices',len(v),'quads',len(f),flush=True)
