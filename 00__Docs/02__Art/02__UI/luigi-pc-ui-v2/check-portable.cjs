const {chromium}=require('C:/Users/eatyourmeat/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const assert=require('node:assert/strict');
(async()=>{
const browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const page=await browser.newPage({viewport:{width:1200,height:1000}});
const requests=[];const errors=[];
const linked=process.argv.includes('--linked');
page.on('pageerror',error=>errors.push(error.message));
page.on('requestfailed',request=>console.log('REQUEST FAILED',request.url(),request.failure()?.errorText));
page.on('response',response=>{if(response.status()>=400)errors.push(response.url()+': '+response.status());});
if(!linked)await page.route('http**/*',route=>{requests.push(route.request().url());return route.abort();});
await page.goto('file:///'+__dirname.replaceAll('\\','/')+'/'+(linked?'claude-import.html':'blood-beans-luigi-portable.html'));
for(const character of ['vampire','zombie','werewolf','witch']){
 await page.click('[data-character="'+character+'"]');
 for(const img of await page.locator('.scene>img').all()){
  await img.scrollIntoViewIfNeeded();await img.evaluate(i=>i.decode());
  assert.ok((await img.getAttribute('src')).startsWith(linked?'https://blood-beans-luigi-ui.free-earth-2790.chatgpt.site/images/':'data:image/webp;base64,'));
 }
}
for(let b=0;b<4;b++){await page.click('button[data-bag="'+b+'"]');assert.equal(await page.locator('.night-bag').getAttribute('data-bag'),String(b));}
assert.deepEqual(requests,[]);assert.deepEqual(errors,[]);
console.log(linked?'PASS: small import HTML, 16 published screenshots, 4 bags, no missing assets or script errors.':'PASS: 16 embedded screenshots, 4 bags, zero network requests or script errors.');
await browser.close();
})().catch(error=>{console.error(error);process.exit(1)});
