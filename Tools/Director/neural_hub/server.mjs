import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {MCPServer} from 'mcp-use';
import {z} from 'zod';
import {readSnapshot,evaluateAdvisor,consultAll,advisoryCatalog} from './advisory.mjs';
import {loadSourceRegistry,discoverSources} from './sources.mjs';

const base=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(path.join(base,p));
const sha=p=>createHash('sha256').update(p).digest('hex');
const doc=read('docs/ARCHITECTURE.md');
const catalog=read('Tools/Director/neural_catalog.json');
const upstreamSourceBytes=read('Tools/Director/neural_hub/upstream_sources.json');
if(!process.env.ASHEN_REPOSITORY_SHA || !process.env.ASHEN_ARCHITECTURE_SHA256 ||
   !process.env.ASHEN_CATALOG_SHA256 || !process.env.ASHEN_SOURCES_SHA256)
  throw Error('MISSING_PINNED_REPOSITORY_PROVENANCE');
if(process.env.NEURAL_HUB_LOCAL_ONLY!=='1')throw Error('LOCAL_ONLY_GUARD_REQUIRED');
const snapshot=readSnapshot({
  architectureBytes:doc,catalogBytes:catalog,
  gitRevision:process.env.ASHEN_REPOSITORY_SHA,
  expectedArchitectureSha256:process.env.ASHEN_ARCHITECTURE_SHA256,
  expectedCatalogSha256:process.env.ASHEN_CATALOG_SHA256
});
if(sha(upstreamSourceBytes)!==process.env.ASHEN_SOURCES_SHA256)
  throw Error('SOURCE_REGISTRY_DIGEST_MISMATCH');
const sources=loadSourceRegistry(upstreamSourceBytes.toString('utf8'),JSON.parse(catalog.toString('utf8')));
const issue=z.object({
  number:z.number().int().positive(),state:z.enum(['open','closed']),
  title:z.string().min(1).max(300),url:z.string().url()
}).strict();
const inputSchema=z.object({
  snapshotId:z.string().regex(/^[0-9a-f]{64}$/),
  issues:z.array(issue).max(100).default([])
}).strict();
const server=new MCPServer({
  name:'ashen-neural-hub',version:'0.1.0',
  description:'Unity 6000.6.8f1: read-only, provenance-bound 28-advisor council; no runtime authority'
});
const result=value=>({
  content:[{type:'text',text:JSON.stringify(value)}],
  structuredContent:value
});
for(const advisor of advisoryCatalog(snapshot)){
  server.tool({
    name:'advice_'+advisor.id,
    description:'Read-only domain advisory: '+advisor.title+
      '. Issue records are supplied by the authorized Director and not independently verified.',
    inputSchema,
    annotations:{readOnlyHint:true,destructiveHint:false,idempotentHint:true,openWorldHint:false}
  },async({snapshotId,issues})=>result(
    evaluateAdvisor(snapshot,advisor.id,issues,{snapshotId})
  ));
  server.prompt({
    name:'brief_'+advisor.id,
    schema:z.object({snapshotId:z.string().regex(/^[0-9a-f]{64}$/)}).strict(),
    description:'Specialist design reasoning guidance for '+advisor.title+
      ' without invoking external tools or mutating Unity.'
  },async({snapshotId})=>{
    if(snapshotId!==snapshot.snapshotId)throw Error('STALE_SNAPSHOT');
    return {messages:[{
      role:'user',
      content:{
        type:'text',
        text:[
          'ASHEN CRYPT UNITY 6000.6.8f1: advisory-only.',
          'Specialist department: '+advisor.id+' ('+advisor.title+').',
          'Allowed skill references: '+advisor.skills.join(', '),
          'Architecture SHA-256: '+snapshot.architectureSha256,
          'Snapshot ID: '+snapshot.snapshotId,
          'Review the shared architecture below and produce only specialized proposals.',
          'Check external issue status independently. No imagined evidence, installs, writes or tool execution.',
          'If evidence is inadequate, return ABSTAIN. Never alter Core authority.',
          snapshot.architectureMarkdown
        ].join('\n')
      }
    }]};
  });
}
server.tool({
  name:'director_consult',
  description:'Collate all 28 read-only domain reviews into up to three distinct paths. Never execute.',
  inputSchema,
  annotations:{readOnlyHint:true,destructiveHint:false,idempotentHint:true,openWorldHint:false}
},async({snapshotId,issues})=>{
  if(snapshotId!==snapshot.snapshotId)throw Error('STALE_SNAPSHOT');
  return result(consultAll(snapshot,issues));
});
server.tool({
  name:'director_snapshot',
  description:'Read the exact shared, pinned architecture identity and 28 advisor names; metadata only.',
  inputSchema:z.object({}).strict(),
  annotations:{readOnlyHint:true,destructiveHint:false,idempotentHint:true,openWorldHint:false}
},async()=>result({
  schema:snapshot.schema,snapshotId:snapshot.snapshotId,
  architectureSha256:snapshot.architectureSha256,catalogSha256:snapshot.catalogSha256,
  gitRevisionClaim:snapshot.gitRevisionClaim,sourceRevisionVerified:false,
  runtimeVerified:false, execution:'NONE',
  departments:advisoryCatalog(snapshot)
}));
server.tool({
  name:'director_sources',
  description:'Read-only inventory of 13 pinned upstream Unity tool repositories; not installed or executable.',
  inputSchema:z.object({snapshotId:z.string().regex(/^[0-9a-f]{64}$/)}).strict(),
  annotations:{readOnlyHint:true,destructiveHint:false,idempotentHint:true,openWorldHint:false}
},async({snapshotId})=>{
  if(snapshotId!==snapshot.snapshotId)throw Error('STALE_SNAPSHOT');
  return result({
    schema:'ashen.director.source-index.v1',
    snapshotId:snapshot.snapshotId,
    registrySha256:sources.registrySha256,
    sourceCount:sources.sources.length,
    sourceIds:sources.sources.map(x=>x.id),
    installedVerified:false,runtimeVerified:false,
    execution:'NONE',effect:'PROPOSAL_ONLY',
    provenance:'PINNED_UPSTREAM_README_LICENSE_AND_TREE_INSPECTION_ONLY'
  });
});
server.tool({
  name:'director_source_discover',
  description:'Filter inspected, non-executable Unity MCP/Skill source candidates by existing specialist department.',
  inputSchema:z.object({
    snapshotId:z.string().regex(/^[0-9a-f]{64}$/),
    department:z.string().min(3).max(50),
    limit:z.number().int().min(1).max(20).default(8)
  }).strict(),
  annotations:{readOnlyHint:true,destructiveHint:false,idempotentHint:true,openWorldHint:false}
},async({snapshotId,department,limit})=>{
  if(snapshotId!==snapshot.snapshotId)throw Error('STALE_SNAPSHOT');
  return result(discoverSources(sources,department,{limit}));
});
export {server,snapshot};
export default server;
