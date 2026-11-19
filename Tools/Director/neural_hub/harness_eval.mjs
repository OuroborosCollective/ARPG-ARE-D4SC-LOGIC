/** Offline tool-trace grader. Its verdict is deliberately weaker than product correctness. */
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import path from 'node:path';

export const CORPUS_SCHEMA='ashen.director.harness-corpus.v1';
export const VERDICT_SCHEMA='ashen.director.harness-verdict.v1';
const SHA40=/^[0-9a-f]{40}$/;
const ID=/^[a-z][a-z0-9-]{2,79}$/;
const TOOL=/^(?:director_(?:snapshot|sources|source_discover|consult)|advice_[a-z][a-z0-9_]{2,48})$/;

function plain(value){return value!==null&&typeof value==='object'&&!Array.isArray(value);}
function checkCorpus(corpus){
  if(!plain(corpus)||corpus.schema!==CORPUS_SCHEMA||!Array.isArray(corpus.cases)
      ||corpus.cases.length<1||corpus.cases.length>64)throw Error('INVALID_CORPUS');
  const ids=new Set();
  for(const row of corpus.cases){
    if(!plain(row)||typeof row.id!=='string'||!ID.test(row.id)||ids.has(row.id)
        ||typeof row.query!=='string'||row.query.length<8||row.query.length>1200
        ||typeof row.mechanism!=='string'||!Array.isArray(row.expected_tools)
        ||row.expected_tools.length>8||new Set(row.expected_tools).size!==row.expected_tools.length
        ||row.expected_tools.some(x=>typeof x!=='string'||!TOOL.test(x))
        ||!Number.isInteger(row.max_calls)||row.max_calls<row.expected_tools.length||row.max_calls>16
        ||!plain(row.checks))throw Error('INVALID_CASE');
    if(Object.keys(row.checks).some(name=>!row.expected_tools.includes(name)
        ||!plain(row.checks[name])))throw Error('INVALID_RESULT_CHECKS');
    if(row.arguments!==undefined&&(!plain(row.arguments)||Object.keys(row.arguments).some(name=>
        !row.expected_tools.includes(name)||!plain(row.arguments[name]))))
      throw Error('INVALID_ARGUMENT_CHECKS');
    ids.add(row.id);
  }
  return corpus;
}

function structured(call){
  const outer=call?.result;
  if(!plain(outer))return null;
  const inner=outer.structuredContent??outer.structured_content??outer;
  return plain(inner)?inner:null;
}

/** Compare tool effects only, never represent this result as answer correctness or runtime evidence. */
export function gradeRun(testCase,run){
  const errors=[];
  if(!plain(testCase)||!plain(run))throw Error('INVALID_GRADE_INPUT');
  if(run.case_id!==testCase.id)errors.push('CASE_ID_MISMATCH');
  if(!['standalone','codex'].includes(run.harness))errors.push('UNKNOWN_HARNESS');
  if(typeof run.source_revision!=='string'||!SHA40.test(run.source_revision))
    errors.push('MISSING_SOURCE_REVISION_CLAIM');
  if(run.completed!==true||typeof run.answer!=='string'||!run.answer.trim())
    errors.push('UNFINISHED_OR_EMPTY_ANSWER');
  const calls=Array.isArray(run.calls)?run.calls:null;
  if(!calls)errors.push('MISSING_TRACE_CALLS');
  const seen=calls??[];
  if(seen.length>testCase.max_calls)errors.push('TOO_MANY_TOOL_CALLS');
  if(JSON.stringify(seen.map(x=>x?.name))!==JSON.stringify(testCase.expected_tools))
    errors.push('TOOL_SEQUENCE_MISMATCH');
  for(const [index,call] of seen.entries()){
    if(!plain(call)||typeof call.name!=='string'||!TOOL.test(call.name)){
      errors.push('UNKNOWN_TOOL_'+index);continue;
    }
    if(call.status!=='completed'||call.error||call.effect==='external-write'||call.effect==='workspace-write')
      errors.push('FAILED_OR_MUTATING_TOOL_'+index);
    const result=structured(call);
    if(!result){errors.push('MISSING_STRUCTURED_RESULT_'+index);continue;}
    if(result.execution!=='NONE'&&result.execution!==undefined)
      errors.push('UNEXPECTED_EXECUTION_'+index);
    if(result.runtimeVerified===true||result.sourceRevisionVerified===true)
      errors.push('UNSUPPORTED_RUNTIME_OR_SOURCE_VERIFICATION_'+index);
    const checked=testCase.checks?.[call.name]??{};
    for(const [field,expected] of Object.entries(checked)){
      if(!Object.hasOwn(result,field)||result[field]!==expected)
        errors.push('RESULT_CONTRACT_'+index+'_'+field);
    }
    const expectedArgs=testCase.arguments?.[call.name]??{};
    if(Object.keys(expectedArgs).length&&!plain(call.arguments)){
      errors.push('MISSING_TOOL_ARGUMENTS_'+index);continue;
    }
    for(const [field,expected] of Object.entries(expectedArgs))
      if(call.arguments[field]!==expected)errors.push('ARGUMENT_CONTRACT_'+index+'_'+field);
  }
  return {
    schema:VERDICT_SCHEMA,case_id:testCase.id,harness:run.harness??'UNKNOWN',
    mechanism:testCase.mechanism,source_revision_claim:run.source_revision??null,
    status:errors.length?'TRACE_CONTRACT_FAIL':'TRACE_CONTRACT_PASS',
    tool_calls:seen.length,errors,
    evidence_scope:'SUPPLIED_TRACE_TOOL_CONTRACT_ONLY',answer_correctness:'NOT_GRADED',
    source_revision_verified:false,unity_runtime_verified:false,product_harness_verified:false
  };
}

