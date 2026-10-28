"""Bounded source-data checks and neutral-hit estimates, not a Unity battle simulation."""
import json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
data=root/'Assets/Resources/Data'
rows=json.loads((data/'BattleDifficultyCatalog.json').read_text())['chapters']
assert [r['throughAct'] for r in rows]==[1,3,5,8]
for i,r in enumerate(rows):
    assert 0<r['bossDamage']<r['enemyDamage']<=1
    assert 1<=r['enemyVulnerability']<r['bossVulnerability']
    assert 0<r['maxHitRatio']<1
    if i:
        assert rows[i-1]['enemyDamage']<r['enemyDamage']
        assert rows[i-1]['bossDamage']<r['bossDamage']
        assert rows[i-1]['bossVulnerability']>r['bossVulnerability']
shadows={};skills={}
for p in data.glob('*Catalog.json'):
    c=json.loads(p.read_text())
    shadows.update({s['shadowId']:s for s in c.get('shadows',[])})
    skills.update({s['skillId']:s for s in c.get('skills',[])})
boss=shadows['headless_guard']
stat=lambda s,level,k:round(s['base'+k]+s[k.lower()+'Growth']*(level-1))
hp,attack,defense=[stat(boss,28,k) for k in ('Hp','Atk','Def')]
assert hp==657 and skills['loyal_guard']['healRatio']==.5
row=rows[1]
print('Source case: headless_guard Lv28 HP',hp,'heal',round(hp*.5),'; Act2 enemy damage ×',row['bossDamage'],'player damage ×',row['bossVulnerability'])
for id,level in [('ash_priest',23),('tomb_candle',27),('kneeling_captain',27)]:
    s=shadows[id];h,a,d=[stat(s,level,k) for k in ('Hp','Atk','Def')]
    basic=skills[s['basicAttack']]
    df=defense*(.5 if basic['damageType']=='Magical' else 1)
    out=a*basic['damageMultiplier']*100/(100+df)*.95*(1+(s.get('critRate',0)+basic.get('bonusCritRate',0))*.5)
    cleave=skills['headless_cleave']
    incoming=attack*cleave['damageMultiplier']*100/(100+d)*.95*(1+(boss['critRate']+cleave['bonusCritRate'])*.5)
    after_in=min(incoming*row['bossDamage'],h*row['maxHitRatio'])
    assert after_in<incoming and out*row['bossVulnerability']>out
    print(f'{id} Lv{level} (Echo, neutral, no statuses/passives): basic {round(out)}→{round(out*row["bossVulnerability"])}; boss cleave {round(incoming)}→{round(after_in)}')
manager=(root/'Assets/Scripts/Battle/BattleManager.cs').read_text()
assert 'enemy.ConfigureDifficulty(difficulty)' in manager
assert 'RegionRepository.Get(context.regionId)' in manager
grant=manager[manager.index('private IEnumerator GrantExp('):manager.index('#endregion',manager.index('private IEnumerator GrantExp('))]
assert 'SplitExperience(' not in grant and 'foreach (BattleUnit receiver in _playerUnits)' in grant
assert 'PartyExperiencePolicy.Amount(reward, participated)' in grant
assert 'PartyExperiencePolicy.Apply(receiver.Instance, exp)' in grant
policy=(root/'Assets/Scripts/Battle/PartyExperiencePolicy.cs').read_text()
assert 'participated ? reward' in policy and 'reward / 2' in policy and 'if (fainted) shadow.currentHp = 0;' in policy
calc=(root/'Assets/Scripts/Battle/DamageCalculator.cs').read_text()
assert calc.count('attacker.Difficulty.damage * defender.Difficulty.vulnerability')==3
assert calc.count('LimitEnemyHit(attacker, defender,')==3
assert 'attacker.Side != BattleSide.Enemy' in calc
print('PASS: monotonic easing curve, reported boss source stats, three known party neutral-hit estimates, attack/preview/joint/cap wiring, undiluted party experience and KO preservation. Not a full six-member reproduction.')
