import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import path from 'node:path';
import {loadSourceRegistry,discoverSources} from './sources.mjs';

const root=path.resolve(import.meta.dirname,'../../..');
const raw=readFileSync(path.join(root,'Tools/Director/neural_hub/upstream_sources.json'),'utf8');
const catalog=JSON.parse(readFileSync(path.join(root,'Tools/Director/neural_catalog.json'),'utf8'));

test('every supplied Unity source is recorded once with exact upstream provenance',()=>{
  const r=loadSourceRegistry(raw,catalog);
  assert.equal(r.sources.length,13);
  assert.equal(new Set(r.sources.map(x=>x.repository)).size,13);
  assert.equal(r.sources.every(x=>/^[a-f0-9]{40}$/.test(x.sourceSha)),true);
  assert.equal(r.sources.every(x=>/^[a-f0-9]{40}$/.test(x.licenseBlobSha)),true);
  assert.equal(r.sources.every(x=>x.status==='SOURCE_INSPECTED_ONLY'),true);
  assert.equal(r.sources.every(x=>x.allowExecution===false),true);
});
test('all upstream SKILL.md paths have concrete pinned repository owners',()=>{
  const r=loadSourceRegistry(raw,catalog);
  const official=r.sources.find(x=>x.id==='unity-official');
  assert.equal(official.discoveredSkillFiles.length,37);
  const besty=r.sources.find(x=>x.id==='besty');
  assert.equal(besty.reportedCount.unit,'REST_SKILLS');
  assert.equal(besty.reportedCount.count,873);
  assert.equal(besty.discoveredSkillFiles.length,2);
  assert.equal(besty.reportedCount.independentlyVerified,false);
});
test('restrictive licenses cannot be promoted to permissive or executable',()=>{
  const r=loadSourceRegistry(raw,catalog);
  const ankle=r.sources.find(x=>x.id==='anklebreaker');
  assert.equal(ankle.license,'ANKLEBREAKER_OPEN_1_0');
  assert.equal(ankle.integrationStrategy,'LICENSE_REVIEW_FIRST');
  const x=JSON.parse(raw);x.sources.find(y=>y.id==='anklebreaker').license='MIT';
  assert.throws(()=>loadSourceRegistry(JSON.stringify(x),catalog),/LICENSE_PROVENANCE_MISMATCH/);
});
test('Unity Companion License is visible and not redistributed into our plugin',()=>{
  const r=loadSourceRegistry(raw,catalog);
  const x=r.sources.find(y=>y.id==='unity-official');
  assert.equal(x.license,'UNITY_COMPANION');
  assert.equal(x.integrationStrategy,'REUSE_LOCAL_CATALOG');
  assert.equal(x.allowExecution,false);
});
test('discovery filters by existing departments and remains metadata-only',()=>{
  const r=loadSourceRegistry(raw,catalog);
  const a=discoverSources(r,'testing',{limit:8});
  assert.ok(a.candidates.length>0);
  assert.ok(a.candidates.length<=8);
  assert.equal(a.execution,'NONE');
  assert.equal(a.authorization,'NONE');
  assert.equal(a.installedVerified,false);
  assert.ok(a.candidates.every(c=>c.departments.includes('testing')));
  assert.ok(a.candidates.every(c=>c.status==='SOURCE_INSPECTED_ONLY'));
  assert.ok(a.candidates.every(c=>c.useRequiresIndependentLicenseAndUnityReview===true));
});
test('unknown department, unknown catalog entry, duplicate source fail closed',()=>{
  const r=loadSourceRegistry(raw,catalog);
  assert.throws(()=>discoverSources(r,'nonexistent'),/UNKNOWN_DEPARTMENT/);
  const bad=JSON.parse(raw);bad.sources[0].departments.push('admin');
  assert.throws(()=>loadSourceRegistry(JSON.stringify(bad),catalog),/UNKNOWN_DEPARTMENT/);
  const dupe=JSON.parse(raw);dupe.sources.push({...dupe.sources[0]});
  assert.throws(()=>loadSourceRegistry(JSON.stringify(dupe),catalog),/INVALID_SOURCE_COUNT/);
});
test('sources cannot contain commands, credentials, hidden auto-installs or network endpoints',()=>{
  const cases=[
    x=>{x.sources[0].command='npm install --yes';},
    x=>{x.sources[0].allowExecution=true;},
    x=>{x.sources[0].sourceUrl='http://localhost:8080/admin';},
    x=>{x.sources[0].sourceSha='main';},
    x=>{x.sources[0].upstreamSkillFiles=['../../etc/passwd'];}
  ];
  for(const makeBad of cases){const x=JSON.parse(raw);makeBad(x);assert.throws(()=>loadSourceRegistry(JSON.stringify(x),catalog));}
});
test('source selection is deterministic and never reports Unity runtime success',()=>{
  const r=loadSourceRegistry(raw,catalog);
  assert.deepEqual(discoverSources(r,'world_design',{limit:4}),discoverSources(r,'world_design',{limit:4}));
  const x=discoverSources(r,'world_design',{limit:4});
  assert.equal(x.unityVersion,'6000.6.8f1');
  assert.equal(x.runtimeVerified,false);
  assert.equal(x.installationPerformed,false);
  assert.equal(x.sourceInspectionOnly,true);
  assert.match(x.registrySha256,/^[a-f0-9]{64}$/);
});
