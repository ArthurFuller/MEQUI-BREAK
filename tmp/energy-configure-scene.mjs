import fs from 'node:fs';
import crypto from 'node:crypto';
const path='Assets/Scenes/EnergyStation/EnergyStation.unity';
let text=fs.readFileSync(path,'utf8');
if(text.includes('questionSets:'))throw Error('Scene already configured');
fs.copyFileSync(path,'tmp/EnergyStation.before-approved-cards.unity');
const manifest=JSON.parse(fs.readFileSync('tmp/energy-card-manifest.json','utf8'));
const guid=name=>manifest.find(a=>a.name===name).guid;
const docs=new Map([...text.matchAll(/^--- !u!(\d+) &(\d+)\r?\n([\s\S]*?)(?=^--- |$(?![\s\S]))/gm)].map(m=>[m[2],{id:m[2],type:m[1],body:m[0]}]));
function refs(body,key){return [...body.matchAll(new RegExp(`${key}: \\{fileID: (\\d+)\\}`,'g'))].map(m=>m[1]);}
function components(go){return refs(docs.get(go).body,'component');}
function subtree(go){let ids=new Set([go]);for(const id of components(go))ids.add(id);let transform=docs.get(components(go)[0]).body,part=transform.split('  m_Children:')[1].split('  m_Father:')[0];for(let id of refs(part,'-')){let child=refs(docs.get(id).body,'m_GameObject')[0];for(const c of subtree(child))ids.add(c);}return ids;}
function children(go){let b=docs.get(components(go)[0]).body;return [...b.split('  m_Children:')[1].split('  m_Father:')[0].matchAll(/fileID: (\d+)/g)].map(m=>refs(docs.get(m[1]).body,'m_GameObject')[0]);}
const dragIds=['2043999787','3100001005','3100001105','3100001205'];
const roots=dragIds.map(id=>refs(docs.get(id).body,'m_GameObject')[0]);
// Capture unmodified templates, including all slot and Icon descendants.
function all(go){let ids=new Set([go,...components(go)]);for(let child of children(go))for(let id of all(child))ids.add(id);return ids;}
const templates=roots.map(go=>({go,ids:[...all(go)]}));
const originals=new Map([...docs].map(([id,d])=>[id,{...d}]));
let nextId=7200000000;
const sets=[
 {id:'pause',prompt:'O que ajudaria o Méqui a recuperar as energias hoje?',names:['hydration','break','stretch','restroom'],labels:['Hidratar','Descansar','Alongar','Banheiro'],feedback:['Hidratação registrada!','Descanso registrado!','Alongamento registrado!','Pausa para banheiro registrada!']},
 {id:'support',prompt:'Que apoio ajudaria o Méqui no trabalho de hoje?',names:['support_doubts','support_priorities','support_reinforcement','support_materials'],labels:['Dúvidas','Prioridades','Reforço','Materiais']},
 {id:'practice',prompt:'O que o Méqui gostaria de praticar mais?',names:['practice_service','practice_preparation','practice_assembly','practice_organization'],labels:['Atendimento','Preparo','Montagem','Organização']}
];
const newTransforms=[];
for(let s=0;s<3;s++){
 sets[s].cards=[];
 for(let i=0;i<4;i++){
  const template=templates[i],map=new Map(template.ids.map(id=>[id,s===0?id:String(nextId++)]));
  if(s>0)for(let id of template.ids){let old=originals.get(id),body=old.body.replace(/^--- !u!(\d+) &\d+/,`--- !u!$1 &${map.get(id)}`).replace(/fileID: (\d+)/g,(m,id)=>map.has(id)?`fileID: ${map.get(id)}`:m);docs.set(map.get(id),{id:map.get(id),type:old.type,body});}
  const cardGo=map.get(template.go),drag=map.get(dragIds[i]),dragDoc=docs.get(drag),cardVisual=refs(dragDoc.body,'cardVisual')[0];
  const iconGo=children(cardVisual).find(go=>docs.get(go).body.includes('m_Name: Icon'));
  const image=components(iconGo).find(id=>docs.get(id).body.includes('  m_Sprite:'));
  const name=sets[s].names[i];
  dragDoc.body=dragDoc.body.replace(/  interactionId:.*\r?\n/,`  interactionId: energy_${name}\n`).replace(/  feedbackMessage:.*\r?\n/,`  feedbackMessage: ${JSON.stringify(sets[s].feedback?.[i]??sets[s].labels[i]+' registrado!')}\n`);
  dragDoc.body=dragDoc.body.replace(/  slotVisual:.*\r?\n/,m=>m+`  stateImage: {fileID: ${image}}\n  normalSprite: {fileID: 21300000, guid: ${guid(name)}, type: 3}\n  usedSprite: {fileID: 21300000, guid: ${guid(name+'_gray')}, type: 3}\n`);
  docs.get(image).body=docs.get(image).body.replace(/  m_Sprite:.*\r?\n/,`  m_Sprite: {fileID: 21300000, guid: ${guid(name)}, type: 3}\n`).replace('  m_PreserveAspect: 0','  m_PreserveAspect: 1');
  docs.get(cardGo).body=docs.get(cardGo).body.replace(/  m_Name:.*\r?\n/,`  m_Name: Card${name.split('_').map(v=>v[0].toUpperCase()+v.slice(1)).join('')}\n`).replace(/  m_IsActive: 1/,'  m_IsActive: 0');
  sets[s].cards.push(drag);
  if(s>0)newTransforms.push(components(cardGo)[0]);
 }
}
const parent=docs.get('226874327');
parent.body=parent.body.replace('  m_Father:',newTransforms.map(id=>`  - {fileID: ${id}}\n`).join('')+'  m_Father:');
const controller=[...docs.values()].find(d=>d.body.includes('guid: a738c918e73ea2841abde7dbc387fe8f'));
let yaml='  questionSets:\n';for(const s of sets){yaml+=`  - id: ${s.id}\n    prompt: ${JSON.stringify(s.prompt)}\n    cards:\n`+s.cards.map(id=>`    - {fileID: ${id}}\n`).join('');}
controller.body=controller.body.replace('  timerLabel:',yaml+'  timerLabel:');
const prompt=docs.get('3100003003');if(prompt)prompt.body=prompt.body.replace(/  m_text:.*\r?\n/,`  m_text: ${JSON.stringify(sets[0].prompt)}\n`);
text=text.substring(0,text.indexOf('--- !u!'))+[...docs.values()].map(d=>d.body).join('');
fs.writeFileSync(path,text.replace(/\r?\n/g,'\r\n'));
// New sprite folder is an ordinary project asset folder with stable identity.
fs.writeFileSync('Assets/Art/EnergyStaion/Cards.meta',`fileFormatVersion: 2\nguid: ${crypto.createHash('md5').update('energy-approved-v1:cards-folder').digest('hex')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
fs.writeFileSync('tmp/energy-question-manifest.json',JSON.stringify(sets,null,2));
console.log('Configured 12 serialized card hierarchies and 3 question sets; no runtime object creation.');
