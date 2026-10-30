"""Synthetic diagnostics fixtures, never real play data."""
import copy,importlib.util,uuid
from pathlib import Path
spec=importlib.util.spec_from_file_location('summary',Path(__file__).with_name('summarize-battle-reviews.py'));summary=importlib.util.module_from_spec(spec);spec.loader.exec_module(summary)
save=uuid.uuid4().hex
unit=dict(shadowId='synthetic',levelBefore=24,levelAfter=24,hpBefore=100,hpAfter=0,maxHpBefore=100,maxHpAfter=100,damageTaken=200,healingReceived=100,healingSkillUses=2,xpBefore=100,xpAfter=150)
def entry(result):return dict(id=uuid.uuid4().hex,sessionId=uuid.uuid4().hex,saveGuid=save,startedUtc='2026-10-30T00:00:00+00:00',result=result,turns=12,playerFaints=6,declaredRewardGold=5,declaredExperience=300,goldBefore=100,goldAfter=107,players=[copy.deepcopy(unit) for _ in range(6)],enemies=[copy.deepcopy(unit)],items=[dict(itemId='salve',before=5,after=3)],regionId='synthetic_region',encounterId='synthetic_boss',gameVersion='test',buildGuid='synthetic')
log=dict(version=1,saveGuid=save,entries=[entry(r) for r in ('Defeat','Victory','Captured','Escaped','InProgress','Interrupted')])
report=summary.summarize([log]);g=report['groups'][0]
assert report['excluded']==dict(InProgress=1,Interrupted=1) and g['samples']==4 and g['results']['Defeat']==1
assert g['avgPartySize']==6 and g['avgPlayerHealing']==600 and g['avgEnemyHealing']==100 and g['avgItemsConsumed']==2
assert g['avgActualPartyXp']==300 and g['avgDeclaredGold']==5 and g['avgSettlementGoldDelta']==7
checks=3
for mutate in (lambda x:x.update(version=2),lambda x:x.update(saveGuid='../../bad'),lambda x:x['entries'].append(x['entries'][0]),lambda x:x['entries'][0].update(result='Unknown'),lambda x:x['entries'][0]['players'][0].update(hpAfter=101),lambda x:x['entries'][0]['items'][0].update(before=-1),lambda x:x['entries'][0].update(startedUtc='bad'),lambda x:x['entries'][0]['players'][0].update(healingReceived=True)):
    bad=copy.deepcopy(log);mutate(bad)
    try:summary.summarize([bad])
    except ValueError:checks+=1
    else:raise AssertionError('invalid fixture accepted')
try:summary.summarize([log,log])
except ValueError:checks+=1
else:raise AssertionError('overlapping files accepted')
print(f'PASS: {checks} synthetic summary/validation checks; incomplete excluded, six-vs-one metrics, schema/duplicate/negative/bool/HP rejection (not gameplay data)')
