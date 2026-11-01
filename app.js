'use strict';

const $=s=>document.querySelector(s), esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));

let state={loaded:false}, tab='Player', selection='overview', advanced=false, serial=0, jobs=new Map(), pending=new Map(), queue=Promise.resolve(), toastTimer;

function rpc(action,params={}){return new Promise((resolve,reject)=>{const id=++serial;jobs.set(id,{resolve,reject});window.chrome.webview.postMessage({id,action,...params});});}

window.chrome.webview.addEventListener('message',async e=>{const m=e.data;if(m.event==='closeRequested'){try{await flush();await rpc('close');}catch(err){notify(err.message,true);await rpc('cancelClose');}return;}const job=jobs.get(m.id);if(!job)return;jobs.delete(m.id);m.ok?job.resolve(m.data):job.reject(Error(m.error));});

function notify(text,error=false){const el=$('#toast');el.textContent=text;el.classList.toggle('error',error);el.hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>el.hidden=true,error?9000:4500);}

function entity(id){return state.entities?.find(e=>e.id===id);}

function savedValue(id,name){const f=state.fields.find(f=>f.entity===id&&f.name===name);return f?(pending.has(f.id)?pending.get(f.id):f.value):undefined;}

function recordLabel(e){if(e.role==='Garrison'){const region=entity(Number(savedValue(e.id,'RegionId')));const castles=region?territoryCastles(region.id):[];const home=castles.some(c=>Number(savedValue(e.owner,'HomeCastleId'))===c.id);return (region?.name||e.name)+(home?' · Home castle garrison':castles.length?' · Castle garrison':' · Garrison');}if(!['Castle','Garrison'].includes(e.role))return e.role==='Territory'?e.name:e.role;const region=entity(Number(savedValue(e.id,'RegionId')));const home=Number(savedValue(e.owner,'HomeCastleId'))===e.id;return (region?.name||e.name)+(home?' · Home castle':' · Castle');}

function territoryCastles(id){return state.entities.filter(e=>e.role==='Castle'&&Number(savedValue(e.id,'RegionId'))===id);}

function territoryHoldings(id){return state.entities.filter(e=>['Castle','Garrison'].includes(e.role)&&Number(savedValue(e.id,'RegionId'))===id);}

function territoryLabel(e){return e.name+(territoryCastles(e.id).some(c=>Number(savedValue(e.owner,'HomeCastleId'))===c.id)?' · Home':'');}

function category(f){return entity(f.entity)?.category||'Campaign';}

function readable(name){const aliases={NameWithTitle:'Full name',GoldGain:'Income per turn',ColorComponent:'Banner color',MovesFromStartGame:'Turns elapsed',RobinHoodHelpsLeft:'Robin Hood helps remaining',MovesWithoutTournament:'Turns since tournament',MovesWithoutKidnapping:'Turns since kidnapping',LastGameEntityId:'Last entity ID'};return aliases[name]||name.replace(/([a-z])([A-Z])/g,'$1 $2');}

function changes(){return state.fields?.filter(f=>f.changed)||[];}

function valueLabel(f,v){return f.options?.[v]??v;}

function describe(f){const e=entity(f.entity);if(!e)return 'Campaign';const owner=entity(e.owner);return (e.category==='Opponents'&&owner?owner.name+' / ':'')+recordLabel(e);}

function indicators(){const ids=new Set(changes().map(f=>f.id));for(const [id,v] of pending){const f=state.fields[id];v!==f.original?ids.add(id):ids.delete(id);}const n=ids.size;$('#changeCount').textContent=n;$('#changeCount').hidden=n===0;$('#saveStatus').textContent=n?`${n} unsaved change${n===1?'':'s'}`:'All changes saved';$('#saveStatus').style.color=n?'var(--gold)':'var(--green)';$('#save').disabled=!state.loaded||n===0;$('#undo').disabled=!state.canUndo&&pending.size===0;}

function markField(id,error=''){const f=state.fields[id],wrap=document.querySelector(`[data-field="${id}"]`);if(!wrap)return;wrap.classList.toggle('changed',pending.has(id)?pending.get(id)!==f.original:f.changed);const input=wrap.querySelector('input,select');input?.classList.toggle('invalid',!!error);wrap.querySelector('.field-error').textContent=error;}

function flush(){queue=queue.catch(()=>{}).then(async()=>{for(const [id,value] of [...pending]){try{state=await rpc('set',{field:id,value,advanced});if(pending.get(id)===value)pending.delete(id);markField(id);indicators();}catch(err){markField(id,err.message);throw err;}}});return queue;}

