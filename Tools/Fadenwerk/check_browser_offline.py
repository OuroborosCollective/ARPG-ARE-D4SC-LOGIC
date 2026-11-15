import pathlib,json,sys,os,shutil
from playwright.sync_api import sync_playwright
html=(pathlib.Path(__file__).resolve().parents[2]/'Packages/com.ouroboros.fadenwerk/Editor/Web/FadenwerkUI.html').read_text().replace('<head>','<head><meta name="fadenwerk-owner-proxy" content="true">',1)
state={'ok':True,'rev':'fixture','project':'Browser failure test','unity':'6000.6.8f1','playing':False,'autoSave':False,'db':{'path':'fixture','worldSeed':'fixture'},'scenes':[],'projectScenes':[],'npcs':[],'markers':[],'quests':[],'dialogues':[],'kits':[],'items':[],'issues':[],'map':{'hasImage':False,'rev':0,'minU':0,'minV':0,'maxU':100,'maxV':100}}
with sync_playwright() as pw:
 b=pw.chromium.launch(executable_path=os.environ.get('CHROMIUM_PATH') or shutil.which('chromium'),headless=True,args=['--no-sandbox']);p=b.new_page();mode={'offline':False}
 p.route('https://fadenwerk.test/',lambda route:route.fulfill(status=200,content_type='text/html',body=html))
 p.route('https://fadenwerk.test/api/state*',lambda route:route.fulfill(status=503 if mode['offline'] else 200,content_type='application/json',body=json.dumps({'ok':False,'error':'Editor temporarily unavailable'} if mode['offline'] else state)))
 p.goto('https://fadenwerk.test/');p.wait_for_selector('#status .dot.ok',timeout=5000);mode['offline']=True
 try:
  p.wait_for_selector('#status .dot.bad',timeout=8000);assert 'Editor temporarily unavailable' in p.locator('#status').inner_text();print('PASS: HTTP 503 changes online indicator to offline; browser response fixture, not Unity evidence')
 finally:b.close()
