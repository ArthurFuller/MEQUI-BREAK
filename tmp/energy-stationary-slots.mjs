import fs from 'node:fs';
import assert from 'node:assert/strict';
const path='Assets/Scenes/EnergyStation/EnergyStation.unity';
const text=fs.readFileSync(path,'utf8');
fs.writeFileSync('tmp/EnergyStation.before-stationary-slots.unity',text);
const docs=new Map([...text.matchAll(/^--- !u!(\d+) &(\d+)\r?\n([\s\S]*?)(?=^--- |$(?![\s\S]))/gm)].map(m=>[m[2],{id:m[2],type:+m[1],body:m[0]}]));
const gameObject=b=>b.match(/m_GameObject: \{fileID: (\d+)\}/)[1];
const rect=go=>docs.get(go).body.match(/component: \{fileID: (\d+)\}/)[1];
const slots=[];
for(const d of docs.values()){
 if(!d.body.includes('guid: 5df13a5e0e7406a41878965c03eb6e5b'))continue;
 const slot=d.body.match(/slotVisual: \{fileID: (\d+)\}/)[1],slotRect=rect(slot),cardRect=rect(gameObject(d.body));
 const card=docs.get(cardRect),fixed=docs.get(slotRect),parent=card.body.match(/m_Father: \{fileID: (\d+)\}/)[1];
 assert.equal(parent,'226874327');
 assert.ok(fixed.body.includes(`m_Father: {fileID: ${cardRect}}`),'Slot already detached');
 card.body=card.body.replace(new RegExp(`  - \\{fileID: ${slotRect}\\}\\r?\\n`),'');
 fixed.body=fixed.body.replace(/m_Father: \{fileID: \d+\}/,`m_Father: {fileID: ${parent}}`);
 for(const field of ['m_LocalRotation','m_LocalScale','m_AnchorMin','m_AnchorMax','m_AnchoredPosition','m_SizeDelta','m_Pivot']){
  const value=card.body.match(new RegExp(`  ${field}: ([^\\r\\n]+)`))[1];
  fixed.body=fixed.body.replace(new RegExp(`  ${field}: [^\\r\\n]+`),`  ${field}: ${value}`);
 }
 docs.get(slot).body=docs.get(slot).body.replace('m_IsActive: 1','m_IsActive: 0');
 const image=[...docs.values()].find(v=>v.body.includes(`m_GameObject: {fileID: ${slot}}`)&&v.body.includes('  m_Sprite:'));
 assert.ok(image);image.body=image.body.replace('m_RaycastTarget: 1','m_RaycastTarget: 0');
 slots.push(slotRect);
}
assert.equal(slots.length,12);
const parent=docs.get('226874327');parent.body=parent.body.replace(/  m_Children:\r?\n/,'  m_Children:\r\n'+slots.map(id=>`  - {fileID: ${id}}\r\n`).join(''));
const result=text.substring(0,text.indexOf('--- !u!'))+[...docs.values()].map(d=>d.body).join('');
fs.writeFileSync(path,result);
// The slot must not descend from the dragged object; it must occupy the same slot.
for(const d of docs.values())if(d.body.includes('guid: 5df13a5e0e7406a41878965c03eb6e5b')){
 const slot=d.body.match(/slotVisual: \{fileID: (\d+)\}/)[1],r=docs.get(rect(slot)).body;
 assert.ok(r.includes('m_Father: {fileID: 226874327}'));
 assert.ok(!docs.get(rect(gameObject(d.body))).body.includes(`  - {fileID: ${rect(slot)}}`));
}
console.log('PASS: 12 stationary slots reparented in scene; all card positions preserved; slot raycasts disabled.');
