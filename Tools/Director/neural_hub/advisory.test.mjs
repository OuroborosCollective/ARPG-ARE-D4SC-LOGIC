import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import path from 'node:path';
import {readSnapshot, evaluateAdvisor, consultAll, advisoryCatalog, validateIssues, SOURCE_VERIFICATION} from './advisory.mjs';

const root = path.resolve(import.meta.dirname, '../../..');
const a = readFileSync(path.join(root, 'docs/ARCHITECTURE.md'));
const c = readFileSync(path.join(root, 'Tools/Director/neural_catalog.json'));
const sha = 'a'.repeat(40);
const sha256 = x => createHash('sha256').update(x).digest('hex');
const snapshot = () => readSnapshot({
  architectureBytes:a, catalogBytes:c, gitRevision:sha,
  expectedArchitectureSha256:sha256(a),
  expectedCatalogSha256:sha256(c)
});
const issue = (number, state='open') => ({
  number, state, title:'Review issue ' + number,
  url:'https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/' + number
});

test('all 28 advisors share one immutable architecture snapshot', () => {
  const s = snapshot();
  assert.equal(s.advisors.length, 28);
  assert.equal(new Set(s.advisors.map(x=>x.snapshotId)).size, 1);
  assert.equal(s.sourceRevisionVerified, false);
  assert.equal(s.runtimeVerified, false);
  assert.equal(advisoryCatalog(s).length, 28);
});
test('snapshot identity checks exact architecture bytes and rejects stale SHA', () => {
  const s = snapshot();
  assert.match(s.snapshotId, /^[0-9a-f]{64}$/);
  assert.throws(() => readSnapshot({architectureBytes:Buffer.concat([a,Buffer.from('x')]),catalogBytes:c,gitRevision:sha,expectedArchitectureSha256:s.architectureSha256,expectedCatalogSha256:s.catalogSha256}), /ARCHITECTURE_DIGEST_MISMATCH/);
  assert.throws(() => readSnapshot({architectureBytes:a,catalogBytes:c,gitRevision:'x',expectedArchitectureSha256:s.architectureSha256,expectedCatalogSha256:s.catalogSha256}), /INVALID_GIT_REVISION/);
});
test('unknown and malformed issue records fail closed', () => {
  for(const invalid of [
    [{number:60,state:'closed',title:'A',url:'https://evil.test/60'}],
    [{number:60,state:'open',title:'A',url:'https://github.com/OuroborosCollective/ARPG-ARE-D4SC-LOGIC/issues/60',command:'delete'}],
    [issue(60),issue(60)],
    Array.from({length:101}, (_,i)=>issue(i+1))
  ]) assert.throws(() => validateIssues(invalid));
});
test('specialist abstains when there is no matching issue evidence', () => {
  const s = snapshot();
  const response = evaluateAdvisor(s,'world_design',[]);
  assert.equal(response.status,'ABSTAIN');
  assert.equal(response.snapshotId,s.snapshotId);
  assert.equal(response.execution,'NONE');
});
test('specialist proposes only from supplied open issue', () => {
  const s=snapshot();
  const r=evaluateAdvisor(s,'world_design',[issue(66)]);
  assert.equal(r.status,'PROPOSE');
  assert.equal(r.proposal.track,'world');
  assert.deepEqual(r.proposal.sources,[issue(66).url]);
  assert.equal(r.sourceVerification,SOURCE_VERIFICATION);
  assert.equal(r.authorization,'NONE');
  assert.equal(r.proposal.skills.every(name=>s.advisors.find(x=>x.id==='world_design').skills.includes(name)),true);
});
test('closed issues cannot trigger a proposal', () => {
  assert.equal(evaluateAdvisor(snapshot(),'world_design',[issue(66,'closed')]).status,'ABSTAIN');
});
test('28 advisors and three different, explicitly evidenced paths', () => {
  const s=snapshot();
  const result=consultAll(s,[issue(76),issue(77),issue(66),issue(48)]);
  assert.equal(result.consulted,28);
  assert.equal(result.advisorResponses.length,28);
  assert.equal(result.status,'THREE_PATHS_READY_FOR_DIRECTOR_REVIEW');
  assert.deepEqual(result.paths.map(x=>x.track), ['gameplay','story','world']);
  assert.equal(result.execution,'NONE');
  assert.equal(result.directorReviewRequired,true);
  assert.equal(result.sourceRevisionVerified,false);
});
test('do not pad only two distinct tracks into three', () => {
  const r=consultAll(snapshot(),[issue(76),issue(77)]);
  assert.equal(r.status,'INSUFFICIENT_DISTINCT_PATHS');
  assert.deepEqual(r.paths,[]);
  assert.equal(r.consulted,28);
});
test('forged department and stale snapshot rejected',()=>{
  const s=snapshot();
  assert.throws(()=>evaluateAdvisor(s,'admin',[]),/UNKNOWN_DEPARTMENT/);
  assert.throws(()=>evaluateAdvisor(s,'world_design',[],{snapshotId:'0'.repeat(64)}),/STALE_SNAPSHOT/);
});
test('runtime and external effects remain denied',()=>{
  const s=snapshot();
  const r=consultAll(s,[issue(66)]);
  assert.equal(r.runtimeVerified,false);
  assert.equal(r.effect,'PROPOSAL_ONLY');
  assert.equal(r.execution,'NONE');
  assert.equal(s.networkUsed,false);
});
