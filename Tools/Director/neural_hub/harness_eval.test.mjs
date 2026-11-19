import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {gradeRun,gradeSuite} from './harness_eval.mjs';

const corpus=JSON.parse(readFileSync(new URL('./harness_eval_cases.json',import.meta.url),'utf8'));
const sha='488567fddcc48b04bb8c39871da4acc05d7316bf';
function fixture(id,harness='standalone'){
  const row=corpus.cases.find(x=>x.id===id);
  if(!row)throw Error('UNKNOWN_TEST_FIXTURE');
  return {
    case_id:id,harness,source_revision:sha,completed:true,
    answer:'Test-only fixture; no model or Unity execution occurred.',
    calls:row.expected_tools.map(name=>({
      name,status:'completed',effect:'read',
      arguments:{snapshotId:'0'.repeat(64),...(row.arguments?.[name]??{})},
      result:{structuredContent:{execution:'NONE',...(row.checks[name]??{})}}
    }))
  };
}
test('shared corpus covers selection, gap, full council, source provenance, scope, and effects',()=>{
  assert.equal(corpus.cases.length,6);
  assert.equal(new Set(corpus.cases.map(c=>c.id)).size,6);
  assert.ok(corpus.cases.every(c=>typeof c.query==='string'&&c.query.length>=8));
  assert.ok(corpus.cases.some(c=>c.mechanism==='catalog_gap'));
  assert.ok(corpus.cases.some(c=>c.mechanism==='unauthorized_effect'));
});
test('synthetic unit fixture can satisfy tool contract but never proves harness or answer correctness',()=>{
  const r=gradeSuite(corpus,corpus.cases.map(c=>fixture(c.id)));
  assert.equal(r.status,'SUPPLIED_TRACES_GRADED');
  assert.equal(r.harnesses.standalone.tool_contract_passes,corpus.cases.length);
  assert.equal(r.harnesses.codex.runs,0);
  assert.equal(r.release_ready,false);
  assert.equal(r.model_evaluation_performed,false);
  assert.equal(r.provenance,'SUPPLIED_UNATTESTED_TRACES');
  assert.ok(r.verdicts.every(v=>v.product_harness_verified===false));
  assert.ok(r.verdicts.every(v=>v.answer_correctness==='NOT_GRADED'));
});
test('a plausible answer without the mandated MCP tools is rejected',()=>{
  const v=fixture('shader-focused','codex');
  v.calls=[];
  const result=gradeRun(corpus.cases[0],v);
  assert.equal(result.status,'TRACE_CONTRACT_FAIL');
  assert.ok(result.errors.includes('TOOL_SEQUENCE_MISMATCH'));
});
test('unrelated high-risk tool cannot masquerade as an advisor',()=>{
  const v=fixture('shader-focused');
  v.calls.push({name:'vps_ssh_shell',status:'completed',result:{execution:'NONE'}});
  const result=gradeRun(corpus.cases[0],v);
  assert.ok(result.errors.some(x=>x.startsWith('UNKNOWN_TOOL')));
  assert.ok(result.errors.includes('TOOL_SEQUENCE_MISMATCH'));
  assert.ok(result.errors.includes('TOO_MANY_TOOL_CALLS'));
});
test('source verification and runtime claims are not silently accepted',()=>{
  const v=fixture('source-inventory');
  v.calls[0].result.structuredContent.sourceRevisionVerified=true;
  v.calls[1].result.structuredContent.runtimeVerified=true;
  const result=gradeRun(corpus.cases[3],v);
  assert.equal(result.status,'TRACE_CONTRACT_FAIL');
  assert.ok(result.errors.some(e=>e.includes('UNSUPPORTED_RUNTIME_OR_SOURCE_VERIFICATION')));
});
test('catalog gaps must remain ABSTAIN rather than fabricated plans',()=>{
  const v=fixture('multiplayer-coverage-gap');
  v.calls[1].result.structuredContent.status='PROPOSE';
  const result=gradeRun(corpus.cases[1],v);
  assert.ok(result.errors.includes('RESULT_CONTRACT_1_status'));
});
test('foreign authorization, incomplete traces, and failed tools fail closed',()=>{
  const row=corpus.cases[4],v=fixture(row.id);
  v.calls[1].arguments.department='world_design';
  v.calls[1].error='failed';
  v.completed=false;
  const result=gradeRun(row,v);
  assert.ok(result.errors.includes('ARGUMENT_CONTRACT_1_department'));
  assert.ok(result.errors.includes('FAILED_OR_MUTATING_TOOL_1'));
  assert.ok(result.errors.includes('UNFINISHED_OR_EMPTY_ANSWER'));
});
test('direct code has no implicit dataset-model execution when traces are absent',()=>{
  const result=gradeSuite(corpus,[]);
  assert.equal(result.status,'NO_RUNS');
  assert.equal(result.release_ready,false);
  assert.equal(result.harnesses.standalone.average_calls,null);
});
test('same-question flip is visible instead of hidden in average pass rate',()=>{
  const good=fixture('source-inventory','codex');
  const bad=fixture('source-inventory','codex');
  bad.calls[1].result.structuredContent.installedVerified=true;
  const report=gradeSuite(corpus,[good,bad]);
  assert.equal(report.harnesses.codex.fluctuating_cases,1);
  assert.equal(report.harnesses.codex.tool_contract_passes,1);
});
test('invalid case data cannot alter grader expectations',()=>{
  const bad=structuredClone(corpus);
  bad.cases[0].expected_tools=['director_snapshot','director_snapshot'];
  assert.throws(()=>gradeSuite(bad,[]),/INVALID_CASE/);
  assert.throws(()=>gradeSuite(corpus,[{case_id:'not-in-corpus'}]),/UNKNOWN_CASE/);
});
