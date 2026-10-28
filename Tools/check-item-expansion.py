"""Static content integration checks; does not run Unity or emulate combat."""
import json
import re
from pathlib import Path

root=Path(__file__).resolve().parents[1]
data=root/'Assets/Resources/Data'
new=json.loads((data/'ItemExpansionCatalog.json').read_text())['items']
all_items=[i for p in data.glob('*Catalog.json') for i in json.loads(p.read_text()).get('items',[])]
ids=[i['itemId'] for i in all_items]
assert len(new)==8 and len(all_items)==len(set(ids))==21
assert len({i['iconStyle'] for i in new})==8
generator=(root/'Assets/Editor/CoreContentBatchGenerator.cs').read_text()
shops=(root/'Assets/Scripts/Story/TownServices.cs').read_text()
common=shops.split('CommonItems =',1)[1].split(';',1)[0]
regions=json.loads((data/'RegionCatalog.json').read_text())['regions']
for i in new:
    assert f'"{i["itemId"]}"' in common,i['itemId']
    assert any(r.get('isSettlement') and r['act']>=i['shopUnlockAct'] for r in regions)
    assert 0<=i['sellPrice']<i['buyPrice'] and i['value']>0
    assert re.fullmatch(r'#[0-9A-Fa-f]{6}',i['iconColor'])
    assert f'case "{i["iconStyle"]}":' in generator
    assert i['usableInBattle'] or i['usableInField']
    if i['effectType']=='RestoreAndCleanse':
        assert 0<i['value']<=1 and i['usableInBattle'] and not i['usableInField']
    if i['usableInField']: assert i['effectType'] in ('HealFlat','HealRatio','GainExperience')
assert 'Data/ItemExpansionCatalog.json' in generator
assert 'catalog.items.Length != 21' in generator
assert generator.index('BuildRegionalExpansion(baseCatalog)')<generator.index('Data/ItemExpansionCatalog.json')
expansion=json.loads((data/'ElementExpansionCatalog.json').read_text())
droppers=[s for s in expansion['shadows'] if s.get('dropItemId')]
assert len(droppers)==4
for s in droppers:
    assert s['dropItemId'] in ids and 0<s['dropChance']<=1
    assert 1<=s['dropMinCount']<=s['dropMaxCount']
effect_enum=(root/'Assets/Scripts/Data/ItemData.cs').read_text().split('public enum ItemEffectType',1)[1].split('}',1)[0]
effects=re.findall(r'^\s*(\w+)\s*(?:,|//)',effect_enum,re.M)
assert effects==['HealFlat','HealRatio','CureStatus','GainFP','CaptureBoost','TraversalTool','GainExperience','RestoreAndCleanse']
battle=(root/'Assets/Scripts/Battle/BattleManager.cs').read_text()
assert battle.count('case ItemEffectType.RestoreAndCleanse:')==2
assert 'target.RestoreAndCleanse(item.value)' in battle
print('PASS: 21 unique items; 8 new shop-reachable items with distinct icon styles; 4 valid drops; effect enum compatibility and battle/field wiring')
