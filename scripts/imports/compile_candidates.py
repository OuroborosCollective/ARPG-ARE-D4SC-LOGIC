"""Compile extracted C# only in isolation; never execute generated proposal assemblies."""
import pathlib,json,subprocess,collections,re
import argparse
parser=argparse.ArgumentParser();parser.add_argument('--audit',required=True,type=pathlib.Path);parser.add_argument('--dotnet-root',required=True,type=pathlib.Path);args=parser.parse_args()
audit=args.audit.resolve();root=audit.parent;m=json.loads((audit/'manifest.json').read_text());sdk=args.dotnet_root.resolve();csc=next((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'));refs=list((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0/*.dll'));tmp=root/'candidate-compile';tmp.mkdir(exist_ok=True)
results={}
for r in m['records']:
 if r['sha256'] in results:continue
 if r['filename'] not in ['csharp','cs','c#'] and not (r['filename'] or '').endswith('.cs'):continue
 source=tmp/(r['sha256']+'.cs');source.write_bytes((audit/r['extracted_file']).read_bytes())
 cmd=[str(sdk/'dotnet'),str(csc),'/nologo','/target:library','/langversion:9','/out:'+str(tmp/(r['sha256']+'.dll'))]+['/reference:'+str(f) for f in refs]+[str(source)]
 p=subprocess.run(cmd,capture_output=True,text=True,timeout=25);log=p.stdout+p.stderr;(audit/(r['sha256']+'.compile.log')).write_text(log)
 results[r['sha256']]={'exit_code':p.returncode,'environment':'isolated .NET 8 references / C# 9, no Unity or generated dependency stubs','diagnostics':log.splitlines()[:8]}
for r in m['records']:
 if r['sha256'] in results:
  r['compilation']=results[r['sha256']]
  r['classification']='ausführbarer Quellcode' if results[r['sha256']]['exit_code']==0 else 'unvollständiger Quellcode'
 if r['filename']=='json':
  try:json.loads((audit/r['extracted_file']).read_text());r['classification']='Datenmodell'
  except ValueError:r['classification']='Pseudocode'
(audit/'manifest.json').write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n');(audit/'compilation-summary.json').write_text(json.dumps(results,indent=2)+'\n')
print('COMPILED',len(results),'PASS',sum(x['exit_code']==0 for x in results.values()))
for sha,v in results.items():print(sha[:12],v['exit_code'],[re.sub(r'.*?: (error [^:]+):',r'\1:',e) for e in v['diagnostics'][:2]])
