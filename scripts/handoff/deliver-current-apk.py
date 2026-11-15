from pathlib import Path
import subprocess,shutil,json,hashlib,urllib.request,urllib.error,runpy,threading
root=Path('/var/lib/unityarpg/.local/share/are-arpg');e=root/'game-apk-handoff-20261115';base=root/'private-apk';server=root/'private-apk-server.py'
r=json.loads((e/'apk-receipt.json').read_text());assert r['unity_build_exit']==0 and r['apk_signature_verified'] and r['zip_integrity_verified']
apk=e/r['artifact_name'];assert hashlib.sha256(apk.read_bytes()).hexdigest()==r['artifact_sha256']
assert not (base/apk.name).exists(),'Refuse overwrite'
shutil.copy2(apk,base/apk.name);receiptName='apk-game-2f296b2-20261115-receipt.json';shutil.copy2(e/'apk-receipt.json',base/receiptName)
old=server.read_text();oldsha=hashlib.sha256(server.read_bytes()).hexdigest()
assert 'CURRENT_GAME_APK' not in old
constants='\nCURRENT_GAME_APK = BASE / '+repr(apk.name)+'\nCURRENT_GAME_RECEIPT = BASE / '+repr(receiptName)+'\nCURRENT_GAME_SOURCE = '+repr(r['source_commit_sha'])+'\n\n'
new=old.replace('class Handler(BaseHTTPRequestHandler):',constants+'class Handler(BaseHTTPRequestHandler):',1)
marker='        path = self.path\n'
assert new.count(marker)==1
block='''        if path in ("/" + CURRENT_GAME_APK.name, "/" + CURRENT_GAME_RECEIPT.name):
            try:
                validate_artifact(CURRENT_GAME_APK, CURRENT_GAME_RECEIPT, CURRENT_GAME_SOURCE)
            except (RuntimeError, OSError, ValueError):
                self.send_error(503, "Artifact validation failed")
                return
            target = CURRENT_GAME_APK if path == "/" + CURRENT_GAME_APK.name else CURRENT_GAME_RECEIPT
            self.send_response(200)
            self.send_header("Content-Type", "application/vnd.android.package-archive" if target == CURRENT_GAME_APK else "application/json; charset=utf-8")
            self.send_header("Content-Length", str(target.stat().st_size))
            self.send_header("Cache-Control", "private, no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.send_header("Referrer-Policy", "no-referrer")
            if target == CURRENT_GAME_APK:
                self.send_header("Content-Disposition", 'attachment; filename="' + target.name + '"')
            self.end_headers()
            if body:
                with target.open("rb") as stream:
                    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                        self.wfile.write(chunk)
            return
'''
new=new.replace(marker,marker+block,1)
staged=e/'private-apk-server-staged.py';staged.write_text(new)
compile(new,str(staged),'exec')
d=runpy.run_path(str(staged));d['validate_artifact'](d['CURRENT_GAME_APK'],d['CURRENT_GAME_RECEIPT'],d['CURRENT_GAME_SOURCE'])
host=d['ThreadingHTTPServer'](('127.0.0.1',39132),d['Handler']);t=threading.Thread(target=host.serve_forever,daemon=True);t.start()
def probe(port):
 url='http://127.0.0.1:'+str(port)+'/'+apk.name
 h=hashlib.sha256();size=0
 with urllib.request.urlopen(url,timeout=120) as response:
  for chunk in iter(lambda:response.read(1024*1024),b''):h.update(chunk);size+=len(chunk)
  status=response.status;typ=response.headers.get('Content-Type')
 assert size==r['artifact_size_bytes'] and h.hexdigest()==r['artifact_sha256'] and status==200
 try:urllib.request.urlopen('http://127.0.0.1:'+str(port)+'/does-not-exist',timeout=5);raise AssertionError('Unknown route permitted')
 except urllib.error.HTTPError as x:assert x.code==404
 return {'status':status,'bytes':size,'sha256':h.hexdigest(),'contentType':typ,'unknownPath':404}
try:staging=probe(39132)
finally:host.shutdown();host.server_close()
assert hashlib.sha256(server.read_bytes()).hexdigest()==oldsha
shutil.copy2(server,e/'private-apk-server-before.py')
server.write_text(new)
subprocess.run(['sudo','-n','systemctl','restart','are-arpg-apk-private.service'],check=True)
# systemd start can report before the socket begins listening; bounded retries.
import time
for i in range(20):
 try:live=probe(39131);break
 except (ConnectionError,urllib.error.URLError):time.sleep(1)
else:raise RuntimeError('Live relay did not become ready')
url='https://arelogic.space/arpg-apk/'+apk.name
try:
 resp=urllib.request.urlopen(urllib.request.Request(url,method='HEAD'),timeout=30);https=resp.status
except urllib.error.HTTPError as x:https=x.code
result={'source':r['source_commit_sha'],'staging':staging,'live':live,'url':url,'anonymousHttpsStatus':https,'oldScriptSha256':oldsha,'newScriptSha256':hashlib.sha256(server.read_bytes()).hexdigest(),'existingRoutesPreserved':True,'authConfigurationChanged':False}
(e/'delivery-readback.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
