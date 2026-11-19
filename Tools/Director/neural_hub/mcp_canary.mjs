import {server,snapshot} from './server.mjs';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {collectMcpList} from './mcp_pagination.mjs';

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
  assert.equal(typeof response.result.instructions,'string',
    'initialize must supply harness-aware MCP guidance');
  for(const required of ['director_snapshot','director_consult','caller-supplied',
                         'Unity Game Director','NOT_EXECUTED']){
    assert.ok(response.result.instructions.includes(required),
      'initialize guidance must mention '+required);
  }
  const t=await collectMcpList({method:'tools/list',
    request:(method,params)=>request(endpoint.url,2,method,params)});
  const tools=t.items;
  assert.equal(tools.length,32,'28 specialists + 4 director tools');
  assert.equal(new Set(tools.map(tool=>tool.name)).size,32);
  const names=new Set(tools.map(tool=>tool.name));
  for(const advisor of snapshot.advisors)assert.ok(names.has('advice_'+advisor.id));
  for(const tool of tools){
    assert.equal(tool.annotations?.readOnlyHint,true,tool.name+' read only');
    assert.equal(tool.annotations?.destructiveHint,false,tool.name+' no destructive effects');
  }
  const prompts=(await collectMcpList({method:'prompts/list',
    request:(method,params)=>request(endpoint.url,3,method,params)})).items;
  assert.equal(prompts.length,28,'one prompt for each specialist');
  const snap=await request(endpoint.url,4,'tools/call',{name:'director_snapshot',arguments:{}});
  const identity=snap.result.structuredContent;
  assert.equal(identity.snapshotId,snapshot.snapshotId);
  assert.equal(identity.sourceRevisionVerified,false);
  assert.equal(identity.runtimeVerified,false);
  const fixtureIssue={number:66,state:'open',title:'WorldForge larger region maps',
    url:'https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/66'};
  let issues=[fixtureIssue],suppliedIssuesSha256=null;
  if(process.env.ASHEN_ISSUES_FILE){
    const bytes=readFileSync(process.env.ASHEN_ISSUES_FILE);
    assert.ok(bytes.length>1&&bytes.length<=65536,'bounded issue input');
    const payload=JSON.parse(bytes.toString('utf8'));
    assert.ok(payload&&Object.keys(payload).length===1&&Array.isArray(payload.issues),
      'input only issues, no extra instructions');
    issues=payload.issues;
    suppliedIssuesSha256=createHash('sha256').update(bytes).digest('hex');
  }
  const answer=await request(endpoint.url,5,'tools/call',{
    name:'advice_world_design',arguments:{snapshotId:snapshot.snapshotId,issues}
  });
  assert.equal(answer.result.structuredContent.status,
    issues.some(x=>x.number===66&&x.state==='open')?'PROPOSE':'ABSTAIN');
  assert.equal(answer.result.structuredContent.execution,'NONE');
  assert.equal(answer.result.structuredContent.sourceVerification,'SUPPLIED_ISSUE_NOT_INDEPENDENTLY_VERIFIED');
  const all=await request(endpoint.url,6,'tools/call',{
    name:'director_consult',arguments:{snapshotId:snapshot.snapshotId,issues}
  });
  assert.equal(all.result.structuredContent.consulted,28);
  assert.ok(['THREE_PATHS_READY_FOR_DIRECTOR_REVIEW','INSUFFICIENT_DISTINCT_PATHS']
    .includes(all.result.structuredContent.status));
  assert.equal(all.result.structuredContent.runtimeVerified,false);
  const sourceCatalog=await request(endpoint.url,7,'tools/call',{
    name:'director_sources',arguments:{snapshotId:snapshot.snapshotId}
  });
  assert.equal(sourceCatalog.result.structuredContent.sourceCount,13);
  assert.equal(sourceCatalog.result.structuredContent.execution,'NONE');
  assert.equal(sourceCatalog.result.structuredContent.installedVerified,false);
  const sources=await request(endpoint.url,8,'tools/call',{
    name:'director_source_discover',
    arguments:{snapshotId:snapshot.snapshotId,department:'testing',limit:6}
  });
  assert.ok(sources.result.structuredContent.candidates.length>0);
  assert.ok(sources.result.structuredContent.candidates.length<=6);
  assert.equal(sources.result.structuredContent.authorization,'NONE');
  assert.equal(sources.result.structuredContent.runtimeVerified,false);
  const advisorFindings=[];
  let nextId=30;
  for(const advisor of snapshot.advisors){
    const call=await request(endpoint.url,nextId++,'tools/call',{
      name:'advice_'+advisor.id,
      arguments:{snapshotId:snapshot.snapshotId,issues}
    });
    assert.equal(call.result.structuredContent.department,advisor.id);
    assert.equal(call.result.structuredContent.execution,'NONE');
    assert.equal(call.result.structuredContent.authorization,'NONE');
    assert.equal(call.result.structuredContent.sourceVerification,
      'SUPPLIED_ISSUE_NOT_INDEPENDENTLY_VERIFIED');
    assert.ok(['PROPOSE','ABSTAIN'].includes(call.result.structuredContent.status));
    advisorFindings.push({
      department:advisor.id,status:call.result.structuredContent.status,
      proposal:call.result.structuredContent.proposal??null
    });
  }
  assert.equal(advisorFindings.length,28,'all advisory tools actually called over MCP');
  console.log(JSON.stringify({
    schema:'ashen.mcp-protocol-canary.v2',status:'PASS',
    toolCount:tools.length,promptCount:prompts.length,advisorCount:snapshot.advisors.length,
    registeredExternalSources:sourceCatalog.result.structuredContent.sourceCount,
    sourceRevision:snapshot.gitRevisionClaim,snapshotId:snapshot.snapshotId,
    outputProvenance:'SUPPLIED_ISSUE_NOT_INDEPENDENTLY_VERIFIED',
    suppliedIssuesSha256,issueCount:issues.length,individualAdvisorsCalled:advisorFindings.length,
    advised:advisorFindings.filter(a=>a.status==='PROPOSE').length,
    abstained:advisorFindings.filter(a=>a.status==='ABSTAIN').length,
    advisorFindings,councilStatus:all.result.structuredContent.status,
    councilPaths:all.result.structuredContent.paths,
    runtimeVerified:false,productUnityVerified:false
  }));
}finally{
  if(opened)await server.close();
}
