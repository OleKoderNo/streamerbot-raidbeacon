// Headless logic check. This does not test browser rendering or OBS compatibility.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const source = fs.readFileSync(__dirname + '/fleet-overlay.js', 'utf8');

async function check(viewers, brokenAsset = false) {
  const frames = [], logs = [], errors = [];
  const drawing = new Proxy({}, {get: (o,k) => o[k] || (()=>{}), set: (o,k,v)=>(o[k]=v,true)});
  const canvas = {width:1920,height:1080,getContext:()=>drawing};
  const errorBox = {hidden:true,textContent:''};
  class Image {
    set src(value) {
      const sizes = {'ship.png':[144,256],'cannon.png':[480,160],'impact.png':[1280,320]};
      [this.naturalWidth,this.naturalHeight] = sizes[value.split('/').pop()];
      queueMicrotask(()=> brokenAsset ? this.onerror() : this.onload());
    }
  }
  const context = vm.createContext({
    window:{innerWidth:1920,innerHeight:1080,addEventListener:()=>{}},
    document:{getElementById:id=>id==='fleet'?canvas:errorBox},
    Image, console:{info:m=>logs.push(m),error:e=>errors.push(e.message)},
    requestAnimationFrame:cb=>frames.push(cb),
  });
  vm.runInContext(source.replace('testViewers: 12,',`testViewers: ${viewers},`), context);
  await new Promise(resolve=>setImmediate(resolve));
  let n=0;
  while(frames.length && n<100000) frames.shift()(n++ * (1000/60));
  if (viewers < 1 || brokenAsset) {
    assert.equal(errorBox.hidden,false);
    assert.equal(logs.some(x=>x.includes('COMPLETE')),false);
  } else {
    assert.ok(n<100000, 'Animation must terminate');
    const count=viewers*5;
    assert.equal(logs.at(-1),`[RaidBeacon] COMPLETE ships=${count}/${count} shots=${count*2}/${count*2} impacts=${count*2}/${count*2}`);
    assert.equal(errors.length,0);
  }
  console.log(`PASS: viewers=${viewers}, missingAsset=${brokenAsset}`);
}
(async()=>{
  for (const n of [1,12,100,0]) await check(n);
  await check(1,true);
})().catch(error=>{console.error(error);process.exitCode=1;});
