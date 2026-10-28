const {chromium}=require('playwright'),assert=require('node:assert/strict');
(async()=>{const browser=await chromium.launch({executablePath:'/usr/bin/chromium',headless:true,args:['--no-sandbox']});try{
 const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(process.env.PROTOTYPE_URL||'http://127.0.0.1:4176/?star');await page.waitForFunction(()=>!!window.__st);
 assert.equal(await page.locator('#btnAuto').count(),0);
 await page.evaluate(async()=>{
  const a=window.__st;await a.startGame('knight');a.G.party[0].level=100;a.G.party[0].currentHp=99999;a.G.speed=100;
  a.G.auto=true; // A stale flag must have no effect even if supplied by older prototype state.
  Math.random=()=>0;window.manualDone=a.symbolBattle(a.symbols.find(s=>s.mode==='Wild'));
 });
 await page.waitForFunction(()=>window.__st.getB()?.resolve);
 const turn=await page.evaluate(()=>window.__st.getB().turn);await page.waitForTimeout(700);
 assert.equal(await page.evaluate(()=>window.__st.getB().turn),turn);
 assert.equal(await page.evaluate(()=>!!window.__st.getB().resolve),true);
 await page.locator('#menu button').filter({hasText:/^공격$/}).click();await page.evaluate(()=>window.manualDone);
 assert.equal(await page.evaluate(()=>window.__st.G.streak),1);
 await page.evaluate(async()=>{
  const a=window.__st;await a.startGame('knight');a.G.party[0].level=1;a.G.party[0].currentHp=1;
  a.G.party.push({...a.G.party[0],instanceId:'manual-backup',shadowId:'mage',level:100,currentHp:99999});a.G.speed=100;a.G.auto=true;
  const sym=a.symbols.find(s=>s.mode==='Wild');sym.shadowId='crow';sym.levelRange=[5,5];window.manualDone=a.symbolBattle(sym);
 });
 await page.waitForFunction(()=>window.__st.getB()?.resolve);await page.locator('#menu button').filter({hasText:/^공격$/}).click();
 await page.waitForFunction(()=>!!window.__st.getB()?.forced);await page.waitForTimeout(500);
 assert.equal(await page.evaluate(()=>!!window.__st.getB().forced),true,'single surviving reserve still waits for player');
 await page.locator('#menu button').first().click();await page.waitForFunction(()=>!!window.__st.getB()?.resolve);
 await page.locator('#menu button').filter({hasText:/^공격$/}).click();await page.evaluate(()=>window.manualDone);
 assert.equal(await page.evaluate(()=>window.__st.G.streak),1);
 await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);assert.deepEqual(errors,[]);
 console.log('PASS: no AUTO UI; stale AUTO flag cannot advance a turn; manual attack/forced switch required; two real battles finish; mobile width; no JS errors');
}finally{await browser.close()}})().catch(e=>{console.error(e);process.exit(1)});
