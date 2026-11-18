import {createHash} from 'node:crypto';

const ID=/^[a-z][a-z0-9-]{2,48}$/;
const SHA40=/^[a-f0-9]{40}$/;
const PATH=/^(?:[A-Za-z0-9_.~-]+\/)*[A-Za-z0-9_.~-]+\/SKILL\.md$/;
const DEPT=/^[a-z][a-z0-9_]{2,48}$/;
const LICENSES=Object.freeze({
  besty:['MIT','f8497e55581c5b7154d87e7b664bb9b95745d21b','Besty0728/Unity-Skills'],
  anklebreaker:['ANKLEBREAKER_OPEN_1_0','e7b3732ee2d054fc9d86874d8e8a84adc0e8c132','AnkleBreaker-Studio/unity-mcp-server'],
  'unity-open-mcp':['MIT','3c7fe049043edfff9364e3caa968f18c12ce18f0','AlexeyPerov/Unity-Open-MCP'],
  'unity-biome-mcp':['MIT','d880896e5c9cecb1419894e19a13a821a1eba30b','german-krasnikov/unity-biome-mcp'],
  ivanmurzak:['APACHE-2.0','106983b4c6f49a91c4dd7b11dc19a4927c42ccda','IvanMurzak/Unity-MCP'],
  'unified-unity-mcp':['MIT','4e3f02329c48d81609973509f843111cab31b6b6','AndreySkyFoxSidorov/UnifiedUnityMCP'],
  coplay:['MIT','e7f878d1da6b4d9488058157ce6b48cb584460b1','CoplayDev/unity-mcp'],
  'unity-official':['UNITY_COMPANION','869c724c8cab8764b548fd5bd8cb8ea1fc849100','Unity-Technologies/skills'],
  coder:['MIT','bba7cd45ec0eec738f39e27ada6768b300057e10','CoderGamester/mcp-unity'],
  ucp:['MIT','8aa26455d23acf904be3ed9dfb3a3efe3e49245a','mflRevan/unity-control-protocol'],
  uloop:['MIT','ebfa1e9a074c742a2684fe040e4d9abd9dab589b','hatayama/unity-cli-loop'],
  'unity-ai-cli-bridge':['MIT','3c46e2a2d8e954f0d00a7871b63d3ded0363c4da','hatayama/unity-ai-cli-bridge'],
  accelbyte:['APACHE-2.0','261eeb9e9f8b2b4b0d119366dda99c6fd7d35c64','AccelByte/accelbyte-unity-mcp']
});
const INSPECTED_SOURCE_SHA=Object.freeze({
  besty:'c07ee7f74660def88533278f09ec611bb36e0cbe',
  anklebreaker:'a669788cd367de7f0b7bfd657a66807a9156628a',
  'unity-open-mcp':'7695c5e0af44423c2a7800b105a6b3fa7a05075e',
  'unity-biome-mcp':'c88b0bbc45891d4147f8f24f64c02be73ef4279e',
  ivanmurzak:'393c9e374b4485d1db37117440e41af2262d4b0c',
  'unified-unity-mcp':'a934d2cba994c581089a6131b5dc1439d30e5d2f',
  coplay:'19b66ecef4ac3a9be22a7a62ea21accb975c39e7',
  'unity-official':'5841a34846f3e2b908c37aca1fca2eb15118a440',
  coder:'d572dc88f1b982a0fdfc09cd14cfdfd760b658e8',
  ucp:'7cfc26324c13f0d1c8d91cae80e915a6a00adc1c',
  uloop:'fa4d15227f5f1146c7ca142a3a72c5fb332601fe',
  'unity-ai-cli-bridge':'a8e272d32b3df5d579d1cb16eef8961702570e53',
  accelbyte:'80b99d260cd5b6cdb414f7a32fec3cecbff8290e'
});
const EXPECTED_DEPARTMENTS=28;
const EXACT_FIELDS=['id','repository','sourceUrl','sourceSha','license','licenseFile',
  'licenseBlobSha','status','allowExecution','installedVerified','unityBuildVerified',
  'integrationStrategy','priority','departments','capabilityAreas','reportedCount',
  'sourceTreeFileCount','discoveredSkillFiles','upstreamSkillFiles'];
