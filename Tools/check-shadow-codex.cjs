const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({executablePath:'/usr/bin/chromium',headless:true,args:['--no-sandbox']});
 try {
  const page=await browser.newPage({viewport:{width:1280,height:900}});
  const errors=[]; page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:4174/shadow-codex/');
  assert.equal(await page.locator('#grid .card').count(),191);
  assert.equal(await page.locator('#elementTable tbody td').count(),121);
  const ids=await page.evaluate(()=>S.filter(s=>s.id.startsWith('ele_')).map(s=>s.id));
  for(const id of ids){
   await page.locator('#q').fill(id);
   assert.equal(await page.locator('#grid .card').count(),1);
   await page.locator('#grid .card').click();
   await page.locator('#fieldImg').waitFor();
   await page.waitForTimeout(150);
   const first=await page.locator('#fieldImg').getAttribute('src');
   await page.waitForTimeout(450);
   assert.notEqual(await page.locator('#fieldImg').getAttribute('src'),first);
   assert.equal(await page.locator('#panel img[src$="_restored.webp"]').count(),1);
   assert.equal(await page.locator('#panel').evaluate(p=>[...p.querySelectorAll('img')].every(i=>i.complete&&i.naturalWidth>0)),true);
   await page.keyboard.press('Escape');
  }
  await page.locator('#q').fill('');
  for(const el of ['None','Flame','Water','Grass','Frost','Light','Shade','Earth','Electric','Wind','Psychic']){
   await page.locator('#fEl').selectOption(el);
   assert.ok(await page.locator('#grid .card').count()>0);
  }
  await page.setViewportSize({width:390,height:844});
  await page.locator('#q').fill('ele_dream_marble_rabbit');
  await page.locator('#grid .card').click();
  await page.screenshot({path:'/tmp/codex-expansion-mobile.png'});
  await page.locator('#closeBtn').click();
  assert.deepEqual(errors,[]);
  console.log('PASS: 191 cards; 11 new details, portraits/evolutions/animation; 11 filters; 121 cells; mobile; no JS errors');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
