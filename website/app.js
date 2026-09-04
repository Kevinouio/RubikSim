const $ = selector => document.querySelector(selector);
const colors = {U:'#f0efe6',R:'#ec7171',F:'#68bd98',D:'#f1cc64',L:'#f3a16b',B:'#83ace3'};
const names = {U:'white',R:'red',F:'green',D:'yellow',L:'orange',B:'blue'};
const faces = 'URFDLB';
let unity, state, selectedColor='U', editor = [...faces].map(f=>f.repeat(9)).join('').split('');
let planKey='', restoringState=false, failedRestoreVersion=null;
$('#unity-canvas').addEventListener('contextmenu',event=>event.preventDefault());
function error(message) { $('#error').textContent=message; $('#error').hidden=!message; }
function command(action,value='') {
  error('');
  if (!unity) { error('The Unity build must be loaded before using the cube.'); return; }
  unity.SendMessage('RubikBridge','SendCommand',JSON.stringify({action,value:String(value)}));
}
function refreshEditor() {
  document.querySelectorAll('.sticker').forEach(button=>{
    const i=Number(button.dataset.index),color=editor[i];
    button.style.background=colors[color]; button.textContent=color;
    button.setAttribute('aria-label',`${faces[Math.floor(i/9)]} face, row ${Math.floor(i%9/3)+1}, column ${i%3+1}, ${names[color]} (${color})`);
  });
}
for (const face of faces) {
  const group=document.createElement('div');group.style.setProperty('--face-color',colors[face]);
  for (const suffix of ['',"'",'2']) { const b=document.createElement('button');b.textContent=face+suffix;b.disabled=true;b.dataset.command='notation';b.dataset.value=face+suffix;b.setAttribute('aria-label',`Turn ${face}${suffix==='2'?' twice':suffix?' counterclockwise':' clockwise'}`);group.append(b); }
  $('#face-controls').append(group);
  const swatch=document.createElement('button');swatch.textContent=face;swatch.style.background=colors[face];swatch.setAttribute('aria-label',`Paint ${names[face]} (${face})`);swatch.setAttribute('aria-pressed',face===selectedColor);swatch.onclick=()=>{selectedColor=face;$('#palette').querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',b===swatch));};$('#palette').append(swatch);
  const faceGrid=document.createElement('div');faceGrid.className='editor-face';const caption=document.createElement('span');caption.className='caption';caption.textContent=`${face} / ${names[face]}`;faceGrid.append(caption);
  for(let cell=0;cell<9;cell++){const b=document.createElement('button');b.className='sticker';b.dataset.index=faces.indexOf(face)*9+cell;b.onclick=()=>{editor[Number(b.dataset.index)]=selectedColor;refreshEditor();};faceGrid.append(b);}$('#face-editor').append(faceGrid);
}
refreshEditor();
document.addEventListener('click',event=>{
  const button=event.target.closest('[data-command]');if(!button||button.disabled)return;
  const action=button.dataset.command;let value=button.dataset.value||'';
  if(action==='export'){$('#state-data').value=state.serialized;return;}
  if(action==='import')value=$('#state-data').value;
  if(action==='scramble')value=$('#seed').value;
  if(action==='practice')value=$('#practice-case').value;
  command(action,value);
});
$('#notation-form').addEventListener('submit',e=>{e.preventDefault();command('notation',$('#notation').value.replaceAll('′',"'"));});
$('#speed').addEventListener('input',()=>{$('#speed-label').textContent=`${$('#speed').value} moves/s`;command('speed',$('#speed').value);});
$('#read-facelets').onclick=()=>{editor=state.facelets.split('');refreshEditor();};
$('#apply-facelets').onclick=()=>command('import',JSON.stringify({schemaVersion:1,puzzle:'cube-3x3',definitionVersion:1,facelets:editor.join('')}));
$('#download-state').onclick=()=>{const blob=new Blob([$('#state-data').value||state.serialized],{type:'application/json'}),url=URL.createObjectURL(blob),link=document.createElement('a');link.href=url;link.download='rubiksim-cube.json';link.click();URL.revokeObjectURL(url);};
try{$('#remember').checked=localStorage.getItem('rubiksim.remember')==='true';}catch{}
$('#remember').onchange=()=>{try{localStorage.setItem('rubiksim.remember',String($('#remember').checked));if(!$('#remember').checked)localStorage.removeItem('rubiksim.state');else if(state)localStorage.setItem('rubiksim.state',state.serialized);}catch{error('Local storage is unavailable in this browser. Export your state to keep it.');}};
function updatePlan(s){
  const key=JSON.stringify([s.moves,(s.steps||[]).map(step=>[step.phase,step.caseId,step.before,step.after])]);
  if(planKey!==key){planKey=key;$('#phase-list').replaceChildren();$('#move-list').replaceChildren();
    (s.steps||[]).forEach((step,index)=>{const b=document.createElement('button');b.dataset.command='jump';b.dataset.value=index;b.textContent=step.phase;b.title=step.goal;$('#phase-list').append(b);});
    (s.moves||[]).forEach((move,index)=>{const span=document.createElement('span');span.className='move';span.textContent=move;span.dataset.move=index;$('#move-list').append(span);});
  }
  $('#phase-list').querySelectorAll('button').forEach((b,i)=>{b.classList.toggle('active',i===s.activeStep);b.disabled=!s.hasPlan||s.solving;});
  const activeMove=s.animating?s.cursor-1:s.cursor;
  $('#move-list').querySelectorAll('.move').forEach((b,i)=>{b.classList.toggle('done',i<activeMove);b.classList.toggle('current',i===activeMove);if(i===activeMove)b.setAttribute('aria-current','step');else b.removeAttribute('aria-current');});
  const step=(s.steps||[])[s.activeStep];
  $('#algorithm-parts').hidden=!step;
  if(step){for(const [part,id] of [['setup','setup-moves'],['algorithm','algorithm-moves'],['alignment','alignment-moves']])$('#'+id).textContent=step[part].join(' ')||'None needed';}
  if(step){$('#case-label').textContent=`${step.phase} / ${step.caseId}`;$('#step-goal').textContent=step.goal;$('#recognition').textContent=step.recognition;$('#explanation').textContent=step.explanation;$('#orientation').textContent=step.orientation;const validSource=/^https:\/\//.test(step.source);$('#source').hidden=!validSource;if(validSource){$('#source').href=step.source;$('#source').textContent='Method / algorithm source ↗';}}
  else{$('#case-label').textContent='YOUR NEXT STEP';$('#step-goal').textContent=s.solved?'A solved cube. A fresh start.':'Find your next move.';$('#recognition').textContent=s.solved?'Try a scramble or load a practice case.':'Solve the current state to see recognition cues and a verified move sequence.';$('#explanation').textContent='CFOP: aligned cross → four corner/edge pairs → OLL → PLL.';$('#orientation').textContent='U white, R red and F green in the home orientation. Camera orbit does not change face notation.';$('#source').hidden=false;$('#source').href='https://jperm.net/3x3/cfop';}
}
window.addEventListener('rubik-state',event=>{
  const s=typeof event.detail==='string'?JSON.parse(event.detail):event.detail;state=s;
  if(failedRestoreVersion!==null&&s.version>failedRestoreVersion&&!s.error){restoringState=false;failedRestoreVersion=null;}
  $('#loading').hidden=true;$('#connection').textContent=s.solving?'Finding your solution':s.solved?'Cube solved':'Cube connected';$('#status').textContent=s.solving?s.solverProgress:s.status;error(s.error||'');
  document.querySelectorAll('[data-command]').forEach(b=>{const a=b.dataset.command;b.disabled=!s.ready;
    if(a==='undo')b.disabled=!s.canUndo;if(a==='redo')b.disabled=!s.canRedo;
    if(['play','pause','next','previous','jump'].includes(a))b.disabled=!s.hasPlan||s.solving;
    if(a==='previous')b.disabled=b.disabled||s.cursor===0;if(a==='next'||a==='play')b.disabled=b.disabled||s.cursor>=s.totalMoves;
    if(a==='solve')b.disabled=s.solving||s.animating||s.pending;if(a==='pause')b.disabled=!s.playing;
    if(a==='cancel'){b.hidden=!s.solving;b.disabled=!s.solving;}
  });
  for(const id of ['apply-notation','read-facelets','apply-facelets','download-state','speed'])$('#'+id).disabled=!s.ready;
  $('#move-count').textContent=`${s.cursor} / ${s.totalMoves}`;$('#speed').value=s.speed;$('#speed-label').textContent=`${s.speed} moves/s`;
  $('#state-audit').textContent=!s.animationAgrees?'State/view agreement: animation MISMATCH — reload and report this failure':s.animating?'State/view agreement: turn in progress':`State/view agreement: ${s.viewAgrees?'matches':'MISMATCH'} · state version ${s.version}`;
  updatePlan(s);
  if($('#remember').checked&&!s.animating&&!restoringState){try{localStorage.setItem('rubiksim.state',s.serialized);}catch{}}
});
async function boot(){
  try{
    let saved=null;try{if($('#remember').checked)saved=localStorage.getItem('rubiksim.state');}catch{}
    restoringState=Boolean(saved);
    const response=await fetch('unity/build-manifest.json',{cache:'no-store'});
    if(!response.ok)throw new Error('No Unity Web build is present. Build this project with Unity 6000.0.68f1 and Web Build Support, then reload this page. See README.md for the exact build command.');
    const config=await response.json();for(const key of ['loaderUrl','dataUrl','frameworkUrl','codeUrl'])config[key]='unity/'+config[key];const loader=document.createElement('script');loader.src=config.loaderUrl;await new Promise((resolve,reject)=>{loader.onload=resolve;loader.onerror=()=>reject(new Error('The Unity loader could not be downloaded. Rebuild and check the server paths.'));document.head.append(loader);});
    unity=await window.createUnityInstance($('#unity-canvas'),config,progress=>{$('#load-progress').value=progress;$('#loading-message').textContent=`Loading Unity · ${Math.round(progress*100)}%`;});
    command('snapshot');
    if(saved){$('#state-data').value=saved;command('import',saved);}
    if(saved&&state?.error){failedRestoreVersion=state.version;restoringState=true;}
    else restoringState=false;
  }catch(e){$('#loading strong').textContent='Unity build needed';$('#loading-message').textContent=e.message;$('#load-progress').hidden=true;$('#connection').textContent='Unity unavailable';$('#status').textContent='Cube controls and the tutor will become available when the Unity Web build is loaded.';}
}
boot();
