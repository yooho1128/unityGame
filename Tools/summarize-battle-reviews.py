"""Summarize opt-in local battle review JSON. No thresholds, uploads or balance writes."""
import argparse
import collections
import json
from pathlib import Path
import statistics
import uuid
from datetime import datetime

FINISHED={'Victory','Defeat','Captured','Escaped'}
RESULTS=FINISHED|{'InProgress','Interrupted'}

def guid(value):
    try:return uuid.UUID(value).hex
    except (ValueError,TypeError,AttributeError):raise ValueError('invalid GUID')

def number(row,key):
    value=row.get(key)
    if type(value) is not int or not 0<=value<=9223372036854775807:raise ValueError('invalid nonnegative integer: '+key)
    return value

def validate(data):
    if data.get('version')!=1:raise ValueError('unsupported battle review schema')
    save=data.get('saveGuid');guid(save)
    entries=data.get('entries')
    if not isinstance(entries,list) or len(entries)>20:raise ValueError('invalid recent history')
    ids=set()
    for entry in entries:
        if not isinstance(entry,dict):raise ValueError('invalid entry')
        identity=guid(entry.get('id'));guid(entry.get('sessionId'))
        if identity in ids or entry.get('saveGuid')!=save:raise ValueError('duplicate entry or mismatched save')
        ids.add(identity)
        if entry.get('result') not in RESULTS:raise ValueError('unknown result')
        try:datetime.fromisoformat(entry['startedUtc'].replace('Z','+00:00'))
        except (ValueError,KeyError,AttributeError,TypeError):raise ValueError('invalid start time')
        for key in ('turns','playerFaints','declaredRewardGold','declaredExperience','goldBefore','goldAfter'):number(entry,key)
        for side in ('players','enemies'):
            rows=entry.get(side)
            if not isinstance(rows,list) or not rows:raise ValueError('missing combat party')
            for unit in rows:
                if not isinstance(unit,dict) or not unit.get('shadowId'):raise ValueError('missing shadow')
                for key in ('levelBefore','levelAfter','hpBefore','hpAfter','maxHpBefore','maxHpAfter','damageTaken','healingReceived','healingSkillUses','xpBefore','xpAfter'):number(unit,key)
                if min(unit['levelBefore'],unit['levelAfter'],unit['maxHpBefore'],unit['maxHpAfter'])<1 or unit['hpBefore']>unit['maxHpBefore'] or unit['hpAfter']>unit['maxHpAfter']:raise ValueError('invalid level or HP')
        if not isinstance(entry.get('items'),list):raise ValueError('missing inventory snapshot')
        for item in entry['items']:
            if not isinstance(item,dict) or not item.get('itemId'):raise ValueError('missing item id')
            number(item,'before');number(item,'after')
    return entries

def summarize(logs):
    groups=collections.defaultdict(list);seen=set();saves=set();excluded=collections.Counter()
    for data in logs:
        entries=validate(data);saves.add(guid(data['saveGuid']))
        for e in entries:
            identity=(guid(e['saveGuid']),guid(e['id']))
            if identity in seen:raise ValueError('same battle appears in multiple input files')
            seen.add(identity)
            if e['result'] not in FINISHED:excluded[e['result']]+=1;continue
            groups[(e.get('regionId',''),e.get('encounterId',''),e.get('gameVersion',''),e.get('buildGuid',''))].append(e)
    output=[]
    for key,rows in sorted(groups.items()):
        def avg(f):return round(statistics.mean(f(e) for e in rows),2)
        count=collections.Counter(e['result'] for e in rows)
        output.append(dict(zip(('region','encounter','gameVersion','buildGuid'),key),samples=len(rows),results=dict(count),
            avgTurns=avg(lambda e:e['turns']),avgPartySize=avg(lambda e:len(e['players'])),
            avgPartyLevel=avg(lambda e:statistics.mean(u['levelBefore'] for u in e['players'])),
            avgEnemyLevel=avg(lambda e:statistics.mean(u['levelBefore'] for u in e['enemies'])),
            avgPlayerFaints=avg(lambda e:e['playerFaints']),
            avgPlayerHealing=avg(lambda e:sum(u['healingReceived'] for u in e['players'])),
            avgEnemyHealing=avg(lambda e:sum(u['healingReceived'] for u in e['enemies'])),
            avgPlayerHealSkillUses=avg(lambda e:sum(u['healingSkillUses'] for u in e['players'])),
            avgEnemyHealSkillUses=avg(lambda e:sum(u['healingSkillUses'] for u in e['enemies'])),
            avgItemsConsumed=avg(lambda e:sum(max(0,i['before']-i['after']) for i in e['items'])),
            avgActualPartyXp=avg(lambda e:sum(max(0,u['xpAfter']-u['xpBefore']) for u in e['players'])),
            avgDeclaredGold=avg(lambda e:e['declaredRewardGold']),
            avgSettlementGoldDelta=avg(lambda e:e['goldAfter']-e['goldBefore'])))
    return dict(saveCount=len(saves),excluded=dict(excluded),groups=output,
        note='Recent 20 per save only; selected samples, not a balance verdict. Settlement gold may include quest rewards. No shop spending/cost or later healing cost is inferred.')

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('files',nargs='+',type=Path);args=parser.parse_args()
    try:report=summarize([json.loads(path.read_text(encoding='utf-8-sig')) for path in args.files])
    except (ValueError,TypeError,AttributeError,OSError) as error:parser.exit(1,'INVALID: '+str(error)+'\n')
    print(json.dumps(report,ensure_ascii=False,indent=2))
if __name__=='__main__':main()
