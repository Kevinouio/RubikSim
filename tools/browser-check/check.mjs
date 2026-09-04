import { chromium } from 'playwright';
import assert from 'node:assert/strict';
import { mkdir, writeFile, access, readFile, readdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import os from 'node:os';
import { createHash } from 'node:crypto';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const shellOnly=process.argv.includes('--shell');
const base=process.env.RUBIKSIM_URL||'http://127.0.0.1:8080';
const artifacts=path.join(root,'artifacts');await mkdir(artifacts,{recursive:true});
const manifest=path.join(root,'website/unity/build-manifest.json');
let hasBuild=true;try{await access(manifest);}catch{hasBuild=false;}
if(!shellOnly&&!hasBuild){console.error('BLOCKED: website/unity/build-manifest.json is missing. Build Unity first. Live cube browser checks were NOT RUN.');process.exit(2);}
const browser=await chromium.launch({channel:process.env.RUBIKSIM_BROWSER||'msedge',headless:true});
const page=await browser.newPage({viewport:{width:1440,height:1100},deviceScaleFactor:1});
const errors=[],consoleErrors=[],resourceResponses=new Map(),responseReads=[];
page.on('pageerror',error=>errors.push(error.message));
page.on('console',message=>{if(message.type()==='error')consoleErrors.push(message.text());});
page.on('response',response=>{
  if(!response.url().includes('/unity/Build/'))return;
  responseReads.push(response.allHeaders().then(headers=>{
    // A later reload may legitimately revalidate with304; keep the initial actual body download.
    if(resourceResponses.get(response.url())?.status!==200)resourceResponses.set(response.url(),{status:response.status(),headers});
  }));
});
const evidence={date:new Date().toISOString(),browser:await browser.version(),mode:shellOnly?'website shell only':'actual Unity Web build',url:base,checks:[],unityChecksRun:false,
  hardware:{cpu:os.cpus()[0]?.model,logicalCpus:os.cpus().length,totalMemoryBytes:os.totalmem(),os:os.type(),release:os.release(),architecture:os.arch()}};
function record(name){evidence.checks.push(name);console.log('PASS '+name);}
async function sourceFingerprint(){
  const files=[];
  async function collect(relative){
    for(const entry of await readdir(path.join(root,relative),{withFileTypes:true})){
      const filename=relative+'/'+entry.name;
      if(entry.isDirectory())await collect(filename);else files.push(filename);
    }
  }
  await collect('Assets');
  for(const filename of ['Packages/manifest.json','Packages/packages-lock.json','ProjectSettings/ProjectVersion.txt','ProjectSettings/EditorBuildSettings.asset']){
    try{await access(path.join(root,filename));files.push(filename);}catch(error){if(error.code!=='ENOENT')throw error;}
  }
  // Default JavaScript sort uses ordinal UTF-16 order, matching StringComparer.Ordinal.
  files.sort();const hash=createHash('sha256');
  for(const filename of files){hash.update(Buffer.from(filename+'\0','utf8'));hash.update(await readFile(path.join(root,filename)));hash.update(Buffer.from([0]));}
  return{sha256:hash.digest('hex'),fileCount:files.length,algorithm:'SHA256(UTF8(ordinal-sorted relative forward-slash path + NUL), raw file bytes, NUL)',
    scope:'All Assets files plus existing Packages/manifest.json, Packages/packages-lock.json, ProjectSettings/ProjectVersion.txt and ProjectSettings/EditorBuildSettings.asset'};
}
async function verifyBuildSource(){
  const response=await page.request.get(base.replace(/\/$/,'')+'/unity/build-manifest.json');
  assert.equal(response.status(),200,'The served Unity build manifest must be available before live checks');
  const build=await response.json(),actual=await sourceFingerprint();
  evidence.sourceFingerprint={...actual,manifestSha256:build.sourceSha256||null,unityVersion:build.unityVersion,verified:false};
  assert.match(build.sourceSha256||'',/^[a-f0-9]{64}$/,'Build manifest must contain a lowercase sourceSha256; rebuild the current project');
  assert.equal(actual.sha256,build.sourceSha256,'The Unity player was built from different sources. Rebuild after all Assets/package/build-scene edits before browser verification');
  evidence.sourceFingerprint.verified=true;
  record('Served Unity manifest source SHA256 matches every current hashed project file');
}
await page.addInitScript(()=>{
  window.rubikEvidence={latest:null,disagreements:[],frames:[],solvingFrames:[],playbackFrames:[],unitySolvingFrames:[],unityPlaybackFrames:[],maxSolveSliceMs:0,maxHeapBytes:0,workload:null};
  window.addEventListener('rubik-state',event=>{
    const s=typeof event.detail==='string'?JSON.parse(event.detail):event.detail;
    window.rubikEvidence.latest=s;
    if((!s.animating&&!s.viewAgrees)||!s.animationAgrees)window.rubikEvidence.disagreements.push({version:s.version,animating:s.animating,facelets:s.facelets,view:s.viewFacelets,animationAgrees:s.animationAgrees});
    window.rubikEvidence.maxSolveSliceMs=Math.max(window.rubikEvidence.maxSolveSliceMs,s.solveSliceMs||0,s.maxSolveSliceMs||0);
    window.rubikEvidence.maxHeapBytes=Math.max(window.rubikEvidence.maxHeapBytes,s.wasmHeapBytes||0);
    window.rubikEvidence.unitySolvingFrames.push(...(s.solveFrameSamplesMs||[]));
    window.rubikEvidence.unityPlaybackFrames.push(...(s.playbackFrameSamplesMs||[]));
  });
  let last=performance.now();function frame(now){
    const e=window.rubikEvidence;if(e.latest?.ready){const interval=now-last;e.frames.push(interval);if(e.workload==='solve'&&e.latest.solving)e.solvingFrames.push(interval);if(e.workload==='playback'&&(e.latest.playing||e.latest.animating))e.playbackFrames.push(interval);}
    last=now;requestAnimationFrame(frame);
  }requestAnimationFrame(frame);
});
const snapshot=()=>page.evaluate(()=>window.rubikEvidence.latest);
const idle=()=>page.waitForFunction(()=>{const s=window.rubikEvidence.latest;return s?.ready&&!s.animating&&!s.pending&&!s.solving&&!s.playing;},null,{timeout:120000});
const click=label=>page.getByRole('button',{name:label,exact:true}).click();
async function notation(text){await page.getByLabel('Try a move sequence').fill(text);await click('Apply');}
async function speed(value){await page.locator('#speed').evaluate((el,v)=>{el.value=String(v);el.dispatchEvent(new Event('input',{bubbles:true}));},value);await page.waitForFunction(v=>window.rubikEvidence.latest.speed===v,value);}
async function reset(){await click('Reset cube');await idle();assert.equal((await snapshot()).solved,true);}
async function importState(serialized){await page.getByLabel('Versioned state snapshot').fill(serialized);await click('Validate & import');await page.waitForFunction(value=>window.rubikEvidence.latest.serialized===value,serialized);await idle();}
async function workload(name){await page.evaluate(value=>{window.rubikEvidence.workload=value;},name);}
async function stickerPoint(index){
  await page.locator('#unity-canvas').scrollIntoViewIfNeeded();
  const s=await snapshot(),target=s.stickerTargets?.find(p=>p.index===index);
  assert.ok(target,`Sticker ${index} must be visible and pickable through the actual Unity camera`);
  const box=await page.locator('#unity-canvas').boundingBox();
  return{x:box.x+target.x*box.width,y:box.y+target.y*box.height};
}
async function waitChanged(version){await page.waitForFunction(old=>window.rubikEvidence.latest.version>old,version);await idle();}
async function auditResting(){const s=await snapshot();assert.equal(s.viewAgrees,true);assert.equal(s.viewFacelets,s.facelets);assert.equal(s.animationAgrees,true);}
async function pointerChecks(effects){
  await reset();await speed(12);await click('Reset view');
  await page.waitForFunction(()=>{const p=window.rubikEvidence.latest.camera;return Math.abs(p[0]-34)<.001&&Math.abs(p[1]-24)<.001&&Math.abs(p[2]-8.2)<.001;});
  let point=await stickerPoint(22),version=(await snapshot()).version;
  await page.mouse.click(point.x,point.y);await waitChanged(version);assert.equal((await snapshot()).facelets,effects.F);await auditResting();
  await reset();point=await stickerPoint(22);version=(await snapshot()).version;
  await page.keyboard.down('Shift');await page.mouse.click(point.x,point.y);await page.keyboard.up('Shift');await waitChanged(version);assert.equal((await snapshot()).facelets,effects["F'"]);await auditResting();
  await reset();point=await stickerPoint(22);version=(await snapshot()).version;
  await page.mouse.move(point.x,point.y);await page.mouse.down();await page.mouse.move(point.x+65,point.y,{steps:12});await page.mouse.up();await waitChanged(version);assert.equal((await snapshot()).facelets,effects.E);await auditResting();
  record('Actual sticker picking: click F, Shift-click inverse F, and rightward F-center swipe produce exact expected F/F\u2032/E states');
  let before=await snapshot();const box=await page.locator('#unity-canvas').boundingBox();
  await page.mouse.move(box.x+25,box.y+25);await page.mouse.down();await page.mouse.move(box.x+85,box.y+60,{steps:10});await page.mouse.up();
  await page.waitForFunction(old=>JSON.stringify(window.rubikEvidence.latest.camera)!==JSON.stringify(old),before.camera);assert.equal((await snapshot()).serialized,before.serialized);
  before=await snapshot();await page.mouse.move(box.x+box.width/2,box.y+box.height/2);await page.mouse.wheel(0,-360);
  await page.waitForFunction(distance=>window.rubikEvidence.latest.camera[2]!==distance,before.camera[2]);assert.equal((await snapshot()).serialized,before.serialized);
  await click('Reset view');await page.waitForFunction(()=>{const p=window.rubikEvidence.latest.camera;return Math.abs(p[0]-34)<.001&&Math.abs(p[1]-24)<.001&&Math.abs(p[2]-8.2)<.001;});assert.equal((await snapshot()).serialized,before.serialized);
  record('Background drag and wheel change camera orbit/zoom only; Reset view restores the exact home pose');
  version=(await snapshot()).version;await page.getByLabel('Try a move sequence').focus();await page.keyboard.press('r');await page.waitForTimeout(200);assert.equal((await snapshot()).version,version);await page.getByLabel('Try a move sequence').fill('');
  record('Keyboard input in an HTML form leaves the cube unchanged');
}
async function touchChecks(effects){
  await reset();await click('Reset view');await page.setViewportSize({width:390,height:844});await page.locator('#unity-canvas').scrollIntoViewIfNeeded();
  await page.waitForTimeout(200);await click('Reset view'); // Let Unity adopt the new canvas size before projecting touch targets.
  assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true);
  const cdp=await page.context().newCDPSession(page);await cdp.send('Emulation.setTouchEmulationEnabled',{enabled:true,maxTouchPoints:2});
  const finger=(point,id=1)=>({x:point.x,y:point.y,id,radiusX:2,radiusY:2,force:1});
  const event=async(type,points)=>{await cdp.send('Input.dispatchTouchEvent',{type,touchPoints:points});await page.waitForTimeout(40);};
  try{
    // Native browser touch events feed the real Unity Input.touches path.
    let point=await stickerPoint(22),version=(await snapshot()).version;
    await event('touchStart',[finger(point)]);await event('touchEnd',[]);await waitChanged(version);assert.equal((await snapshot()).facelets,effects.F);await auditResting();
    await reset();point=await stickerPoint(22);version=(await snapshot()).version;
    await event('touchStart',[finger(point)]);for(let step=1;step<=6;step++)await event('touchMove',[finger({x:point.x+step*10,y:point.y})]);await event('touchEnd',[]);await waitChanged(version);assert.equal((await snapshot()).facelets,effects.E);await auditResting();
    record('390px actual Unity touch: sticker tap and one-finger swipe turn the expected face/layer');
    let before=await snapshot();const box=await page.locator('#unity-canvas').boundingBox(),background={x:box.x+25,y:box.y+25};
    await event('touchStart',[finger(background)]);for(let step=1;step<=5;step++)await event('touchMove',[finger({x:background.x+step*10,y:background.y+step*4})]);await event('touchEnd',[]);
    await page.waitForFunction(old=>JSON.stringify(window.rubikEvidence.latest.camera)!==JSON.stringify(old),before.camera);assert.equal((await snapshot()).serialized,before.serialized);
    before=await snapshot();const left={x:box.x+70,y:box.y+55},right={x:box.x+140,y:box.y+55};
    await event('touchStart',[finger(left,1),finger(right,2)]);
    for(let step=1;step<=5;step++)await event('touchMove',[finger({x:left.x+step*3,y:left.y+step*3},1),finger({x:right.x+step*10,y:right.y+step*3},2)]);
    await event('touchEnd',[]);
    await page.waitForFunction(old=>window.rubikEvidence.latest.camera[2]!==old[2]&&window.rubikEvidence.latest.camera[0]!==old[0],before.camera);assert.equal((await snapshot()).serialized,before.serialized);
    await click('Reset view');await reset();point=await stickerPoint(22);version=(await snapshot()).version;
    await event('touchStart',[finger(point)]);await event('touchEnd',[]);await waitChanged(version);assert.equal((await snapshot()).facelets,effects.F);await auditResting();
    record('One-finger background orbit and two-finger drag/pinch preserve state; a later one-finger tap still works');
    await page.screenshot({path:path.join(artifacts,'unity-web-mobile.png'),fullPage:true});
  }finally{await cdp.send('Emulation.setTouchEmulationEnabled',{enabled:false});await cdp.detach();await page.setViewportSize({width:1440,height:1100});await page.waitForTimeout(200);}
}
async function performanceChecks(fixtures,previousLoad){
  const samples=[];await speed(12);
  evidence.performance={samples,completedSeeds:0,requiredSeeds:fixtures.states.length};
  for(const fixture of fixtures.states){
    await importState(fixture.serialized);assert.equal((await snapshot()).facelets,fixture.facelets);
    await workload('solve');await click('Solve current state');await idle();await workload(null);
    const solvedPlan=await snapshot();assert.equal(solvedPlan.outcome,'Solved');assert.equal(solvedPlan.hasPlan,true);assert.equal(solvedPlan.steps.length,9);assert.equal(solvedPlan.serialized,fixture.serialized);
    assert.ok(solvedPlan.solveMs>0,'Completed browser solve must expose measured wall time');
    assert.ok(solvedPlan.maxSolveSliceMs>0,'Even a one-slice solve must retain its actual slice measurement');
    let before=fixture.serialized;
    for(const step of solvedPlan.steps){assert.equal(step.before,before);assert.equal(step.count,step.setup.length+step.algorithm.length+step.alignment.length);assert.ok(step.recognition&&step.goal&&step.explanation);before=step.after;}
    samples.push({seed:fixture.seed,solveMs:solvedPlan.solveMs,maxSliceMs:solvedPlan.maxSolveSliceMs,moves:solvedPlan.totalMoves,outcome:solvedPlan.outcome});
    // Full solution replay is tested above and in the 100-state independent C# suite.
    // Here each separate browser plan additionally replays its final real phase in Unity.
    await page.locator('#phase-list [data-value="8"]').click();await idle();
    assert.equal((await snapshot()).serialized,solvedPlan.steps[8].before);
    if(!(await snapshot()).solved){await workload('playback');await click('Play');await idle();await workload(null);}
    assert.equal((await snapshot()).solved,true);assert.equal((await snapshot()).cursor,solvedPlan.totalMoves);await auditResting();
    evidence.performance.completedSeeds=samples.length;
    console.log(`PERF seed=${fixture.seed} outcome=${solvedPlan.outcome} solveMs=${solvedPlan.solveMs.toFixed(3)} maxSliceMs=${solvedPlan.maxSolveSliceMs.toFixed(3)} moves=${solvedPlan.totalMoves}`);
  }
  record('100 browser solves from imported history-free seeded snapshots, plus exact final-phase replay for every returned plan');
  await page.waitForTimeout(200); // Include the final Unity frame interval in the next scheduled telemetry publication.
  const observed=await page.evaluate(()=>window.rubikEvidence);
  const unitySolvingFrames=[...previousLoad.unitySolvingFrames,...observed.unitySolvingFrames],unityPlaybackFrames=[...previousLoad.unityPlaybackFrames,...observed.unityPlaybackFrames];
  const peakHeap=Math.max(previousLoad.maxHeapBytes,observed.maxHeapBytes);
  const timings=samples.map(sample=>sample.solveMs);
  const frameSummary=values=>({count:values.length,medianMs:quantile(values,.5),p95Ms:quantile(values,.95),maxMs:Math.max(...values)});
  const downloadManifest=await (await page.request.get(base.replace(/\/$/,'')+'/unity/build-manifest.json')).json();
  await Promise.all(responseReads);
  const downloads=[];
  for(const [key,mime] of [['loaderUrl','application/javascript'],['dataUrl','application/octet-stream'],['frameworkUrl','application/javascript'],['codeUrl','application/wasm']]){
    const url=new URL('unity/'+downloadManifest[key],base.replace(/\/$/,'')+'/').href,response=resourceResponses.get(url);
    assert.ok(response,`${key} was actually downloaded by the browser`);assert.equal(response.status,200);
    assert.equal(response.headers['content-type']?.split(';')[0].trim(),mime,`${key} MIME type`);
    assert.ok(!response.headers['content-encoding'],`${key} is served uncompressed as configured`);
    const bytes=Number(response.headers['content-length']);assert.ok(Number.isInteger(bytes)&&bytes>0,`${key} has a real nonzero byte length`);
    downloads.push({kind:key,url,bytes,mime});
  }
  evidence.performance={browserRun:'Actual Unity Web player',seeds:samples.map(sample=>sample.seed),scrambleLength:25,warmSolve:{count:samples.length,medianMs:quantile(timings,.5),p95Ms:quantile(timings,.95),maxMs:Math.max(...timings)},
    frameScope:'Raw Unity workload intervals across both page loads, including cold solving and excluding idle frames',
    unitySolvingFrames:frameSummary(unitySolvingFrames),unityPlaybackFrames:frameSummary(unityPlaybackFrames),
    browserRafDuringSolve:frameSummary([...previousLoad.solvingFrames,...observed.solvingFrames]),browserRafDuringPlayback:frameSummary([...previousLoad.playbackFrames,...observed.playbackFrames]),
    maxSolveSliceMs:Math.max(previousLoad.maxSolveSliceMs,observed.maxSolveSliceMs),wasmHeapBytes:peakHeap,
    playerDownloadBytes:downloads.reduce((sum,file)=>sum+file.bytes,0),downloads,samples,viewport:'1440x1100',deviceScaleFactor:1,
    targets:{frameP95Ms:33,warmSolveP95Ms:10000,heapBytes:512*1024*1024,uncompressedPlayerBytes:40*1024*1024}};
  assert.ok(unitySolvingFrames.length>=20,'Enough actual Unity solving frames were sampled');assert.ok(unityPlaybackFrames.length>=60,'Enough actual Unity playback frames were sampled');
  assert.ok(evidence.performance.unitySolvingFrames.p95Ms<33,`Unity solving frame p95 ${evidence.performance.unitySolvingFrames.p95Ms} ms must be below 33 ms`);
  assert.ok(evidence.performance.unityPlaybackFrames.p95Ms<33,`Unity playback frame p95 ${evidence.performance.unityPlaybackFrames.p95Ms} ms must be below 33 ms`);
  assert.ok(evidence.performance.warmSolve.p95Ms<10000,'Warm solve p95 must be below 10 s');
  assert.ok(peakHeap>0&&peakHeap<=512*1024*1024,'Measured WebAssembly heap must be within 512 MiB');
  assert.ok(evidence.performance.playerDownloadBytes<40*1024*1024,'Actual uncompressed player download must be below 40 MiB');
  record('Actual Unity workload frame/solve median and p95, allocated WebAssembly heap and downloaded player bytes satisfy documented targets; MIME/compression headers verified');
}
try{
  if(!shellOnly)await verifyBuildSource();
  await page.goto(base,{waitUntil:'networkidle'});
  assert.equal(await page.title(),'RubikSim — learn the next move');
  assert.equal(await page.locator('.sticker').count(),54);
  assert.equal(await page.locator('#face-controls button').count(),18);
  record('Accessible HTML page, 54-sticker editor and 18 explicit face-turn controls');
  if(shellOnly){
    if(!hasBuild){await page.getByText('Unity build needed',{exact:true}).waitFor();assert.equal(await page.getByRole('button',{name:'Solve current state',exact:true}).isDisabled(),true);assert.equal(await page.getByRole('button',{name:'Scramble',exact:true}).isDisabled(),true);record('Missing Unity build is explicit; cube and solver controls stay disabled');}
    await page.getByRole('button',{name:'Paint red (R)',exact:true}).click();await page.getByRole('button',{name:'U face, row 1, column 1, white (U)',exact:true}).click();assert.equal(await page.getByRole('button',{name:'U face, row 1, column 1, red (R)',exact:true}).count(),1);record('Face editor palette and color-independent labels update');
    await page.screenshot({path:path.join(artifacts,'website-shell-desktop.png'),fullPage:true});
    await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),true);await page.screenshot({path:path.join(artifacts,'website-shell-mobile.png'),fullPage:true});record('390px layout has no horizontal overflow; actual desktop/mobile screenshots captured');
    assert.deepEqual(errors,[]);record('No browser JavaScript errors');
    evidence.notRun=['Unity Editor compilation','Unity rendering','Unity Web build','Live cube controls/playback/state-view agreement/performance'];
  }else{
    const fixtureText=await readFile(path.join(root,'tools/browser-check/fixtures/seeded-states.json'),'utf8'),fixtures=JSON.parse(fixtureText);
    assert.equal(fixtures.states.length,100);assert.deepEqual(fixtures.states.map(s=>s.seed),Array.from({length:100},(_,i)=>i));
    evidence.fixture={path:'tools/browser-check/fixtures/seeded-states.json',sha256:createHash('sha256').update(fixtureText).digest('hex'),generator:fixtures.generator,generatedUtc:fixtures.generatedUtc};
    await idle();evidence.unityChecksRun=true;assert.equal((await snapshot()).solved,true);assert.equal((await snapshot()).viewAgrees,true);record('Unity loads, starts solved, and actual rendered sticker transforms match logical state');
    await speed(12);
    await notation('R');await idle();const rstate=(await snapshot()).facelets;
    assert.equal(rstate,'UUFUUFUUF'+'RRRRRRRRR'+'FFDFFDFFD'+'DDBDDBDDB'+'LLLLLLLLL'+'UBBUBBUBB');record('R turn matches independently specified sticker bands, including animated transform audit');
    await notation('U invalid F');await page.getByRole('alert').waitFor();assert.equal((await snapshot()).facelets,rstate);record('Malformed notation leaves state unchanged');
    await click('Undo');await idle();assert.equal((await snapshot()).solved,true);await click('Redo');await idle();assert.equal((await snapshot()).facelets,rstate);record('Undo/redo restore exact snapshots');
    await click('Reset cube');await idle();const keyboardVersion=(await snapshot()).version;await page.locator('#unity-canvas').focus();await page.keyboard.press('r');
    await page.waitForFunction(version=>window.rubikEvidence.latest.version>version,keyboardVersion);await idle();assert.equal((await snapshot()).facelets,rstate);record('Keyboard face moves agree with on-screen notation');
    const canvas=await page.locator('#unity-canvas').boundingBox();const beforeCamera=(await snapshot()).camera;
    await page.mouse.move(canvas.x+35,canvas.y+35);await page.mouse.down({button:'right'});await page.mouse.move(canvas.x+95,canvas.y+75,{steps:10});await page.mouse.up({button:'right'});
    await page.waitForFunction(old=>JSON.stringify(window.rubikEvidence.latest.camera)!==JSON.stringify(old),beforeCamera);assert.equal((await snapshot()).facelets,rstate);await click('Reset view');record('Camera orbit and reset preserve puzzle state');
    for(const face of 'URFDLB')for(const suffix of ['',"'",'2']){
      await reset();await click(`Turn ${face}${suffix==='2'?' twice':suffix?' counterclockwise':' clockwise'}`);await idle();assert.equal((await snapshot()).facelets,fixtures.knownMoveEffects[face+suffix]);await auditResting();
    }
    record('All 18 accessible face-turn buttons execute the correct face and suffix in the real player');
    await pointerChecks(fixtures.knownMoveEffects);
    await touchChecks(fixtures.knownMoveEffects);
    await click('Reset cube');await idle();await page.getByLabel('Seed',{exact:true}).fill('42');await click('Scramble');await idle();const scramble=(await snapshot()).serialized;assert.equal((await snapshot()).solved,false);
    await click('Solve current state');await page.getByRole('button',{name:'Cancel solve',exact:true}).click();await idle();assert.equal((await snapshot()).hasPlan,false);assert.equal((await snapshot()).outcome,'Cancelled');record('Cold table solving remains cancellable and exposes no partial plan');
    await click('Export state');assert.equal(await page.getByLabel('Versioned state snapshot').inputValue(),scramble);
    await click('Reset cube');await idle();await page.getByLabel('Versioned state snapshot').fill(scramble);await click('Validate & import');await idle();assert.equal((await snapshot()).serialized,scramble);record('State export/import retains a history-free scrambled snapshot');
    await workload('solve');await click('Solve current state');await idle();await workload(null);let s=await snapshot();assert.equal(s.hasPlan,true);assert.equal(s.steps.length,9);assert.ok(s.totalMoves>0);evidence.coldSolve={seed:42,solveMs:s.solveMs,maxSliceMs:s.maxSolveSliceMs,tableWasPreviouslyCancelled:true};record('History-free current-state CFOP plan exposes nine verified teaching boundaries');
    await speed(1);const slowStart=await page.evaluate(()=>performance.now());await click('Next move / hint');await page.waitForFunction(()=>window.rubikEvidence.latest.animating);assert.equal(await page.locator('#move-list .current').getAttribute('data-move'),'0');await idle();const slowMoveMs=await page.evaluate(start=>performance.now()-start,slowStart);await click('Previous move');await idle();assert.equal((await snapshot()).serialized,scramble);record('Forward/backward playback and current-move highlighting agree with the animation');
    await speed(6);const fastStart=await page.evaluate(()=>performance.now());await click('Next move / hint');await idle();const fastMoveMs=await page.evaluate(start=>performance.now()-start,fastStart);assert.equal((await snapshot()).cursor,1);await click('Previous move');await idle();assert.equal((await snapshot()).serialized,scramble);
    evidence.playbackSpeed={slowMovesPerSecond:1,slowMoveMs,fastMovesPerSecond:6,fastMoveMs};assert.ok(slowMoveMs>700&&slowMoveMs>fastMoveMs*2,`Speed must change real turn duration: slow=${slowMoveMs}ms fast=${fastMoveMs}ms`);assert.ok(fastMoveMs<700,'Six moves/s must produce a visibly faster turn');await speed(1);record('Speed control changes measured actual single-turn duration while inverse playback restores the exact source');
    await page.locator('#phase-list [data-value="3"]').click();await idle();s=await snapshot();assert.equal(s.serialized,s.steps[3].before);await page.locator('#phase-list [data-value="0"]').click();await idle();record('Jump-to-phase restores the documented before-state');
    await click('Play');await page.waitForFunction(()=>window.rubikEvidence.latest.animating);await click('Pause');await idle();const pausedCursor=(await snapshot()).cursor;await page.waitForTimeout(1200);assert.equal((await snapshot()).cursor,pausedCursor);record('Pause lets the committed turn settle and stops subsequent playback');
    await speed(12);await workload('playback');await click('Play');await idle();await workload(null);assert.equal((await snapshot()).solved,true);assert.equal((await snapshot()).cursor,(await snapshot()).totalMoves);record('Automatic playback waits through every turn and reaches the actual solved condition');
    await notation('R');await idle();assert.equal((await snapshot()).hasPlan,false);record('User moves invalidate an existing tutorial plan');
    await page.getByText('Practice a last-layer case',{exact:true}).click();await page.getByLabel('Case',{exact:true}).selectOption('sune');await click('Load practice case');await idle();assert.equal((await snapshot()).solved,false);record('Local practice loads a supported nontrivial OLL case');
    await click('Solve current state');await idle();
    for(let index=0;index<6;index++){
      assert.equal((await snapshot()).steps[index].count,0);
      await page.locator(`#phase-list [data-value="${index}"]`).click();await idle();
      const selected=await snapshot();assert.equal(selected.activeStep,index);assert.equal(selected.cursor,0);
      assert.equal(await page.locator('#step-goal').textContent(),selected.steps[index].goal);
    }
    record('Zero-move phase jumps retain the explicitly selected explanation and highlights');
    await click('Copy current cube');await click('Validate & load colors');await idle();const validState=(await snapshot()).serialized;
    await page.getByRole('button',{name:'Paint red (R)',exact:true}).click();await page.locator('.sticker[data-index="4"]').click();await click('Validate & load colors');await page.getByRole('alert').waitFor();assert.equal((await snapshot()).serialized,validState);record('Face editor accepts a valid state and rejects an impossible edit atomically');
    const beforeReload=await page.evaluate(()=>window.rubikEvidence);
    assert.deepEqual(beforeReload.disagreements,[]);
    await page.getByLabel('Remember my cube on this device').check();await page.reload({waitUntil:'networkidle'});
    await page.waitForFunction(expected=>window.rubikEvidence.latest?.serialized===expected,validState,{timeout:120000});await idle();assert.equal((await snapshot()).serialized,validState);record('Local persistence survives Unity startup without overwriting the saved state');
    await notation('R U F D L B R U F D L B');await page.waitForFunction(()=>window.rubikEvidence.latest.animating);await click('Reset cube');await idle();assert.equal((await snapshot()).solved,true);record('Reset during queued animation cancels pending turns and restores an exact resting cube');
    await performanceChecks(fixtures,beforeReload);
    const observed=await page.evaluate(()=>window.rubikEvidence);assert.deepEqual(observed.disagreements,[]);assert.deepEqual(errors,[]);record('All observed animation endpoints and resting views agree; no browser JavaScript errors');
    assert.deepEqual(consoleErrors,[]);record('No browser or Unity console errors during real-player verification');
    evidence.performance.maxSolveSliceMs=Math.max(beforeReload.maxSolveSliceMs,observed.maxSolveSliceMs);
    evidence.performance.wasmHeapBytes=Math.max(beforeReload.maxHeapBytes,observed.maxHeapBytes);
    await page.screenshot({path:path.join(artifacts,'unity-web-desktop.png'),fullPage:true});
  }
  evidence.result='passed';
  await writeFile(path.join(artifacts,shellOnly?'browser-shell-results.json':'browser-unity-results.json'),JSON.stringify(evidence,null,2));
  console.log(JSON.stringify(evidence,null,2));
}catch(error){evidence.result='failed';evidence.failure={message:error.message,stack:error.stack};evidence.browserErrors=errors;evidence.consoleErrors=consoleErrors;await page.screenshot({path:path.join(artifacts,'browser-failure.png'),fullPage:true}).catch(()=>{});await writeFile(path.join(artifacts,shellOnly?'browser-shell-results.json':'browser-unity-results.json'),JSON.stringify(evidence,null,2));console.error(error);process.exitCode=1;}
finally{await browser.close();}
function quantile(values,p){if(values.length===0)return null;const sorted=[...values].sort((a,b)=>a-b);return sorted[Math.min(sorted.length-1,Math.max(0,Math.ceil(sorted.length*p)-1))];}
