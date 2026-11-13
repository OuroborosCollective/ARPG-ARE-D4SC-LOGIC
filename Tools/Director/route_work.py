#!/usr/bin/env python3
"""Deterministic offline task-to-capability planner. Never claims connector availability."""
import argparse,json
from pathlib import Path
from official_unity import route as official_route
from game_dev import route as gamedev_route
PHASES=(
 ('discovery',('project','status','continue','repo','revision','memory'),('GitHub','Sovott v2','Unity Game Director')),
 ('research',('research','study','docs','api','paper','skill'),('official Unity docs','Exa or web','omgskills')),
 ('design',('quest','story','npc','world','economy','balance','level'),('Unity Game Director','Wolfram optional','project design docs')),
 ('implement',('implement','fix','code','integration','feature','bug'),('GitHub','Sovott v2','EngineeringSuite TDD','Unity MCP if connected')),
 ('test',('test','regression','replay','determinism','core','persist'),('EngineeringSuite TDD','Unity Test Runner')),
 ('android',('android','apk','aab','fps','perf','render','device','build'),('Unity CLI','adb','Perfetto','Simpleperf')),
 ('review',('review','merge','pr','ci','release'),('GitHub','Evidence Readback')),
 ('evidence',('evidence','memory','log','receipt','readback'),('Evidence Readback','local ledger')),
)
ORDER=['discovery','research','design','implement','test','android','review','evidence']
def plan(task,available=(),project=None):
 text=task.casefold(); chosen={'discovery','evidence'}
 for phase,keys,_ in PHASES:
  if any(k in text for k in keys): chosen.add(phase)
 if 'implement' in chosen:chosen.add('test');chosen.add('review')
 if 'android' in chosen:chosen.add('test')
 inventory=set(available)
 out=[]
 for phase,_,tools in PHASES:
  if phase not in chosen:continue
  out.append({'phase':phase,'candidate_tools':[{'name':t,'availability':('AVAILABLE' if t in inventory else 'NOT_VERIFIED')} for t in tools]})
 result = {'schema':'ugd.route.v1','task':task,'advisory_only':True,'phases':out}
 catalog = Path(__file__).resolve().parents[1] / 'official-unity-catalog.json'
 if catalog.exists(): result['official_unity'] = official_route(task, catalog, 'git')
 community = Path(__file__).resolve().parents[1] / 'gamedev-catalog.json'
 if community.exists() and project: result['game_dev'] = gamedev_route(task, community, project)
 return result
def main():
 p=argparse.ArgumentParser();p.add_argument('task');p.add_argument('--project');p.add_argument('--available',action='append',default=[]);a=p.parse_args();print(json.dumps(plan(a.task,a.available,a.project),ensure_ascii=False,indent=2))
if __name__=='__main__':main()
