const {chromium}=require('playwright');
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({executablePath:'/usr/bin/chromium',headless:true,args:['--no-sandbox']});
 try{
  const page=await browser.newPage({viewport:{width:1280,height:900}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('http://127.0.0.1:4174/shadow-codex/');
  const count=await page.evaluate(async()=>{
   let total=0;
   for(const s of S.filter(s=>s.f.some(Boolean))){
    openDetail(s.id,false);
    await Promise.all([...document.querySelectorAll('#panel img')].map(img=>img.decode()));
    const forms=s.f.filter(Boolean);total+=forms.length;
    if(document.querySelectorAll('.form-walk').length!==forms.length)throw Error('Missing stage walking image: '+s.id);
    for(const f of forms){
     if(!f.image||f.fieldImages?.length!==2)throw Error('Incomplete form: '+s.id);
     await Promise.all(f.fieldImages.map(src=>new Promise((resolve,reject)=>{const im=new Image();im.onload=resolve;im.onerror=reject;im.src='/shadow-codex/img/'+src})));
    }
   }return total;
  });assert.equal(count,173);
  await page.evaluate(()=>openDetail('knight',false));
  const before=await page.locator('.form-walk').first().getAttribute('src');await page.waitForTimeout(450);
  assert.notEqual(await page.locator('.form-walk').first().getAttribute('src'),before);
  await page.setViewportSize({width:390,height:844});await page.screenshot({path:'/tmp/evolution-codex-mobile.png',fullPage:true});
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  await page.emulateMedia({reducedMotion:'reduce'});await page.evaluate(()=>openDetail('knight',false));
  const still=await page.locator('.form-walk').first().getAttribute('src');await page.waitForTimeout(500);
  assert.equal(await page.locator('.form-walk').first().getAttribute('src'),still);assert.deepEqual(errors,[]);
  console.log('PASS: all 173 form portraits + 346 walking files load; animation; reduced motion; mobile width; no JS errors');
 }finally{await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
