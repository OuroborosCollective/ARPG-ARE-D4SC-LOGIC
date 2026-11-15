from pathlib import Path
import subprocess,os,json,hashlib,shutil,time
p=Path('/var/lib/unityarpg/workspaces/are-arpg-forges-20261114')
e=Path('/var/lib/unityarpg/.local/share/are-arpg/game-apk-handoff-20261115');e.mkdir(exist_ok=True)
expected='2f296b26767ad0746a2ca26468ef299f91e43b57'
def git(*a):return subprocess.check_output(['git','-C',str(p),*a],text=True).strip()
def sha(f):
 h=hashlib.sha256()
 with f.open('rb') as s:
  for b in iter(lambda:s.read(1024*1024),b''):h.update(b)
 return h.hexdigest()
def private():return {str(f.relative_to(p)):sha(f) for base in ['Assets/PrivateArt','Assets/PrivateVendor','Assets/Resources/ARE/Visuals'] for f in (p/base).rglob('*') if f.is_file()}
assert git('rev-parse','HEAD')==expected and not git('diff','--name-only')
for proc in Path('/proc').iterdir():
 if not proc.name.isdigit():continue
 try:
  args=(proc/'cmdline').read_bytes().split(b'\0')
  if args and args[0].endswith(b'/Editor/Unity') and str(p).encode() in args:raise RuntimeError('Project already open')
 except (OSError,IndexError):pass
settings={str(f.relative_to(p)):f.read_bytes() for f in (p/'ProjectSettings').glob('*') if f.is_file()}
backup=e/'settings-before';backup.mkdir(exist_ok=True)
for name,data in settings.items():(backup/Path(name).name).write_bytes(data)
prefs=Path('/var/lib/unityarpg/.config/unity3d/OuroborosCollective/ARE Ashen Crypt/prefs')
prefbytes=prefs.read_bytes() if prefs.exists() else None
if prefbytes is not None:(e/'owner-prefs.private').write_bytes(prefbytes)
before={'source':expected,'tree':git('rev-parse','HEAD^{tree}'),'private':private(),'settings':{k:hashlib.sha256(v).hexdigest() for k,v in settings.items()},'prefsSha256':sha(prefs) if prefs.exists() else None}
(e/'before.json').write_text(json.dumps(before,indent=2))
apk=p/'Builds/ARE-Ashen-Crypt.apk'
if apk.exists():shutil.copy2(apk,e/'previous.apk');apk.unlink()
cmd=['/var/lib/unityarpg/Unity/Hub/Editor/6000.6.8f1/Editor/Unity','-batchmode','-quit','-force-glcore','-job-worker-count','1','-buildTarget','Android','-projectPath',str(p),'-logFile',str(e/'unity-build.log'),'-executeMethod','AreArpg.EditorTools.ArpgBuildTools.BuildAndroidApk']
start=time.monotonic();code=None
try:
 (e/'state.json').write_text(json.dumps({'state':'building','source':expected,'runnerPid':os.getpid()}))
 code=subprocess.run(cmd,stdout=subprocess.DEVNULL,stderr=subprocess.STDOUT).returncode
finally:
 after={'exit':code,'source':git('rev-parse','HEAD'),'elapsedSeconds':round(time.monotonic()-start,2),'privateUnchanged':before['private']==private(),'prefsUnchanged':before['prefsSha256']==(sha(prefs) if prefs.exists() else None)}
 for name,data in settings.items():(p/name).write_bytes(data)
 if prefbytes is not None and (not prefs.exists() or prefs.read_bytes()!=prefbytes):prefs.write_bytes(prefbytes)
 after['trackedDiff']=git('diff','--name-only');after['settingsRestored']=all((p/name).read_bytes()==data for name,data in settings.items())
 after['logSha256']=sha(e/'unity-build.log') if (e/'unity-build.log').exists() else None
 if code==0 and apk.exists():
  target=e/'Ashen-Crypt-2f296b2-20261115.apk';shutil.copy2(apk,target);after['apk']={'path':str(target),'bytes':target.stat().st_size,'sha256':sha(target)}
 (e/'build-result.json').write_text(json.dumps(after,indent=2)+'\n')
 (e/'state.json').write_text(json.dumps({'state':'completed' if code==0 and 'apk' in after else 'failed','source':expected,'exit':code}))
 print(json.dumps(after),flush=True)
assert code==0 and 'apk' in after and after['privateUnchanged'] and after['prefsUnchanged'] and after['settingsRestored'] and not after['trackedDiff']