async function navigate(nextTab,nextSelection='overview'){try{await flush();tab=nextTab;selection=nextSelection;$('#search').value='';render();}catch(err){notify(err.message,true);}}

async function action(name){if(name==='find'){await showPicker();return;}try{await flush();const result=await rpc(name);if(result?.state)state=result.state;else if(result?.loaded!==undefined)state=result;if(['open','find'].includes(name)&&!result?.cancelled){tab='Player';selection='overview';pending.clear();}if(result?.message)notify(result.message);$('#more').open=false;render();}catch(err){notify(err.message,true);}}

$('#pick').onclick=showPicker;$('#open').onclick=()=>action('open');$('#save').onclick=()=>action('save');$('#undo').onclick=()=>action('undo');document.querySelectorAll('[data-action]').forEach(b=>b.onclick=()=>action(b.dataset.action));document.querySelectorAll('[data-tab]').forEach(b=>b.onclick=()=>navigate(b.dataset.tab));$('#help').onclick=()=>{$('#more').open=false;$('#helpDialog').showModal();};$('#closeHelp').onclick=()=>$('#helpDialog').close();

$('#advanced').onchange=async e=>{try{await flush();advanced=e.target.checked;renderEditor();}catch(err){e.target.checked=advanced;notify(err.message,true);}};$('#search').oninput=async()=>{try{await flush();renderEditor();}catch(err){notify(err.message,true);}};

function navButton(label,key,extra=''){return `<button class="nav-button ${selection===String(key)?'active':''} ${extra}" data-select="${esc(key)}">${esc(label)}</button>`;}

function territoryNav(records){const lands=records.filter(e=>e.role==='Territory');return lands.length?'<details class="lands" open><summary>Territories · '+lands.length+'</summary>'+lands.sort((a,b)=>a.name.localeCompare(b.name)).map(e=>navButton(territoryLabel(e),e.id,'territory')).join('')+'</details>':'';}

function roleOrder(e){return ['Character','Treasury','Army','Castle','Tournament','Territory','Campaign'].indexOf(e.role);}

function renderSidebar(){const side=$('#sidebar');if(tab==='Changes'){side.innerHTML='<div class="nav-group">Review your edits</div>'+navButton('All changes','overview')+['Player','Opponents','Campaign'].map(c=>navButton(`${c} · ${changes().filter(f=>category(f)===c).length}`,c)).join('');}

else if(tab==='Player'){const es=state.entities.filter(e=>e.category==='Player');side.innerHTML=navButton('Overview','overview')+'<div class="nav-separator"></div>'+es.filter(e=>!['Territory','Castle','Garrison'].includes(e.role)).sort((a,b)=>roleOrder(a)-roleOrder(b)).map(e=>navButton(recordLabel(e),e.id)).join('')+territoryNav(es);}

else if(tab==='Opponents'){const lords=state.entities.filter(e=>e.category==='Opponents'&&e.role==='Character').sort((a,b)=>a.name.localeCompare(b.name));if(selection==='overview'&&lords.length)selection='lord:'+lords[0].id;side.innerHTML=lords.map(lord=>{const es=state.entities.filter(e=>e.owner===lord.id);const open=selection==='lord:'+lord.id||es.some(e=>String(e.id)===selection);return `<details class="opponent" ${open?'open':''}><summary>${esc(lord.name)}</summary>${navButton('Overview','lord:'+lord.id)}${es.filter(e=>!['Territory','Castle','Garrison'].includes(e.role)).sort((a,b)=>roleOrder(a)-roleOrder(b)).map(e=>navButton(e.role,e.id)).join('')}${territoryNav(es)}</details>`;}).join('');const other=state.entities.filter(e=>e.category==='Opponents'&&e.owner<0&&!['Castle','Garrison'].includes(e.role));if(other.length)side.innerHTML+='<div class="nav-group">Unassigned holdings</div>'+other.map(e=>navButton(e.name,e.id)).join('');}

else{side.innerHTML=navButton('Campaign overview','overview')+navButton('Shared state','shared')+state.entities.filter(e=>e.category==='Campaign').map(e=>navButton(campaignName(e),e.id)).join('');}

side.querySelectorAll('[data-select]').forEach(b=>b.onclick=()=>navigate(tab,b.dataset.select));}

function campaignName(e){const fields=state.fields.filter(f=>f.entity===e.id&&f.editable&&!f.advanced);return fields.length===1?readable(fields[0].name):fields.some(f=>f.name==='Wins')?'Battle record':e.name;}

