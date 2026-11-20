import assert from 'node:assert/strict';
import {server,snapshot} from './server.mjs';
import {collectMcpList} from './mcp_pagination.mjs';

const endpoint=await server.listen(0,{host:'127.0.0.1'});
let id=0;
const req=async(method,params={},options={})=>{
  const version=options.version??'2026-07-28';
  const meta={
    'io.modelcontextprotocol/protocolVersion':version,
    'io.modelcontextprotocol/clientInfo':{name:'ashen-modern-canary',version:'1.0.0'},
    'io.modelcontextprotocol/clientCapabilities':{}
  };
  const headers={
    'content-type':'application/json',
    'accept':'application/json, text/event-stream',
    'mcp-protocol-version':options.headerVersion??version,
    'mcp-method':method
  };
  if(options.omitMethod)delete headers['mcp-method'];
  if(options.origin)headers.origin=options.origin;
  if(method==='tools/call')headers['mcp-name']=params.name;
  const res=await fetch(endpoint.url,{
    method:'POST',headers,
    body:JSON.stringify({jsonrpc:'2.0',id:++id,method,params:{...params,_meta:meta}}),
    signal:AbortSignal.timeout(6000)
  });
  const body=await res.text();
  const data=body.split('\n').filter(line=>line.startsWith('data: ')).at(-1);
  const rpc=JSON.parse(data?data.slice(6):body);
  return {status:res.status,rpc};
};
try{
  const discovered=await req('server/discover');
  assert.equal(discovered.status,200);
  assert.equal(discovered.rpc.result.resultType,'complete');
  assert.ok(discovered.rpc.result.supportedVersions.includes('2026-07-28'));
  const tools=await collectMcpList({method:'tools/list',request:async(method,params)=>{
    const res=await req(method,params);assert.equal(res.status,200);return res.rpc;
  }});
  assert.equal(tools.items.length,32);
  const prompts=await collectMcpList({method:'prompts/list',request:async(method,params)=>{
    const res=await req(method,params);assert.equal(res.status,200);return res.rpc;
  }});
  assert.equal(prompts.items.length,28);
  const snap=await req('tools/call',{name:'director_snapshot',arguments:{}});
  assert.equal(snap.status,200);
  assert.equal(snap.rpc.result.structuredContent.snapshotId,snapshot.snapshotId);
  assert.equal(snap.rpc.result.structuredContent.runtimeVerified,false);
  const unknown=await req('server/discover',{}, {version:'1900-01-01'});
  assert.equal(unknown.status,400);
  assert.equal(unknown.rpc.error.code,-32022);
  assert.ok(unknown.rpc.error.data.supported.includes('2026-07-28'));
  const mismatch=await req('server/discover',{}, {headerVersion:'2025-11-25'});
  assert.equal(mismatch.status,400);
  assert.equal(mismatch.rpc.error.code,-32020);
  const missingMethod=await req('tools/list',{}, {omitMethod:true});
  assert.equal(missingMethod.status,400);
  assert.equal(missingMethod.rpc.error.code,-32020);
  const foreignOrigin=await req('tools/list',{}, {origin:'https://untrusted.invalid'});
  assert.equal(foreignOrigin.status,403, 'FOREIGN_ORIGIN_MUST_BE_REJECTED');
  console.log('MODERN_MCP_CANARY',JSON.stringify({
    status:'PASS',modernProtocol:'2026-07-28',legacyCanarySeparate:true,
    discoverStatus:discovered.status,tools:tools.items.length,toolPages:tools.pages,
    prompts:prompts.items.length,promptPages:prompts.pages,
    snapshotReadOnly:true,unsupportedVersionCode:unknown.rpc.error.code,
    headerMismatchCode:mismatch.rpc.error.code,
    missingMethodCode:missingMethod.rpc.error.code,
    rejectedForeignOrigin:true,unityRuntimeVerified:false
  }));
}finally{await server.close();}
