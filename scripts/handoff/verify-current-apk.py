from pathlib import Path
import subprocess,json,hashlib,zipfile,os,re
p=Path('/var/lib/unityarpg/workspaces/are-arpg-forges-20261114');e=Path('/var/lib/unityarpg/.local/share/are-arpg/game-apk-handoff-20261115')
r=json.loads((e/'build-result.json').read_text());assert r['exit']==0 and 'apk' in r
apk=Path(r['apk']['path']);expected='2f296b26767ad0746a2ca26468ef299f91e43b57';assert r['source']==expected
# Build preprocessing can serialize tracked rendering assets. Preserve and restore only
# changes created after the runner's clean-tree preflight, never unrelated user work.
changed=subprocess.check_output(['git','-C',str(p),'diff','--name-only'],text=True).splitlines()
restored=[]
for name in changed:
 assert name.startswith(('ProjectSettings/','Assets/')) and name.endswith('.asset'),name
 original=subprocess.check_output(['git','-C',str(p),'show',expected+':'+name])
 target=e/'generated-before-restore'/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes((p/name).read_bytes())
 (p/name).write_bytes(original);restored.append(name)
assert not subprocess.check_output(['git','-C',str(p),'diff','--name-only'],text=True).strip()
for k in ['privateUnchanged','prefsUnchanged','settingsRestored']:assert r[k],k
unity=Path('/var/lib/unityarpg/Unity/Hub/Editor/6000.6.8f1/Editor');android=unity/'Data/PlaybackEngines/AndroidPlayer';b=android/'SDK/build-tools/36.0.0'
env=dict(os.environ);env['JAVA_HOME']=str(android/'OpenJDK');env['PATH']=env['JAVA_HOME']+'/bin:'+env.get('PATH','')
def run(name,args):
 v=subprocess.run(args,capture_output=True,text=True,env=env);(e/(name+'.txt')).write_text(v.stdout+v.stderr);assert v.returncode==0,(name,v.returncode);return v.stdout+v.stderr
badging=run('apk-badging',[str(b/'aapt'),'dump','badging',str(apk)])
sign=run('apk-signature',[str(b/'apksigner'),'verify','--verbose','--print-certs',str(apk)])
run('apk-alignment',[str(b/'zipalign'),'-c','-P','16','-v','4',str(apk)])
assert "package: name='com.ouroboroscollective.areashencrypt'" in badging
assert "native-code: 'arm64-v8a'" in badging
cert=lambda t:next(x.split(': ',1)[1] for x in t.splitlines() if x.startswith('Signer #1 certificate SHA-256 digest:'))
old=e.parent/'private-apk/ARE-Ashen-Crypt-HUD-9f679842.apk'
match=None
if old.exists():match=cert(sign)==cert(run('previous-apk-signature',[str(b/'apksigner'),'verify','--print-certs',str(old)]))
with zipfile.ZipFile(apk) as z:
 assert z.testzip() is None;names=z.namelist();assert 'lib/arm64-v8a/libil2cpp.so' in names
 assert not any('/armeabi-v7a/' in n or '/x86/' in n for n in names)
log=(e/'unity-build.log').read_text(errors='replace');assert 'ARE ARPG Android build succeeded:' in log
h=hashlib.sha256(apk.read_bytes()).hexdigest();assert h==r['apk']['sha256']
receipt={'schema':'ashen.game-apk-handoff.v1','source_commit_sha':expected,'artifact_name':apk.name,'artifact_sha256':h,'artifact_size_bytes':apk.stat().st_size,'unity':'6000.6.8f1','package_id':'com.ouroboroscollective.areashencrypt','architecture':'arm64-v8a','scripting_backend':'IL2CPP','apk_signature_verified':True,'signer_sha256':cert(sign),'matches_previous_hud_signer':match,'zip_integrity_verified':True,'zipalign_16k_verified':True,'unity_build_exit':0,'build_origin':'native VPS, not GitHub Actions','device_installation':'UNVERIFIED','device_runtime':'UNVERIFIED','device_performance':'UNVERIFIED','private_assets_unchanged':True,'prefs_unchanged':True,'settings_restored':True,'tracked_tree_restored_clean':True,'generated_assets_restored':restored,'build_log_sha256':r['logSha256'],'elapsed_seconds':r['elapsedSeconds'],'entry_scenes':['Assets/Scenes/AshenEntry.unity','Assets/Scenes/Arena.unity']}
(e/'apk-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps(receipt,indent=2))
