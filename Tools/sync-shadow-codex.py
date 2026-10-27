"""Update the existing openWorld HTML codex, retaining all legacy lore and growth data."""
import json
import re
import sys
import shutil
from pathlib import Path
from PIL import Image

root=Path(__file__).resolve().parents[1]
target=Path(sys.argv[1]).resolve()/'public/shadow-codex'
html_path=target/'index.html'
html=html_path.read_text()
pattern=r'(<script id="data" type="application/json">)([\s\S]*?)(</script>)'
data=json.loads(re.search(pattern,html)[2])
catalog=json.loads((root/'Assets/Resources/Data/ElementExpansionCatalog.json').read_text())
elements=json.loads((root/'Assets/Resources/Data/ElementCatalog.json').read_text())
regions=json.loads((root/'Assets/Resources/Data/RegionCatalog.json').read_text())['regions']
encounters=json.loads((root/'Assets/Resources/Data/ElementExpansionEncounters.json').read_text())['entries']
ids={s['shadowId'] for s in catalog['shadows']}
data['shadows']=[s for s in data['shadows'] if s['id'] not in ids]
assert len(data['shadows'])==180,'Unexpected legacy roster; inspect before exporting'
for s in catalog['shadows']:
    forms=[]
    for key in ('restored','salvation','grudge'):
        f=s.get(key)
        forms.append(None if not f else dict(name=f['formName'],color=f['accentColor'],lv=f['requiredLevel'],flag=f['requiredFlag'],how=[],
            mult=[f[k+'Multiplier'] for k in ('hp','atk','def','spd')],crit=f['bonusCritRate'],eva=f['bonusEvasion'],skills=f['bonusSkills'],lore=f['loreAppend'],auto=False,
            image=s['shadowId']+'_restored.webp'))
    locations=[]
    for e in encounters:
        if e['shadowId']!=s['shadowId']: continue
        r=next(r for r in regions if r['regionId']==e['regionId'])
        locations.append(dict(kind='랜덤 조우',region=r['displayName'],act=r['act'],lv=f'{r["recommendedLevelMin"]}–{r["recommendedLevelMax"]}'))
    data['shadows'].append(dict(id=s['shadowId'],n=s['displayName'],t=s['title'],tier=s['growthTier'],el=s['element'],role=s['role'],c=s['accentColor'],
        base=[s[k] for k in ('baseHp','baseAtk','baseDef','baseSpd')],gr=[s[k] for k in ('hpGrowth','atkGrowth','defGrowth','spdGrowth')],
        crit=s['critRate'],eva=s['evasion'],basic=s['basicAttack'],sk=s['skills'],cap=s['baseCaptureRate'],exp=s['expReward'],gold=s['goldReward'],ll=s['loreLocked'],lu=s['loreUnlocked'],gen=False,f=forms,w=locations,fld=True))
for s in catalog['skills']:
    data['skills'][s['skillId']]=dict(n=s['displayName'],d=s['description'],fp=s['fpCost'],fg=s['fpGain'],ty=s['damageType'],el=s['element'],
        pw=s['damageMultiplier'],acc=s['accuracy'],tg=s['target'],heal=s['healRatio'],st=s['statusEffect'],sc=s['statusChance'],sd=s['statusDuration'],ult=s['isUltimate'],lv=s['requiredLevel'],neutral=s['forceNeutralElement'])
data['elements']=elements['elements']
for s in data['shadows']:
    for frame in (1,2):
        source=root/'Assets/Art/Final/Field'/f'{s["id"]}_{frame:02}.png'
        if not source.exists(): raise FileNotFoundError(source)
        shutil.copyfile(source,target/'img'/f'{s["id"]}_f{frame:02}.png')
    s['fld']=True
for s in catalog['shadows']:
    for suffix in ('','_restored'):
        source=root/'Assets/Art/Final/Portraits'/f'{s["shadowId"]}{suffix}.png'
        Image.open(source).save(target/'img'/f'{s["shadowId"]}{suffix}.webp',lossless=True)
html=re.sub(pattern,lambda m:m[1]+json.dumps(data,ensure_ascii=False,separators=(',',':'))+m[3],html,count=1)
html=re.sub(r'const EL=\{[^\n]*;',"const EL=Object.fromEntries(D.elements.map(e=>[e.id,[e.name,e.color]]));",html,count=1)
html=re.sub(r'const BEATS=\{[^\n]*;',"const BEATS=Object.fromEntries(D.elements.map(e=>[e.id,e.strong]));\nconst matchup=(a,b)=>a===b?1:BEATS[a].includes(b)?1.5:BEATS[b].includes(a)?.75:1;",html,count=1)
html=html.replace("const weak=Object.keys(BEATS).find(a=>BEATS[a]===s.el), resist=BEATS[s.el];", "const weak=Object.keys(EL).filter(a=>matchup(a,s.el)>1).map(a=>EL[a][0]).join(' · '), resist=Object.keys(EL).filter(a=>matchup(a,s.el)<1).map(a=>EL[a][0]).join(' · ');")
html=html.replace("${weak?EL[weak][0]:'없음'}","${weak||'없음'}").replace("${resist?EL[resist][0]:'없음'}","${resist||'없음'}")
html=re.sub(r"  if[(]k[.](?:neutral|el)[^\n]*", "  if(k.neutral)parts.push('무속성 (고정)');else if(k.el&&k.el!=='None')parts.push(EL[k.el][0]);else parts.push('시전자 속성');",html,count=1)
needle='  return `<div class="node" style="--nc:${color}">'
if '${f.image?' not in html:
    html=html.replace(needle,needle+'\n    ${f.image?`<img src="/shadow-codex/img/${esc(f.image)}" alt="${esc(f.name)}" style="width:128px;height:128px;object-fit:contain">`:""}')
if 'id="elementChart"' not in html:
    html=html.replace('<main','<details id="elementChart" class="wrap"><summary>11속성 상성표 보기 · 공격 기준</summary><div class="tbl"><table id="elementTable"></table></div><p>빛↔어둠, 에스퍼↔무속성은 서로 1.5배. 면역 없음.</p></details>\n<main',1)
    html=html.replace('const h=location.hash.slice(1);',"document.getElementById('elementTable').innerHTML='<thead><tr><th>공격 ↓ / 방어 →</th>'+Object.values(EL).map(e=>'<th>'+e[0]+'</th>').join('')+'</tr></thead><tbody>'+Object.keys(EL).map(a=>'<tr><th>'+EL[a][0]+'</th>'+Object.keys(EL).map(b=>'<td>'+matchup(a,b)+'×</td>').join('')+'</tr>').join('')+'</tbody>';\nconst h=location.hash.slice(1);")
html_path.write_text(html)
print(f'Updated {len(data["shadows"])} shadows, {len(data["skills"])} skills, 11 elements, 382 field frames and 22 new portraits in {target}')
