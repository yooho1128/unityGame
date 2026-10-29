"""Bake objective locations from authored Unity scene-generator calls, never guessed from ID names."""
import json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];DATA=ROOT/'Assets/Resources/Data'
regions=json.loads((DATA/'RegionCatalog.json').read_text())['regions'];scenes={r['sceneName']:r for r in regions};quests=json.loads((DATA/'QuestCatalog.json').read_text())['quests'];identities={e['id']:e for e in json.loads((DATA/'NpcIdentityCatalog.json').read_text())['entries']};code=(ROOT/'Assets/Editor/PlayablePrologueGenerator.cs').read_text()
def arguments(text):
 result=[];start=0;depth=0;quoted=False;escape=False
 for i,c in enumerate(text):
  if quoted:
   if escape:escape=False
   elif c=='\\':escape=True
   elif c=='"':quoted=False
  elif c=='"':quoted=True
  elif c in '([{':depth+=1
  elif c in ')]}':depth-=1
  elif c==',' and depth==0:result.append(text[start:i].strip());start=i+1
 return result+[text[start:].strip()]
def calls(body,name):
 for m in re.finditer(r'\b'+name+r'\(',body):
  start=m.end();depth=1;quoted=False;escape=False
  for i in range(start,len(body)):
   c=body[i]
   if quoted:
    if escape:escape=False
    elif c=='\\':escape=True
    elif c=='"':quoted=False
   elif c=='"':quoted=True
   elif c=='(':depth+=1
   elif c==')':
    depth-=1
    if depth==0:yield arguments(body[start:i]);break
def literal(s):return json.loads(s)
def cell(s):return [int(v) for v in re.search(r'Vector2Int\(\s*(-?\d+),\s*(-?\d+)\)',s).groups()]
default_boss=re.search(r'encounterId = "([^"]+)"',(ROOT/'Assets/Scripts/Story/BossEncounterTrigger.cs').read_text())[1]
entries={}
def add(kind,target,region,pos,label):
 key=(kind,target);entry=dict(type=kind,targetId=target,regionId=region['regionId'],x=pos[0],y=pos[1],label=label)
 assert key not in entries or entries[key]==entry,key
 entries[key]=entry
for body in re.split(r'        private static void ',code):
 scene=re.search(r'BeginFieldScene\("([^"]+)"',body)
 if not scene or scene[1] not in scenes:continue
 r=scenes[scene[1]]
 for a in calls(body,'CreateNpc'):
  if a[0]=='map.fieldRoot':add('Talk',literal(a[3]),r,cell(a[2]),identities[literal(a[3])]['name'])
 for a in calls(body,'CreateBossEncounter'):
  if a[0]=='map.fieldRoot':add('Defeat',literal(a[4]),r,cell(a[2]),r['bossShadowName'] or '이 지역의 보스')
 for a in calls(body,'CreateBoss'):
  if a[0]=='map.fieldRoot':add('Defeat',default_boss,r,cell(a[1]),r['bossShadowName'] or '달빛의 미망인')
 for a in calls(body,'CreateAreaTrigger'):
  if a[0]=='map.fieldRoot':add('Reach',literal(a[2]),r,cell(a[1]),r['displayName'])
# Expansion regions use the authored arrival point in CreateExpansionRegions.
for r in regions:
 if r['order']>40:add('Reach','area_'+r['regionId'],r,[r['arrivalX'],r['arrivalY']],r['displayName'])
required={(o['type'],o['targetId']) for q in quests for o in q['objectives'] if o['targetId']!='*'}
missing=required-entries.keys();assert not missing,missing
selected=[entries[k] for k in sorted(required)]
(DATA/'QuestNavigationCatalog.json').write_text(json.dumps(dict(entries=selected),ensure_ascii=False,indent=2)+'\n')
print(f'Exported {len(selected)} authored destinations for {len(quests)} quests; every non-wildcard objective resolves')
