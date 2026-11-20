/** Genuine, ephemeral localhost MCP read probe. Never acts on Unity. */
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {server,snapshot} from './server.mjs';
import assert from 'node:assert/strict';

const department=process.argv[2];
if(typeof department!=='string'||!(/^[a-z][a-z0-9_]{2,48}$/).test(department))
  throw Error('INVALID_DEPARTMENT');
const bytes=readFileSync(process.env.ASHEN_ISSUES_FILE);
if(!bytes.length||bytes.length>65536)throw Error('INVALID_ISSUES_LENGTH');
const data=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(bytes));
if(!data||Object.keys(data).length!==1||!Array.isArray(data.issues)||data.issues.length>100)
  throw Error('INVALID_ISSUES');
const digest=createHash('sha256').update(bytes).digest('hex');
const headers={'content-type':'application/json','accept':'application/json, text/event-stream'};
async function rpc(url,id,method,params={}){
  const response=await fetch(url,{method:'POST',headers,
    body:JSON.stringify({jsonrpc:'2.0',id,method,params}),
    signal:AbortSignal.timeout(8000)});
  assert.equal(response.status,200,method+' must succeed');
  const body=await response.text();
  const messages=body.split('\n').filter(x=>x.startsWith('data: '));
  assert.equal(messages.length,1,method+' one complete event');
  const payload=JSON.parse(messages[0].slice(6));
  if(payload.error)throw Error('MCP_METHOD_FAILED_'+method);
  return payload.result;
}
let running=false;
try{
  const endpoint=await server.listen(0,{host:'127.0.0.1'});
  running=true;
  const init=await rpc(endpoint.url,1,'initialize',{
    protocolVersion:'2025-11-25',capabilities:{},
    clientInfo:{name:'ashen-sovott-advice-readback',version:'1.0.0'}});
  assert.equal(init.serverInfo.name,'ashen-neural-hub');
  assert.ok(init.instructions.includes('director_snapshot'));
  const tools=(await rpc(endpoint.url,2,'tools/list')).tools;
  assert.equal(tools.length,32);
  const names=new Set(tools.map(x=>x.name));
  assert.ok(names.has('advice_'+department),'unknown domain');
  assert.ok(tools.every(x=>x.annotations?.readOnlyHint===true&&
                          x.annotations?.destructiveHint===false));
  const snap=(await rpc(endpoint.url,3,'tools/call',
    {name:'director_snapshot',arguments:{}})).structuredContent;
  assert.equal(snap.snapshotId,snapshot.snapshotId);
  assert.equal(snap.gitRevisionClaim,snapshot.gitRevisionClaim);
  assert.equal(snap.sourceRevisionVerified,false);
  assert.equal(snap.runtimeVerified,false);
  const advisory=(await rpc(endpoint.url,4,'tools/call',{
    name:'advice_'+department,
    arguments:{snapshotId:snap.snapshotId,issues:data.issues}
  })).structuredContent;
  assert.equal(advisory.execution,'NONE');
  assert.equal(advisory.authorization,'NONE');
  assert.equal(advisory.sourceVerification,'SUPPLIED_ISSUE_NOT_INDEPENDENTLY_VERIFIED');
  assert.equal(advisory.department,department);
  assert.ok(['PROPOSE','ABSTAIN'].includes(advisory.status));
  const result={
    schema:'ashen.director.mcp-probe.v1',status:'ADVISORY_ONLY',execution:'NONE',
    sourceRevisionVerified:false,runtimeVerified:false,
    sourceRevisionClaim:snapshot.gitRevisionClaim,snapshotId:snap.snapshotId,
    department,advisorStatus:advisory.status,
    proposal:advisory.proposal??null,issueDigest:digest,
    evidenceType:'REAL_EPHEMERAL_LOCALHOST_MCP_TOOLS_CALL',
    toolCount:tools.length
  };
  console.log(JSON.stringify(result));
}finally{
  if(running)await server.close();
}
