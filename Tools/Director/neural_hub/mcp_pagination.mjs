const LIST_FIELDS=Object.freeze({
  'tools/list':'tools',
  'prompts/list':'prompts',
  'resources/list':'resources',
  'resources/templates/list':'resourceTemplates'
});
const positiveInteger=(v,label)=>{
  if(!Number.isSafeInteger(v)||v<1)throw Error('INVALID_'+label);
  return v;
};

/**
 * Collect all pages of a server-owned MCP catalog, without interpreting
 * opaque cursors or assuming that nonempty pages precede the final page.
 * The request callback is the authenticated transport selected by the caller.
 * Does not grant access, change the protocol version, or authorize a tool call.
 */
export async function collectMcpList({
  method,request,maxPages=64,maxItems=10000,maxBytes=2*1024*1024
}={}){
  if(!Object.hasOwn(LIST_FIELDS,method))throw Error('UNSUPPORTED_LIST_METHOD');
  if(typeof request!=='function')throw Error('MISSING_REQUEST');
  positiveInteger(maxPages,'PAGE_LIMIT');
  positiveInteger(maxItems,'ITEM_LIMIT');
  positiveInteger(maxBytes,'BYTE_LIMIT');
  const field=LIST_FIELDS[method];
  const items=[],seen=new Set(),seenCursors=new Set();
  let cursor,pages=0,bytes=0;
  while(true){
    if(pages>=maxPages)throw Error('PAGE_LIMIT');
    const params=cursor===undefined?{}:{cursor};
    const rpc=await request(method,params);
    pages++;
    if(!rpc||typeof rpc!=='object'||Array.isArray(rpc))throw Error('INVALID_RPC');
    if(rpc.error){
      const code=Number.isInteger(rpc.error.code)?rpc.error.code:'INVALID';
      throw Error('MCP_RPC_'+code);
    }
    const result=rpc.result;
    if(!result||typeof result!=='object'||Array.isArray(result))throw Error('INVALID_RESULT');
    if(result.resultType!==undefined&&result.resultType!=='complete')
      throw Error('INVALID_RESULT_TYPE');
    if(!Array.isArray(result[field]))throw Error('INVALID_ITEMS');
    bytes+=Buffer.byteLength(JSON.stringify(result),'utf8');
    if(bytes>maxBytes)throw Error('BYTE_LIMIT');
    if(items.length+result[field].length>maxItems)throw Error('ITEM_LIMIT');
    for(const item of result[field]){
      if(!item||typeof item!=='object'||Array.isArray(item))throw Error('INVALID_ITEM');
      const identity=method==='resources/list'?item.uri:
        method==='resources/templates/list'?item.uriTemplate:item.name;
      if(typeof identity!=='string'||!identity)throw Error('INVALID_ITEM_ID');
      if(seen.has(identity))throw Error('DUPLICATE_ITEM');
      seen.add(identity);items.push(item);
    }
    const next=result.nextCursor;
    if(next===undefined||next===null)return {items,pages};
    if(typeof next!=='string'||next.length>4096)throw Error('INVALID_CURSOR');
    if(seenCursors.has(next))throw Error('REPEATED_CURSOR');
    seenCursors.add(next);
    cursor=next;
  }
}