/** Regrades supplied captured traces; does not open an MCP connection or call a model. */
export function gradeSuite(corpus,runs){
  checkCorpus(corpus);
  if(!Array.isArray(runs)||runs.length>2000)throw Error('INVALID_RUNS');
  const cases=new Map(corpus.cases.map(x=>[x.id,x]));
  const verdicts=runs.map(run=>{
    if(!plain(run)||!cases.has(run.case_id))throw Error('UNKNOWN_CASE');
    return gradeRun(cases.get(run.case_id),run);
  });
  const harnesses={};
  for(const harness of ['standalone','codex']){
    const selected=verdicts.filter(v=>v.harness===harness);
    const groups=new Map();
    for(const v of selected){
      if(!groups.has(v.case_id))groups.set(v.case_id,[]);
      groups.get(v.case_id).push(v.status==='TRACE_CONTRACT_PASS');
    }
    harnesses[harness]={
      runs:selected.length,cases_covered:groups.size,
      tool_contract_passes:selected.filter(v=>v.status==='TRACE_CONTRACT_PASS').length,
      average_calls:selected.length?selected.reduce((a,v)=>a+v.tool_calls,0)/selected.length:null,
      fluctuating_cases:[...groups.values()].filter(a=>a.includes(true)&&a.includes(false)).length,
      all_repeats_pass_cases:[...groups.values()].filter(a=>a.every(Boolean)).length
    };
  }
  return {
    schema:'ashen.director.harness-suite.v1',
    status:verdicts.length?'SUPPLIED_TRACES_GRADED':'NO_RUNS',
    cases_defined:corpus.cases.length,verdicts,harnesses,
    release_ready:false,
    model_evaluation_performed:false,
    provenance:'SUPPLIED_UNATTESTED_TRACES',
    answer_correctness:'NOT_GRADED',
    note:'Synthetic unit fixtures and supplied traces do not prove a Codex run, a deployed plugin, or Unity Runtime.'
  };
}

const ownFile=process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url);
if(ownFile){
  try{
    if(process.argv.length!==3)throw Error('USAGE: node harness_eval.mjs /path/to/captured-traces.json');
    const base=path.dirname(fileURLToPath(import.meta.url));
    const corpusBytes=readFileSync(path.join(base,'harness_eval_cases.json'));
    if(corpusBytes.length>131072)throw Error('CORPUS_TOO_LARGE');
    const inputBytes=readFileSync(process.argv[2]);
    if(inputBytes.length>2_000_000)throw Error('TRACES_TOO_LARGE');
    const parsed=JSON.parse(inputBytes.toString('utf8'));
    const report=gradeSuite(JSON.parse(corpusBytes.toString('utf8')),parsed.runs);
    console.log(JSON.stringify(report,null,2));
    if(report.verdicts.some(v=>v.status!=='TRACE_CONTRACT_PASS'))process.exitCode=1;
    if(report.status==='NO_RUNS')process.exitCode=2;
  }catch(e){
    console.error('HARNESS_GRADER_BLOCKED:',e.message);
    process.exitCode=2;
  }
}
