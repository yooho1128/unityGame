// Execute the prototype's actual healing guards and inspect Unity integration.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const root=path.resolve(__dirname,'..'),read=p=>fs.readFileSync(path.join(root,p),'utf8');
const html=read('Prototype/shadow-theater.html');
const guards=html.slice(html.indexOf('const HEAL_LIMIT='),html.indexOf('class Unit {'));
const context=vm.createContext({});
vm.runInContext(guards+';globalThis.rules={HEAL_LIMIT,HEAL_REST_TURNS,isHealingSkill,canHealSkill}',context);
const r=context.rules,heal={target:'Self',healRatio:.35},otherHeal={target:'Self',healRatio:.55},attack={target:'Enemy',healRatio:0};
assert.equal(r.HEAL_LIMIT,3);assert.equal(r.HEAL_REST_TURNS,2);
const unit={fainted:false,hp:20,maxHp:100,healUses:0,nextHealTurn:1};
for(const turn of [1,4,7]){
 assert.equal(r.canHealSkill(unit,heal,turn),true);
 unit.healUses++;unit.nextHealTurn=turn+r.HEAL_REST_TURNS+1;
 assert.equal(r.canHealSkill(unit,otherHeal,turn+1),false);
 assert.equal(r.canHealSkill(unit,otherHeal,turn+2),false);
}
assert.equal(r.canHealSkill(unit,otherHeal,100),false);
assert.equal(r.canHealSkill(unit,attack,100),true);
assert.equal(r.canHealSkill({...unit,healUses:0,hp:100},heal,100),false);
assert.equal(r.canHealSkill({...unit,healUses:0,fainted:true},heal,100),false);
const budget=read('Assets/Scripts/Battle/HealingSkillBudget.cs');
assert.match(budget,/Limit = 3/);assert.match(budget,/RestTurns = 2/);
assert.match(budget,/turn \+ RestTurns \+ 1/);
const manager=read('Assets/Scripts/Battle/BattleManager.cs');
assert.ok(manager.indexOf('!user.CanUseHealingSkill(skill, Turn)')<manager.indexOf('AddFp(a.side, skill.fpGain - skill.fpCost)'));
assert.match(manager,/user.UseHealingSkill\(skill, Turn\)/);
assert.match(manager,/profile, Turn\)/);
assert.match(read('Assets/Scripts/Battle/BattleAI.cs'),/!self.CanUseHealingSkill\(s, battleTurn\)/);
assert.match(read('Assets/Editor/ShadowBattleSimulator.cs'),/actor.healing.CanUse\(turn\)/);
assert.match(read('Assets/Scripts/UI/BattleUIController.cs'),/HealingSkills.Remaining/);
for(const script of html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/g))new vm.Script(script[1]);
console.log('PASS: prototype runtime healing guards (3 shared uses, 2 rest turns), cross-skill/healthy/fainted restrictions; Unity/AI/simulator/UI wiring and JS syntax. Unity not executed.');
