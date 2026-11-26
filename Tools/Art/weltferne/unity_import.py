"""Install art importer settings in the isolated Ashen Crypt worktree during the build."""
from pathlib import Path
import argparse, hashlib, shutil, re

def install(root,source):
    target=root/'Assets/Resources/Weltferne'; detail=target/'Detail';detail.mkdir(exist_ok=True)
    res=root/'Assets/Resources'
    examples=[(res/'Equipment.meta').read_text(),(res/'AshenAudioCredits.txt.meta').read_text(),
              (res/'Equipment/Tiefscharrer/Tiefscharrer_BaseColor.png.meta').read_text()]
    def meta(asset,index):
        p=Path(str(asset)+'.meta')
        ident=hashlib.sha256(('ashen-crypt/horizon-atlas/v1/'+asset.relative_to(root).as_posix()).encode()).hexdigest()[:32]
        data=re.sub(r'^guid: [0-9a-f]+$',f'guid: {ident}',examples[index],flags=re.M)
        if index==2:
            data=re.sub(r'maxTextureSize: [0-9]+','maxTextureSize: 512',data)
            data=data.replace('filterMode: 1','filterMode: 2').replace('alphaUsage: 1','alphaUsage: 0')
        data=''.join(line.rstrip()+'\n' for line in data.splitlines())
        if p.exists() and f'guid: {ident}' not in p.read_text():raise RuntimeError('foreign GUID: '+str(p))
        p.write_text(data)
    meta(target,0);meta(detail,0)
    for p in target.glob('*.bytes'):meta(p,1)
    for name in ['masonry','rock','timber','earth']:
        p=detail/(name+'.png')
        if (source/p.name).resolve()!=p.resolve():shutil.copyfile(source/p.name,p)
        meta(p,2)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('repository',type=Path);p.add_argument('--source',type=Path,default=Path(__file__).resolve().parent/'textures');a=p.parse_args()
    install(a.repository.resolve(),a.source.resolve())
