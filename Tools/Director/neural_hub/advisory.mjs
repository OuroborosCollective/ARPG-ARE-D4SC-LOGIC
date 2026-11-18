import {createHash} from 'node:crypto';
export const SOURCE_VERIFICATION='SUPPLIED_ISSUE_NOT_INDEPENDENTLY_VERIFIED';
const SHA40=/^[a-f0-9]{40}$/;
const SHA64=/^[a-f0-9]{64}$/;
const ID=/^[a-z][a-z0-9_]{2,48}$/;
const SKILL=/^[a-z][a-z0-9_.-]{1,79}$/;
const MARKER='<!-- ashen.shared-architecture.v1 -->';
const ISSUE_BASE='https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/';
const TRACKS=['gameplay','story','world','quality','infrastructure'];
const RULES=Object.freeze({
  game_design:{track:'gameplay',issues:[75,76,60]},
  core_logic:{track:'gameplay',issues:[63,65,74]},
  combat:{track:'gameplay',issues:[74]},
  npc_ai:{track:'gameplay',issues:[76]},
  quest_design:{track:'story',issues:[77,68]},
  lore_dialogue:{track:'story',issues:[77]},
  world_design:{track:'world',issues:[66,69,70]},
  object_placement:{track:'world',issues:[62,69]},
  dungeons:{track:'world',issues:[67,71]},
  region_transitions:{track:'world',issues:[69,70,71,72,73]},
  shaders:{track:'quality',issues:[48]},
  vfx:{track:'quality',issues:[48]},
  lighting:{track:'quality',issues:[48]},
  materials:{track:'quality',issues:[48]},
  animation:{track:'quality',issues:[48]},
  audio:{track:'quality',issues:[48]},
  user_interface:{track:'quality',issues:[48,61]},
  asset_pipeline:{track:'infrastructure',issues:[62]},
  persistence:{track:'infrastructure',issues:[63]},
  performance_cache:{track:'infrastructure',issues:[64]},
  testing:{track:'infrastructure',issues:[64]},
  android_release:{track:'infrastructure',issues:[48]},
  tooling:{track:'infrastructure',issues:[64]},
  security:{track:'infrastructure',issues:[62]},
  multiplayer:{track:'infrastructure',issues:[]},
  economy:{track:'gameplay',issues:[75]},
  camera:{track:'quality',issues:[48]},
  physics:{track:'quality',issues:[48]}
});
const digest=bytes=>createHash('sha256').update(bytes).digest('hex');
function parseJson(bytes) {
  let parsed;
  try {parsed=JSON.parse(bytes.toString('utf8'));} catch {throw Error('INVALID_CATALOG_JSON');}
  return parsed;
}
const keys=(obj,allowed)=>Object.keys(obj).sort().join('|')===allowed.slice().sort().join('|');
function plain(value){return value!==null&&typeof value==='object'&&!Array.isArray(value);}
export function readSnapshot({architectureBytes,catalogBytes,gitRevision,expectedArchitectureSha256,expectedCatalogSha256}) {
  if(typeof gitRevision!=='string'||!SHA40.test(gitRevision))throw Error('INVALID_GIT_REVISION');
  if(!Buffer.isBuffer(architectureBytes)||!Buffer.isBuffer(catalogBytes)
    ||architectureBytes.length>96000||catalogBytes.length>131072||!architectureBytes.length||!catalogBytes.length)
    throw Error('INVALID_SNAPSHOT_BYTES');
  if(typeof expectedArchitectureSha256!=='string'||!SHA64.test(expectedArchitectureSha256)
    ||typeof expectedCatalogSha256!=='string'||!SHA64.test(expectedCatalogSha256))
    throw Error('MISSING_EXPECTED_DIGEST');
  const docHash=digest(architectureBytes),catalogHash=digest(catalogBytes);
  if(docHash!==expectedArchitectureSha256)throw Error('ARCHITECTURE_DIGEST_MISMATCH');
  if(catalogHash!==expectedCatalogSha256)throw Error('CATALOG_DIGEST_MISMATCH');
  const md=architectureBytes.toString('utf8');
  if(!md.startsWith('# ARE / Ashen Crypt')||md.split(MARKER).length!==2)throw Error('INVALID_ARCHITECTURE');
  const catalog=parseJson(catalogBytes);
  if(!plain(catalog)||!keys(catalog,['schema','unity','departments'])
    ||catalog.schema!=='ashen.director.neural-catalog.v1'||catalog.unity!=='6000.6.8f1'
    ||!Array.isArray(catalog.departments)||catalog.departments.length!==28)throw Error('INVALID_CATALOG');
  const ids=new Set();
  const advisors=catalog.departments.map(d=>{
    if(!plain(d)||!keys(d,['id','title','triggers','skills'])
      ||typeof d.id!=='string'||!ID.test(d.id)||ids.has(d.id)
      ||!Array.isArray(d.skills)||!d.skills.length||d.skills.length>10
      ||d.skills.some(s=>typeof s!=='string'||!SKILL.test(s))
      ||new Set(d.skills).size!==d.skills.length||!Object.hasOwn(RULES,d.id))
      throw Error('INVALID_ADVISOR');
    ids.add(d.id);
    return Object.freeze({id:d.id,title:d.title,skills:Object.freeze([...d.skills]),snapshotId:''});
  });
  if(ids.size!==Object.keys(RULES).length)throw Error('MISSING_DEPARTMENTS');
  const snapshotId=digest(Buffer.from(JSON.stringify([gitRevision,docHash,catalogHash,'6000.6.8f1'])));
  return Object.freeze({
    schema:'ashen.neural-hub.snapshot.v1',
    snapshotId,
    architectureSha256:docHash,
    catalogSha256:catalogHash,
    gitRevisionClaim:gitRevision,
    sourceRevisionVerified:false,
    runtimeVerified:false,
    networkUsed:false,
    architectureMarkdown:md,
    advisors:Object.freeze(advisors.map(x=>Object.freeze({...x,snapshotId})))
  });
}
export function advisoryCatalog(snapshot){
  if(!snapshot||!Array.isArray(snapshot.advisors)||snapshot.advisors.length!==28)throw Error('INVALID_SNAPSHOT');
  return snapshot.advisors.map(a=>({id:a.id,title:a.title,skills:[...a.skills],effect:'PROPOSAL_ONLY'}));
}
export function validateIssues(issues){
  if(!Array.isArray(issues)||issues.length>100)throw Error('INVALID_ISSUE_CATALOG');
  const seen=new Set();
  for(const x of issues){
    if(!plain(x)||!keys(x,['number','state','title','url'])
      ||!Number.isSafeInteger(x.number)||x.number<1
      ||typeof x.title!=='string'||x.title.length<1||x.title.length>300
      ||!['open','closed'].includes(x.state)
      ||x.url!==ISSUE_BASE+x.number||seen.has(x.number))throw Error('INVALID_ISSUE');
    seen.add(x.number);
  }
  return issues.slice();
}
export function evaluateAdvisor(snapshot,department,issues,{snapshotId=snapshot?.snapshotId}={}){
  if(!snapshot||!Array.isArray(snapshot.advisors)||snapshot.advisors.length!==28||!SHA64.test(snapshot.snapshotId||''))
    throw Error('INVALID_SNAPSHOT');
  if(snapshotId!==snapshot.snapshotId)throw Error('STALE_SNAPSHOT');
  const advisor=snapshot.advisors.find(x=>x.id===department);
  if(!advisor||!Object.hasOwn(RULES,department))throw Error('UNKNOWN_DEPARTMENT');
  const supplied=validateIssues(issues);
  const rule=RULES[department];
  const matched=rule.issues.map(n=>supplied.find(x=>x.number===n&&x.state==='open')).find(Boolean);
  const base={department,snapshotId:snapshot.snapshotId,execution:'NONE',authorization:'NONE',sourceVerification:SOURCE_VERIFICATION};
  if(!matched)return {...base,status:'ABSTAIN'};
  return {...base,status:'PROPOSE',proposal:{
    track:rule.track,
    title:'Architecture review: '+matched.title.slice(0,200),
    reason:'Review current issue #'+matched.number+' and its dependencies against the pinned architecture; no gameplay change is authorized.',
    skills:[advisor.skills[0]],
    sources:[matched.url]
  }};
}
export function consultAll(snapshot,issues){
  const supplied=validateIssues(issues);
  const responses=snapshot.advisors.map(advisor=>evaluateAdvisor(snapshot,advisor.id,supplied));
  const proposal=responses.filter(x=>x.status==='PROPOSE');
  const selected=new Map();
  for(const track of TRACKS){
    const row=proposal.filter(x=>x.proposal.track===track).sort((a,b)=>a.department.localeCompare(b.department,'en'))[0];
    if(row)selected.set(track,row);
  }
  const paths=TRACKS.filter(t=>selected.has(t)).slice(0,3).map(t=>({...selected.get(t).proposal,department:selected.get(t).department}));
  return {
    schema:'ashen.neural-hub.council-result.v1',
    snapshotId:snapshot.snapshotId,
    consulted:responses.length,
    advisorResponses:responses,
    status:paths.length===3?'THREE_PATHS_READY_FOR_DIRECTOR_REVIEW':'INSUFFICIENT_DISTINCT_PATHS',
    paths:paths.length===3?paths:[],
    candidateCount:proposal.length,
    sourceRevisionVerified:false,
    sourceVerification:SOURCE_VERIFICATION,
    directorReviewRequired:true,
    runtimeVerified:false,
    effect:'PROPOSAL_ONLY',
    execution:'NONE'
  };
}
