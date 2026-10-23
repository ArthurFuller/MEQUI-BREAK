import fs from 'node:fs';
import assert from 'node:assert/strict';
const path='Assets/Scenes/EnergyStation/EnergyStation.unity',scene=fs.readFileSync(path,'utf8');
const entries=[...scene.matchAll(/^--- !u!(\d+) &(\d+)\r?\n([\s\S]*?)(?=^--- |$(?![\s\S]))/gm)];
const docs=new Map(entries.map(m=>[m[2],{type:+m[1],body:m[3]}]));
assert.equal(entries.length,docs.size,'Duplicate scene IDs');
for(const [id,d] of docs)for(const line of d.body.split('\n')){if(line.includes('guid:'))continue;for(const m of line.matchAll(/fileID: (\d+)/g))if(m[1]!=='0')assert.ok(docs.has(m[1]),`Missing fileID ${m[1]} in ${id}`);}
const sets=JSON.parse(fs.readFileSync('tmp/energy-question-manifest.json','utf8'));
const cards=JSON.parse(fs.readFileSync('tmp/energy-card-manifest.json','utf8'));
assert.equal(sets.length,3);assert.equal(cards.length,24);assert.equal(new Set(cards.map(c=>c.guid)).size,24);
let ids=new Set();for(const set of sets){assert.equal(set.cards.length,4);for(let i=0;i<4;i++){
 const card=docs.get(set.cards[i]).body;
 const interaction=card.match(/interactionId: (\S+)/)[1];assert.ok(!ids.has(interaction));ids.add(interaction);
 assert.equal(interaction,'energy_'+set.names[i]);
 for(const [field,name] of [['normalSprite',set.names[i]],['usedSprite',set.names[i]+'_gray']]){
  const expected=cards.find(c=>c.name===name);assert.ok(card.includes(`${field}: {fileID: 21300000, guid: ${expected.guid}, type: 3}`));
  assert.ok(fs.existsSync(expected.path));assert.ok(fs.readFileSync(expected.path+'.meta','utf8').includes('guid: '+expected.guid));
 }
 const image=card.match(/stateImage: \{fileID: (\d+)\}/)[1];assert.ok(docs.get(image).body.includes('m_PreserveAspect: 1'));
 const go=card.match(/m_GameObject: \{fileID: (\d+)\}/)[1];const comp=docs.get(go).body.match(/component: \{fileID: (\d+)\}/)[1];
 const rect=docs.get(comp).body;assert.ok(rect.includes('m_SizeDelta: {x: 200, y: 200}'));assert.ok(rect.includes('m_Father: {fileID: 226874327}'));
}}
assert.equal(ids.size,12);
// Existing hierarchy, transforms and external UI references remain intact.
const before=fs.readFileSync('tmp/EnergyStation.before-approved-cards.unity','utf8');
const old=[...before.matchAll(/^--- !u!(\d+) &(\d+)\r?\n([\s\S]*?)(?=^--- |$(?![\s\S]))/gm)];
for(const d of old)if(+d[1]===224&&d[2]!=='226874327')assert.equal(docs.get(d[2]).body.replace(/\r/g,''),d[3].replace(/\r/g,''),'Existing transform changed '+d[2]);
console.log(`PASS: ${docs.size} unique scene objects; all internal fileIDs resolve; 3 questions; 12 unique choices; 24 paired sprite references; all existing UI transforms preserved.`);
