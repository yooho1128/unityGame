// Execute the actual prototype's finale path, including a simultaneous knockout.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const path=require('node:path'),root=path.resolve(__dirname,'..'),html=fs.readFileSync(path.join(root,'Prototype/shadow-theater.html'),'utf8');
const rules=html.slice(html.indexOf('const STAR_BASE='),html.indexOf('function aiChoose('));
const handle=html.slice(html.indexOf('async function handleFaints(){'),html.indexOf('\n// 연출',html.indexOf('async function handleFaints(){')));
const context=vm.createContext({URLSearchParams,location:{search:''},Math,console});
vm.runInContext(rules+`;
let B,G;
const unit=hp=>({fainted:hp===0,d:{goldReward:20},inst:{star:false}});
const PA=()=>B.p[0],EA=()=>B.e[0];
const P={faint:async()=>{},say:async()=>{}};
function updateBar(){};function refreshInfo(){};
const expReward=()=>0;
function finish(result){B.outcome.result=result;B.ended=true;concludeShowcase(result)};
${handle}
globalThis.run=async function(fainted){
 B={ended:false,p:[unit(fainted?0:10)],e:[unit(0)],aud:{type:'NoUnderstudy',state:'Pending'},ctx:{mode:'Wild'},outcome:{exp:0}};
 G={streak:4,bestStreak:4,audienceDone:0};
 await handleFaints();return {result:B.outcome.result,ok:B.outcome.audOk,state:B.aud.state,gold:B.outcome.audGold||0,streak:G.streak};
};`,context);
// A living player needs no actual experience/UI work in this focused test.
vm.runInContext('function addExp(){return 0}',context);
(async()=>{
 const mutual=await context.run(true);assert.equal(mutual.result,'Victory');assert.equal(mutual.ok,false);assert.equal(mutual.state,'Failed');assert.equal(mutual.gold,0);
 const clean=await context.run(false);assert.equal(clean.ok,true);assert.equal(clean.gold,15);assert.equal(clean.streak,5);
 const manager=fs.readFileSync(path.join(root,'Assets/Scripts/Battle/BattleManager.cs'),'utf8');
 const start=manager.indexOf('private IEnumerator HandleFaints()'),end=manager.indexOf('private void RollLoot()',start);const body=manager.slice(start,end);
 assert.ok(body.indexOf('_audience.NotifyPlayerFaint()')<body.indexOf('Finish(BattleResult.Victory'),'Unity checks player knockout before victory');
 assert.match(html,/async function startGame\(id\)\{\s*G\.streak=0; G\.bestStreak=0; G\.audienceDone=0/);
 for(const m of html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/g))new vm.Script(m[1]);
 console.log('PASS: real prototype mutual knockout rejects flawless reward; clean victory rewards; Unity ordering; new-game reset; JS syntax');
})().catch(e=>{console.error(e);process.exit(1)});
