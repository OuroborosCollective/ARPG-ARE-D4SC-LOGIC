import {server,snapshot} from './server.mjs';
import assert from 'node:assert/strict';

const headers={'content-type':'application/json','accept':'application/json, text/event-stream'};
const request = async (url,id,method,params={}) => {
  const res=await fetch(url,{
    method:'POST',headers,
    body:JSON.stringify({jsonrpc:'2.0',id,method,params}),
    signal:AbortSignal.timeout(15000)
  });
  assert.equal(res.status,200,method+' HTTP response');
  const payload=await res.text();
  const json=payload.split('\n').find(line=>line.startsWith('data: '));
  assert.ok(json,method+' valid SSE message');
  return JSON.parse(json.slice(6));
};
let opened=false;
try{
  const endpoint=await server.listen(0,{host:'127.0.0.1'});
  opened=true;
  const response=await request(endpoint.url,1,'initialize',{
    protocolVersion:'2025-11-25',capabilities:{},
    clientInfo:{name:'ashen-neural-canary',version:'0.1.0'}
  });
  assert.equal(response.result.serverInfo.name,'ashen-neural-hub');
  const t=await request(endpoint.url,2,'tools/list');
  const tools=t.result.tools;
  assert.equal(tools.length,30,'28 specialists + 2 director tools');
  assert.equal(new Set(tools.map(tool=>tool.name)).size,30);
  const names=new Set(tools.map(tool=>tool.name));
  for(const advisor of snapshot.advisors)assert.ok(names.has('advice_'+advisor.id));
  for(const tool of tools){
    assert.equal(tool.annotations?.readOnlyHint,true,tool.name+' read only');
    assert.equal(tool.annotations?.destructiveHint,false,tool.name+' no destructive effects');
  }
  const prompts=(await request(endpoint.url,3,'prompts/list')).result.prompts;
  assert.equal(prompts.length,28,'one prompt for each specialist');
  const snap=await request(endpoint.url,4,'tools/call',{name:'director_snapshot',arguments:{}});
  const identity=snap.result.structuredContent;
  assert.equal(identity.snapshotId,snapshot.snapshotId);
  assert.equal(identity.sourceRevisionVerified,false);
  assert.equal(identity.runtimeVerified,false);
  const issue={number:66,state:'open',title:'WorldForge larger region maps',
    url:'https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/66'};
  const answer=await request(endpoint.url,5,'tools/call',{
    name:'advice_world_design',arguments:{snapshotId:snapshot.snapshotId,issues:[issue]}
  });
  assert.equal(answer.result.structuredContent.status,'PROPOSE');
  assert.equal(answer.result.structuredContent.execution,'NONE');
  assert.equal(answer.result.structuredContent.sourceVerification,'SUPPLIED_ISSUE_NOT_INDEPENDENTLY_VERIFIED');
  const all=await request(endpoint.url,6,'tools/call',{
    name:'director_consult',arguments:{snapshotId:snapshot.snapshotId,issues:[issue]}
  });
  assert.equal(all.result.structuredContent.consulted,28);
  assert.equal(all.result.structuredContent.status,'INSUFFICIENT_DISTINCT_PATHS');
  assert.equal(all.result.structuredContent.runtimeVerified,false);
  console.log(JSON.stringify({
    schema:'ashen.mcp-protocol-canary.v1',status:'PASS',
    toolCount:tools.length,promptCount:prompts.length,advisorCount:snapshot.advisors.length,
    sourceRevision:snapshot.gitRevisionClaim,snapshotId:snapshot.snapshotId,
    outputProvenance:'SUPPLIED_ISSUE_NOT_INDEPENDENTLY_VERIFIED',
    runtimeVerified:false,productUnityVerified:false
  }));
}finally{
  if(opened)await server.close();
}