function scope(){if(selection==='shared')return state.fields.filter(f=>f.entity===-1);if(/^\d+$/.test(selection)){const id=Number(selection);const ids=new Set([id,...(entity(id)?.role==='Territory'?territoryHoldings(id).map(e=>e.id):[])]);return state.fields.filter(f=>ids.has(f.entity));}if(selection.startsWith('lord:')){const id=Number(selection.slice(5));return state.fields.filter(f=>entity(f.entity)?.owner===id);}return state.fields.filter(f=>category(f)===tab);}

function fieldVisible(f){if(advanced)return true;if(!f.editable||f.advanced)return false;return !f.name.includes('/')&&!f.name.includes('+');}

function fieldHTML(f){const v=pending.has(f.id)?pending.get(f.id):f.value;const id='field-'+f.id;let control;const readonly=!f.editable||(f.advanced&&!advanced);

if(f.options){const opts={...f.options};if(!(v in opts))opts[v]=v;control=`<select id="${id}" ${readonly?'disabled':''}>`+Object.entries(opts).map(([key,label])=>`<option value="${esc(key)}" ${key===v?'selected':''}>${esc(label)}</option>`).join('')+'</select>';}

else if(f.kind==='Boolean')control=`<select id="${id}">${['True','False'].map(x=>`<option ${v===x?'selected':''}>${x}</option>`).join('')}</select>`;

else control=`<input id="${id}" type="text" ${/Int|Byte|Single|Double|Decimal/.test(f.kind)?'inputmode="decimal"':''} value="${esc(v)}" ${readonly?'disabled':''} autocomplete="off" spellcheck="false">`;

return `<div class="field ${f.changed?'changed':''}" data-field="${f.id}"><label for="${id}" ${advanced?`title="${esc(f.path)}"`:''}>${esc(readable(f.name))}</label>${control}<div class="field-error" role="alert"></div></div>`;}

function titleForSelection(){if(tab==='Changes')return 'Unsaved changes';if(selection.startsWith('lord:'))return entity(Number(selection.slice(5)))?.name||'Opponent';if(/^\d+$/.test(selection)){const e=entity(Number(selection));return e.category==='Opponents'?`${entity(e.owner)?.name||''} · ${recordLabel(e)}`:e.role==='Campaign'?campaignName(e):recordLabel(e);}return tab==='Campaign'?'Campaign settings':'Overview';}

function renderChanges(){const query=$('#search').value.toLowerCase();let fs=changes().filter(f=>(selection==='overview'||category(f)===selection)&&(`${describe(f)} ${f.name} ${f.value} ${f.original}`).toLowerCase().includes(query));if(!fs.length){$('#editor').innerHTML=`<div class="empty"><h3>${changes().length?'No matching changes':'Nothing to review yet'}</h3><p>${changes().length?'Choose another category or clear the search.':'Your edits will appear here, grouped by Player, Opponents and Campaign.'}</p></div>`;return;}$('#editor').innerHTML=['Player','Opponents','Campaign'].map(c=>{const list=fs.filter(f=>category(f)===c);if(!list.length)return '';return `<section class="section-card"><div class="card-heading"><h3>${c}</h3><small>${list.length} change${list.length===1?'':'s'}</small></div>${list.map(f=>`<div class="change-row"><div>${esc(readable(f.name))}<small>${esc(describe(f))}</small></div><div class="change-before">${esc(valueLabel(f,f.original))}</div><span class="arrow">→</span><div class="change-after">${esc(valueLabel(f,f.value))}</div></div>`).join('')}</section>`;}).join('');}