const isObject=x=>x!==null&&typeof x==='object'&&!Array.isArray(x);
const sameKeys=(x,keys)=>isObject(x)&&Object.keys(x).sort().join('|')===keys.slice().sort().join('|');
const hash=raw=>createHash('sha256').update(raw).digest('hex');
function validPaths(xs, limit){
  return Array.isArray(xs)&&xs.length<=limit&&
    xs.every(x=>typeof x==='string'&&x.length<256&&PATH.test(x)&&!x.includes('..'))&&
    new Set(xs).size===xs.length;
}
function validStrings(xs,limit){
  return Array.isArray(xs)&&xs.length>0&&xs.length<=limit&&
    xs.every(x=>typeof x==='string'&&x.length>2&&x.length<160)&&
    new Set(xs).size===xs.length;
}
export function loadSourceRegistry(raw,catalog){
  if(typeof raw!=='string'||raw.length>300000||raw.length===0)throw Error('INVALID_SOURCE_REGISTRY_BYTES');
  let obj;
  try{obj=JSON.parse(raw);}catch{throw Error('INVALID_SOURCE_REGISTRY_JSON');}
  if(!sameKeys(obj,['schema','unityVersion','purpose','sources'])||
     obj.schema!=='ashen.director.upstream-source-registry.v1'||
     obj.unityVersion!=='6000.6.8f1'||
     typeof obj.purpose!=='string'||
     !Array.isArray(obj.sources)||obj.sources.length!==13)throw Error('INVALID_SOURCE_COUNT');
  if(!isObject(catalog)||catalog.schema!=='ashen.director.neural-catalog.v1'||
     catalog.unity!=='6000.6.8f1'||
     !Array.isArray(catalog.departments)||catalog.departments.length!==EXPECTED_DEPARTMENTS)
    throw Error('INVALID_DEPARTMENT_CATALOG');
  const allowedDepartments=new Set(catalog.departments.map(x=>x.id));
  if(allowedDepartments.size!==EXPECTED_DEPARTMENTS||[...allowedDepartments].some(d=>!DEPT.test(d)))
    throw Error('INVALID_DEPARTMENT_CATALOG');
  const seenIds=new Set(),seenRepos=new Set();
  for(const s of obj.sources){
    if(!sameKeys(s,EXACT_FIELDS))throw Error('INVALID_SOURCE_FIELDS');
    if(typeof s.id!=='string'||!ID.test(s.id)||!Object.hasOwn(LICENSES,s.id)||
       seenIds.has(s.id))throw Error('UNKNOWN_OR_DUPLICATE_SOURCE');
    seenIds.add(s.id);
    const [license,blob,repo]=LICENSES[s.id];
    if(s.license!==license||s.licenseBlobSha!==blob)throw Error('LICENSE_PROVENANCE_MISMATCH');
    const licenseFile=(s.id==='unity-official'||['coder','ucp','uloop'].includes(s.id))?'LICENSE.md':'LICENSE';
    if(s.repository!==repo||s.sourceUrl!=='https://github.com/'+repo||
       s.licenseFile!==licenseFile||seenRepos.has(repo)||
       typeof s.sourceSha!=='string'||!SHA40.test(s.sourceSha)||
       typeof s.licenseBlobSha!=='string'||!SHA40.test(s.licenseBlobSha))
      throw Error('SOURCE_IDENTITY_MISMATCH');
    seenRepos.add(repo);
    if(s.sourceSha!==INSPECTED_SOURCE_SHA[s.id])throw Error('SOURCE_COMMIT_DRIFT');
    if(s.status!=='SOURCE_INSPECTED_ONLY'||s.allowExecution!==false||
       s.installedVerified!==false||s.unityBuildVerified!==false)
      throw Error('UNAUTHORIZED_SOURCE_PROMOTION');
    if(!['INSPECT_SANDBOX_FIRST','LICENSE_REVIEW_FIRST','REUSE_LOCAL_CATALOG'].includes(s.integrationStrategy)||
       (s.id==='anklebreaker'&&s.integrationStrategy!=='LICENSE_REVIEW_FIRST')||
       (s.id==='unity-official'&&s.integrationStrategy!=='REUSE_LOCAL_CATALOG')||
       !['S','A','B'].includes(s.priority))
      throw Error('INVALID_INTEGRATION_STRATEGY');
    if(!Array.isArray(s.departments)||s.departments.length<1||s.departments.length>28||
       new Set(s.departments).size!==s.departments.length||
       s.departments.some(x=>!allowedDepartments.has(x)))
      throw Error('UNKNOWN_DEPARTMENT');
    if(!validStrings(s.capabilityAreas,24))throw Error('INVALID_CAPABILITY_NAMES');
    if(!sameKeys(s.reportedCount,['count','unit','independentlyVerified'])||
       !Number.isInteger(s.reportedCount.count)||s.reportedCount.count<0||
       s.reportedCount.count>5000||typeof s.reportedCount.unit!=='string'||
       !['REST_SKILLS','NAMED_OPERATIONS','MCP_TOOLS','CLI_COMMANDS','SKILL_MD_FILES'].includes(s.reportedCount.unit)||
       s.reportedCount.independentlyVerified!==false)
      throw Error('INVALID_ADVERTISED_COUNT');
    if(!Number.isSafeInteger(s.sourceTreeFileCount)||s.sourceTreeFileCount<0||
       !validPaths(s.discoveredSkillFiles,150)||!validPaths(s.upstreamSkillFiles,150)||
       s.upstreamSkillFiles.length!==s.discoveredSkillFiles.length||
       s.upstreamSkillFiles.some(x=>!s.discoveredSkillFiles.includes(x)))
      throw Error('INVALID_SOURCE_PATH');
  }
  if(seenIds.size!==Object.keys(LICENSES).length)throw Error('MISSING_SOURCES');
  return Object.freeze({
    schema:obj.schema,unityVersion:obj.unityVersion,registrySha256:hash(raw),
    catalogDepartments:Object.freeze([...allowedDepartments].sort()),
    sources:Object.freeze(obj.sources.map(s=>Object.freeze({
      ...s,departments:Object.freeze([...s.departments]),
      capabilityAreas:Object.freeze([...s.capabilityAreas]),
      discoveredSkillFiles:Object.freeze([...s.discoveredSkillFiles]),
      upstreamSkillFiles:Object.freeze([...s.upstreamSkillFiles])
    })))
  });
}
export function discoverSources(registry,department,{limit=8}={}){
  if(!isObject(registry)||registry.schema!=='ashen.director.upstream-source-registry.v1'||
     !Array.isArray(registry.sources)||registry.sources.length!==13||
     !Array.isArray(registry.catalogDepartments))throw Error('INVALID_REGISTRY');
  // A real catalog department with no covering source returns an empty candidate list, not UNKNOWN_DEPARTMENT.
  const areas=new Set(registry.catalogDepartments);
  if(typeof department!=='string'||!areas.has(department))throw Error('UNKNOWN_DEPARTMENT');
  if(!Number.isInteger(limit)||limit<1||limit>20)throw Error('INVALID_LIMIT');
  const rank={S:0,A:1,B:2};
  const candidates=registry.sources.filter(x=>x.departments.includes(department))
    .sort((a,b)=>rank[a.priority]-rank[b.priority]||a.id.localeCompare(b.id,'en'))
    .slice(0,limit)
    .map(x=>({
      id:x.id,repository:x.repository,sourceSha:x.sourceSha,
      sourceUrl:x.sourceUrl,license:x.license,licenseBlobSha:x.licenseBlobSha,
      priority:x.priority,departments:[...x.departments],
      integrationStrategy:x.integrationStrategy,
      capabilityAreas:[...x.capabilityAreas],
      reportedCount:{...x.reportedCount},
      discoveredSkillFileCount:x.discoveredSkillFiles.length,
      status:'SOURCE_INSPECTED_ONLY',
      useRequiresIndependentLicenseAndUnityReview:true
    }));
  return {
    schema:'ashen.director.source-discovery.v1',
    unityVersion:'6000.6.8f1',
    registrySha256:registry.registrySha256,
    department,
    candidates,
    sourceInspectionOnly:true,
    installedVerified:false,
    runtimeVerified:false,
    installationPerformed:false,
    authorization:'NONE',
    execution:'NONE'
  };
}
