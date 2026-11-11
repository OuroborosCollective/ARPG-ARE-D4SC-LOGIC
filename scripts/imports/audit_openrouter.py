"""Extract untrusted OpenRouter proposals without importing or executing them."""
import argparse
import hashlib
import json
from pathlib import Path
import re


def digest(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('inputs', nargs='+', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    records, sources = [], []
    def add(source, locator, content, model=None, filename=None):
        if not isinstance(content, str) or not content.strip():
            return
        sha = digest(content)
        path = args.output / (sha + '.txt')
        path.write_text(content, encoding='utf-8')
        cs = bool(re.search(r'\b(class|struct|interface|enum)\s+\w+', content))
        incomplete = bool(re.search(r'\.\.\.|TODO|NotImplementedException|placeholder|pseudocode', content, re.I))
        category = ('unvollständiger Quellcode' if cs else 'Konzept')
        if not cs and content.lstrip().startswith(('{','[')): category = 'Datenmodell'
        if re.search(r'\bpseudocode\b',content,re.I): category = 'Pseudocode'
        flags = [name for name,pattern in {
            'wall-clock':r'DateTime\.(Now|UtcNow)|Date\.now|Time\.(deltaTime|time)',
            'random-source':r'Random\(|Random\.|random\(|random_device',
            'unstable-hash':r'GetHashCode\(',
            'ordering-review':r'Dictionary<|HashSet<|unordered_map|\.Sort\(|BinarySearch',
            'threading-review':r'Task\.|Thread|Parallel\.|async ',
            'unity-dependency':r'UnityEngine|MonoBehaviour|ScriptableObject',
            'numeric-scaling-review':r'float|double|Fixed|<<\s*16|1000|10000',
            'allocation-lifetime-review':r'new List|new Dictionary|event |IDisposable',
            'save-contract-review':r'Serializ|Save|Load|Json|PlayerPrefs',
            'incomplete':r'\.\.\.|TODO|NotImplementedException|placeholder'
        }.items() if re.search(pattern, content)]
        records.append(dict(source=source,locator=locator,model=model,filename=filename,sha256=sha,
            bytes=len(content.encode()),classification=category,flags=flags,
            compilation='UNVERIFIED: proposal, not compiled in AreArpg.Core',extracted_file=path.name))
    for p in args.inputs:
        raw=p.read_bytes();sources.append(dict(name=p.name,bytes=len(raw),sha256=hashlib.sha256(raw).hexdigest()))
        if p.suffix!='.json':continue
        data=json.loads(raw)
        chars=data.get('characters',{})
        for mid,m in data.get('messages',{}).items():
            if m.get('type')!='assistant':continue
            model=m.get('metadata',{}).get('variantSlug') or chars.get(m.get('characterId'),{}).get('model')
            for ref in m.get('items',[]):
                item=data.get('items',{}).get(ref['id'],{}).get('data',{})
                if item.get('type')!='message':continue
                for ci,c in enumerate(item.get('content',[])):
                    text=c.get('text','');loc=f'messages/{mid}/items/{ref["id"]}/content/{ci}'
                    add(p.name,loc,text,model)
                    for bi,block in enumerate(re.finditer(r'```([^\n]*)\n(.*?)```',text,re.S)):
                        add(p.name,loc+f'/fence/{bi}',block.group(2),model,block.group(1))
                        if block.group(1).strip()=='json':
                            try: obj=json.loads(block.group(2))
                            except ValueError:continue
                            def walk(v,ptr):
                                if isinstance(v,dict):
                                    for k,x in v.items():walk(x,ptr+'/'+str(k))
                                elif isinstance(v,list):
                                    for i,x in enumerate(v):walk(x,ptr+'/'+str(i))
                                elif isinstance(v,str) and re.search(r'\b(class|struct|interface|enum)\s+\w+|\.cs$',v):add(p.name,ptr,v,model)
                            walk(obj,loc+f'/fence/{bi}/json')
        for cid,c in data.get('artifactFileContents',{}).items():
            links=[]
            for vid,v in data.get('artifactVersions',{}).items():
                for fid,target in v.get('fileContents',{}).items():
                    if target==cid:links.append((vid,data.get('artifactFiles',{}).get(fid,{}).get('filename'),v.get('characterName')))
            if not links:links=[(None,None,None)]
            for vid,filename,model in links:add(p.name,f'artifactFileContents/{cid};version={vid}',c.get('content',''),model,filename)
    manifest=dict(schema='ashen.openrouter-audit.v1',sources=sources,records=records,
        notice='Classification is conservative static triage, never proof of compilability. Raw proposals must not enter Assets. No model claims are runtime evidence.')
    (args.output/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps(dict(sources=len(sources),records=len(records),unique_contents=len({r['sha256'] for r in records}),manifest=str(args.output/'manifest.json'))))

if __name__=='__main__':main()
