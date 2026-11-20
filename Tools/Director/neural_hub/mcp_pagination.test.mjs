import test from 'node:test';
import assert from 'node:assert/strict';
import {collectMcpList} from './mcp_pagination.mjs';
const page=(field,items,cursor=undefined,resultType=undefined)=>({
  jsonrpc:'2.0',id:1,result:{...(resultType===undefined?{}:{resultType}),[field]:items,
    ...(cursor===undefined?{}:{nextCursor:cursor})}
});
test('follows opaque, even empty, cursor through empty intermediate pages',async()=>{
  const sent=[];
  const pages=[page('tools',[{name:'a'}],''),page('tools',[],'binary-!'),page('tools',[{name:'b'}],null)];
  const result=await collectMcpList({method:'tools/list',request:async(method,params)=>{
    sent.push({method,params});return pages[sent.length-1];
  }});
  assert.deepEqual(result.items.map(x=>x.name),['a','b']);
  assert.equal(result.pages,3);
  assert.deepEqual(sent,[
    {method:'tools/list',params:{}},
    {method:'tools/list',params:{cursor:''}},
    {method:'tools/list',params:{cursor:'binary-!'}}
  ]);
});
test('supports modern and legacy complete single pages',async()=>{
  for(const type of [undefined,'complete']){
    const result=await collectMcpList({method:'prompts/list',
      request:async()=>page('prompts',[{name:'brief'}],undefined,type)});
    assert.deepEqual(result.items,[{name:'brief'}]);assert.equal(result.pages,1);
  }
});
test('resources and resourceTemplates list field names are correct',async()=>{
  for(const [method,field,item] of [['resources/list','resources',{uri:'file:///x'}],
    ['resources/templates/list','resourceTemplates',{uriTemplate:'file:///{id}'}]]){
    const result=await collectMcpList({method,request:async()=>page(field,[item])});
    assert.deepEqual(result.items,[item]);
  }
});
test('rejects repeat cursor and keeps bounded page, item and byte budgets',async()=>{
  let called=0;
  await assert.rejects(collectMcpList({method:'tools/list',request:async()=>{called++;return page('tools',[],'x');}}),/REPEATED_CURSOR/);
  assert.equal(called,2);
  await assert.rejects(collectMcpList({method:'tools/list',maxPages:1,request:async()=>page('tools',[],'x')}),/PAGE_LIMIT/);
  await assert.rejects(collectMcpList({method:'tools/list',maxItems:1,request:async()=>page('tools',[{name:'a'},{name:'b'}])}),/ITEM_LIMIT/);
  await assert.rejects(collectMcpList({method:'tools/list',maxBytes:10,request:async()=>page('tools',[{name:'a'}])}),/BYTE_LIMIT/);
});
test('fails closed on malformed RPC responses, cursor and resultType',async()=>{
  for(const [rpc,err] of [
    [{jsonrpc:'2.0',id:1,error:{code:-32602,message:'Invalid params'}},/MCP_RPC_-32602/],
    [page('tools',[],33),/INVALID_CURSOR/],
    [page('tools',[],'x'.repeat(4097)),/INVALID_CURSOR/],
    [page('tools',[],'','input_required'),/INVALID_RESULT_TYPE/],
    [{result:{tools:{}}},/INVALID_ITEMS/],
    [{result:{prompts:[]}},/INVALID_ITEMS/]
  ])await assert.rejects(collectMcpList({method:'tools/list',request:async()=>rpc}),err);
  let called=false;
  await assert.rejects(collectMcpList({method:'models/list',request:async()=>{called=true;return page('models',[]);}}),/UNSUPPORTED_LIST_METHOD/);
  assert.equal(called,false);
});

test('resource identity uses URI not display name; templates use uriTemplate',async()=>{
  const resources=[{name:'README.md',uri:'file:///project-a/README.md'},
    {name:'README.md',uri:'file:///project-b/README.md'}];
  const listed=await collectMcpList({method:'resources/list',
    request:async()=>page('resources',resources)});
  assert.equal(listed.items.length,2);
  await assert.rejects(collectMcpList({method:'resources/list',
    request:async()=>page('resources',[resources[0],{name:'other',uri:resources[0].uri}])}),/DUPLICATE_ITEM/);
  await assert.rejects(collectMcpList({method:'resources/templates/list',
    request:async()=>page('resourceTemplates',[{name:'a',uriTemplate:'asset://{id}'},
      {name:'b',uriTemplate:'asset://{id}'}])}),/DUPLICATE_ITEM/);
  await assert.rejects(collectMcpList({method:'tools/list',
    request:async()=>page('tools',[{name:'same',uri:'a'},{name:'same',uri:'b'}])}),/DUPLICATE_ITEM/);
});