function renderEditor(){if(!state.loaded)return;$('#sectionEyebrow').textContent=tab;$('#sectionTitle').textContent=titleForSelection();$('#sectionHint').textContent=tab==='Changes'?'Every unsaved edit, in one place.':advanced?'Advanced fields can affect game relationships.':'Edit a value, then save when you’re ready.';$('#advanced').disabled=tab==='Changes';if(tab==='Changes'){renderChanges();return;}const query=$('#search').value.trim().toLowerCase();let fs=scope().filter(fieldVisible).filter(f=>`${f.name} ${f.group} ${f.path} ${describe(f)}`.toLowerCase().includes(query));

if(!advanced&&(selection==='overview'||selection.startsWith('lord:'))&&!query)fs=fs.filter(f=>!['Territory','Castle','Garrison','Tournament'].includes(entity(f.entity)?.role));

const grouped=new Map();for(const f of fs){const e=entity(f.entity);const key=e?e.id:-1;if(!grouped.has(key))grouped.set(key,[]);grouped.get(key).push(f);}const priorities=['Gold','Fame','Soldiers','Knights','Catapults','Leadership','Jousting','Swordplay','Name','NameWithTitle'];const sorted=[...grouped].sort((a,b)=>{const rank=id=>({Treasury:0,Army:1,Castle:2,Garrison:2,Character:3,Territory:4,Tournament:5,Campaign:6}[entity(id)?.role]??6);if(entity(Number(selection))?.role==='Territory'){if(a[0]===Number(selection))return -1;if(b[0]===Number(selection))return 1;}return rank(a[0])-rank(b[0]);});

$('#editor').innerHTML=sorted.map(([id,fields])=>{const e=entity(id);const title=e?(e.role==='Territory'?e.name:e.role==='Campaign'?campaignName(e):recordLabel(e)):'Shared campaign state';fields.sort((a,b)=>{const rank=f=>priorities.indexOf(f.name)<0?99:priorities.indexOf(f.name);return rank(a)-rank(b);});return `<section class="section-card ${advanced?'raw':''}"><div class="card-heading"><h3>${esc(title)}</h3><small>${e?.role==='Castle'?'Garrison':e?.role==='Army'?'Field army':''}</small></div><div class="fields">${fields.map(fieldHTML).join('')}</div></section>`;}).join('')||'<div class="empty"><h3>No matching attributes</h3><p>Try another search, or enable Advanced to inspect raw fields.</p></div>';

if(entity(Number(selection))?.role==='Territory'&&!territoryCastles(Number(selection)).length&&!query)$('#editor').insertAdjacentHTML('beforeend','<section class="section-card"><div class="card-heading"><h3>Castle</h3></div><p class="territory-note">No castle in this territory.</p></section>');

bindFields();}

function bindFields(){document.querySelectorAll('[data-field]').forEach(w=>{const id=Number(w.dataset.field),input=w.querySelector('input,select');input.addEventListener('input',()=>{pending.set(id,input.value);markField(id);indicators();});input.addEventListener('change',()=>{pending.set(id,input.value);flush().catch(err=>notify(err.message,true));});input.addEventListener('keydown',e=>{if(e.key==='Enter'){input.blur();}if(e.key==='Escape'){pending.delete(id);input.value=state.fields[id].value;markField(id);indicators();input.blur();}});});}

function render(){document.querySelectorAll('[data-tab]').forEach(b=>b.classList.toggle('active',b.dataset.tab===tab));if(!state.loaded){$('#modeBrand').textContent='DEFENDER OF THE CROWN';$('#modeHeading').textContent='CHOOSE A CAMPAIGN';$('#playerName').textContent='Open your campaign';$('#saveName').textContent='Choose a Retro or Classic save to begin.';$('#sidebar').innerHTML='';$('#editor').innerHTML='<div class="empty"><h3>Your kingdom awaits</h3><p>Use Choose save to browse your Retro and Classic campaigns.</p></div>';indicators();return;}$('#modeBrand').textContent='DEFENDER OF THE CROWN · '+state.mode.toUpperCase();$('#modeHeading').textContent=state.mode.toUpperCase()+' CAMPAIGN';$('#playerName').textContent=state.name;$('#saveName').textContent=state.mode+' campaign · '+state.file.split(/[\\/]/).pop();$('#saveName').title=state.file;renderSidebar();renderEditor();indicators();}

document.addEventListener('click',e=>{if(!$('#more').contains(e.target))$('#more').open=false;});

rpc('init').then(async s=>{state=s;render();if(!s.loaded)await showPicker();}).catch(err=>{notify(err.message,true);render();});



let saveChoices=[];

async function showPicker(){try{await flush();saveChoices=await rpc('find');renderSaveList();if(!$('#savePicker').open)$('#savePicker').showModal();}catch(err){notify(err.message,true);}}

function renderSaveList(){const mode=$('#modeFilter').value;const list=saveChoices.filter(s=>!mode||s.mode===mode);$('#saveList').innerHTML=list.map(s=>`<button class="save-choice" data-save="${esc(s.path)}"><strong>${esc(s.autosave?'Autosave':s.name)}</strong><span>${esc(s.mode)} · ${esc(s.modified)} · Account ${esc(s.account)}</span><small>${esc(s.path)}</small></button>`).join('')||'<p>No matching campaign saves found. Try Browse files.</p>';$('#saveList').querySelectorAll('[data-save]').forEach(b=>b.onclick=async()=>{try{await flush();const result=await rpc('selectSave',{path:b.dataset.save});if(result.cancelled)return;state=result.state;tab='Player';selection='overview';pending.clear();$('#search').value='';$('#savePicker').close();render();if(result.message)notify(result.message);}catch(err){notify(err.message,true);}});}

$('#modeFilter').onchange=renderSaveList;$('#refreshSaves').onclick=showPicker;$('#closePicker').onclick=()=>$('#savePicker').close();$('#browseSaves').onclick=async()=>{$('#savePicker').close();await action('open');};

