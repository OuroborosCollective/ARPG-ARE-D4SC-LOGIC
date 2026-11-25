#!/usr/bin/env node
/* Real Chromium, explicitly synthetic HTTP responses. No live Unity or authentication evidence. */
'use strict';
const fs = require('node:fs'), crypto = require('node:crypto'), assert = require('node:assert/strict');
const args = {};
for (let i = 2; i < process.argv.length; i += 2) {
  if (!process.argv[i].startsWith('--') || process.argv[i + 1] === undefined) throw new Error('Use --key value arguments');
  args[process.argv[i].slice(2)] = process.argv[i+1];
}
for (const key of ['html','out','playwright','browser','phase']) assert(args[key], 'Missing --' + key);
const html = fs.readFileSync(args.html), hash = b => crypto.createHash('sha256').update(b).digest('hex');
const { chromium } = require(args.playwright);
const origin = 'http://art-scan-capacity.test', projectSha = 'a'.repeat(40), graphRevision = 'b'.repeat(64);
const nodes = Array.from({length:6}, (_,i)=>({
  id:'fixture.'+i,label:'Fixture '+i,kind:'source',category:'props',parentIds:[],variantGroup:'Test fixtures',
  artifactSha256:'1'.repeat(64),provenance:{provider:'Synthetic UI fixture',sourceRef:'fixture:source',sourceSha256:'2'.repeat(64),receiptSha256:'3'.repeat(64)},
  license:{state:'unverified',identifier:'',evidenceRef:''},reviewState:'review-required'
}));
const identity={ok:true,readOnly:true,schema:'ashen.art-graph.v1',projectSha,graphRevision,diagnostics:[]};
const graph={...identity,nodes}, state={...identity,nodeCount:nodes.length};
const report={phase:args.phase,htmlSha256:hash(html),scriptSha256:hash(fs.readFileSync(__filename)),runtimeEvidence:false,transport:'synthetic',results:[]};
const delay = ms => new Promise(resolve=>setTimeout(resolve,ms));
async function until(predicate,label) {
  for(let i=0;i<400;i++){if(predicate())return;await delay(10);}
  throw Error('Timeout: '+label);
}
async function run(browser,viewport,scenario){
  const context=await browser.newContext({viewport,hasTouch:true});
  context.setDefaultTimeout(5000);
  await context.addInitScript(()=>localStorage.setItem('fw.token','synthetic-test-only'));
  const page=await context.newPage(), pending=new Map(), held=new Map(), starts=[], cancels=[], errors=[];
  let ticketCounter=0;
  const result={viewport,scenario,starts,cancels,errors,passed:false};
  page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/*',async route=>{
    const req=route.request(), u=new URL(req.url()), q=Object.fromEntries(u.searchParams);
    if(u.origin===origin&&u.pathname==='/'){await route.fulfill({status:200,contentType:'text/html',body:html});return;}
    if(u.origin!==origin||req.method()!=='GET'||!/^\/api\/x\/art\/(state|graph|asset)$/.test(u.pathname)){await route.abort();return;}
    const send=(status,data)=>route.fulfill({status,contentType:'application/json',body:JSON.stringify(data)});
    const endpoint=u.pathname.split('/').pop();
    if(endpoint==='state'){await send(200,state);return;}
    if(endpoint==='graph'){await send(200,graph);return;}
    if(q.cancelScan==='1'){
      cancels.push(q.id);
      const entry=pending.get(q.scanId);
      if(!entry||entry.id!==q.id||entry.expectedProjectSha!==q.expectedProjectSha||entry.expectedGraphRevision!==q.expectedGraphRevision){
        await send(410,{ok:false,code:'ART_SCAN_EXPIRED',error:'expired'});return;
      }
      pending.delete(q.scanId);
      await send(200,{ok:true,readOnly:true,code:'ART_SCAN_ABANDONED'});return;
    }
    if(!q.scanId){
      starts.push(q.id);
      const ticket=(++ticketCounter).toString(16).padStart(32,'0');
      pending.set(ticket,q);
      await send(202,{ok:false,readOnly:true,code:'ART_SCAN_PENDING',scanId:ticket,retryAfterMs:100,error:'pending'});return;
    }
    const entry=pending.get(q.scanId);
    if(!entry||entry.id!==q.id||entry.expectedProjectSha!==q.expectedProjectSha||entry.expectedGraphRevision!==q.expectedGraphRevision){await send(410,{ok:false,code:'ART_SCAN_EXPIRED',error:'expired'});return;}
    held.set(q.id,{send,ticket:q.scanId});
  });
  const click=async index=>page.evaluate(i=>document.querySelectorAll('#artnodes .art-node')[i].click(),index);
  const release=async (id,status)=>{
    const x=held.get(id);assert(x,'No pending request for '+id);
    pending.delete(x.ticket);held.delete(id);
    await x.send(status,status===200?{...identity,node:nodes.find(n=>n.id===id)}:{ok:false,code:status===410?'ART_SCAN_EXPIRED':'GRAPH_REVISION_CONFLICT',error:'synthetic terminal error'});
  };
  try{
    await page.goto(origin+'/?bau&art');
    await page.waitForFunction(()=>document.querySelectorAll('#artnodes .art-node').length===6);
    if(scenario==='reload-abandons-ticket'){
      await click(0);await until(()=>held.has('fixture.0'),'first poll');
      await page.locator('#artrefresh').click();
      await page.waitForFunction(()=>document.querySelectorAll('#artnodes .art-node').length===6);
      // Return another pending response to the superseded browser epoch.
      const x=held.get('fixture.0');held.delete('fixture.0');
      await x.send(202,{ok:false,readOnly:true,code:'ART_SCAN_PENDING',scanId:x.ticket,retryAfterMs:100,error:'pending'});
      await until(()=>cancels.includes('fixture.0'),'abandoned scan cleanup');
      assert.equal(await page.locator('#artnodes .art-node').count(),6);
    } else {
      await click(0);await click(1);
      await until(()=>held.has('fixture.0')&&held.has('fixture.1'),'two overlapping detail polls');
      await page.evaluate(()=>{for(let i=2;i<6;i++)document.querySelectorAll('#artnodes .art-node')[i].click();});
      await delay(350);
      assert.deepEqual(starts,['fixture.0','fixture.1'],'Rapid selections must not start more than two simultaneous detail scans.');
      if(scenario==='reclaim-410-invalidates'){
        await release('fixture.0',410);
        await page.waitForFunction(()=>document.querySelectorAll('#artnodes .art-node').length===0);
        await release('fixture.1',200);
        await delay(250);
        assert.equal(await page.locator('#artnodes .art-node').count(),0);
        assert.deepEqual(starts,['fixture.0','fixture.1'],'A queued selection must not restart an invalidated graph.');
      }else{
        await release('fixture.0',200);
        await until(()=>held.has('fixture.5'),'latest queued selection starts');
        assert.deepEqual(starts,['fixture.0','fixture.1','fixture.5'],'Only the latest queued selection may begin.');
        await release('fixture.5',200);
        await page.waitForFunction(()=>document.querySelector('#artdetail h2')?.textContent==='Fixture 5');
        await release('fixture.1',200);
        await delay(150);
        assert.equal(await page.locator('#artdetail h2').innerText(),'Fixture 5');
      }
    }
    assert.deepEqual(errors,[]);
    result.passed=true;
  }catch(e){result.failure=e.message;}finally{await context.close();}
  report.results.push(result);console.log((result.passed?'PASS ':'FAIL ')+scenario+' '+viewport.width+'x'+viewport.height+(result.failure?': '+result.failure:''));
}
(async()=>{
  const browser=await chromium.launch({executablePath:args.browser,headless:true,args:['--no-sandbox','--disable-dev-shm-usage']});
  try{for(const viewport of [{width:1007,height:601},{width:601,height:1007},{width:390,height:844}])for(const scenario of ['latest-only','reclaim-410-invalidates','reload-abandons-ticket'])await run(browser,viewport,scenario);}
  finally{await browser.close();fs.mkdirSync(args.out,{recursive:true});fs.writeFileSync(args.out+'/results.json',JSON.stringify(report,null,2)+'\n');}
  process.exitCode=report.results.every(r=>r.passed)?0:1;
})().catch(e=>{console.error(e);process.exitCode=1;});
