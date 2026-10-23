import fs from 'node:fs';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
const codec=fs.readFileSync('tmp/energy-approved-assets.mjs','utf8').split('const sets=')[0];
fs.writeFileSync('tmp/energy-png-codec.mjs',codec+'\nexport {decode,png};\n');
const {decode,png}=await import('./energy-png-codec.mjs');
const folder='Assets/Art/EnergyStaion/Cards';
const backup='tmp/energy-before-edge-cleanup';fs.mkdirSync(backup,{recursive:true});
const cleaned='tmp/energy-cleaned-edges';fs.mkdirSync(cleaned,{recursive:true});
const results=[];
for(const name of fs.readdirSync(folder).filter(n=>n.endsWith('.png'))){
 const path=folder+'/'+name,im=decode(path),{w,h,channels:c,pixels:p}=im;assert.equal(c,4);
 const old=Buffer.from(p),meta=fs.readFileSync(path+'.meta'),count=w*h;
 if(!fs.existsSync(backup+'/'+name))fs.copyFileSync(path,backup+'/'+name);
 const seen=new Uint8Array(count),outside=new Uint8Array(count),q=new Int32Array(count);let head=0,tail=0;
 const dark=i=>old[i*4+3]>0&&Math.max(old[i*4],old[i*4+1],old[i*4+2])<=55;
 const add=i=>{if(i>=0&&i<count&&!seen[i]){seen[i]=1;q[tail++]=i;}};
 for(let x=0;x<w;x++){add(x);add((h-1)*w+x);}for(let y=1;y<h-1;y++){add(y*w);add(y*w+w-1);}
 while(head<tail){let i=q[head++];if(dark(i))continue;outside[i]=1;let x=i%w,y=(i/w)|0;if(x)add(i-1);if(x<w-1)add(i+1);if(y)add(i-w);if(y<h-1)add(i+w);}
 let fringe=0,removed=0,keptInterior=0;
 for(let i=0;i<count;i++){
  if(!outside[i]){keptInterior++;continue;}
  let x=i%w,y=(i/w)|0,nearBlack=false;
  for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)if(x+dx>=0&&x+dx<w&&y+dy>=0&&y+dy<h&&dark((y+dy)*w+x+dx))nearBlack=true;
  const j=i*4,alpha=nearBlack?Math.round(old[j+3]*(255-Math.max(old[j],old[j+1],old[j+2]))/238):0;
  // Unmatte exterior antialias pixels against their original white paper.
  // Preserve coverage using dark RGB; all fully transparent padding is dark too.
  p[j]=p[j+1]=p[j+2]=17;p[j+3]=alpha>8?Math.min(255,alpha):0;
  if(old[j+3]&&p[j+3]===0)removed++;if(p[j+3])fringe++;
 }
 for(let i=0;i<count;i++)if(!outside[i])assert.ok(old.subarray(i*4,i*4+4).equals(p.subarray(i*4,i*4+4)),`Interior altered: ${name}`);
 for(let i=0;i<count;i++)if(outside[i])assert.ok(p[i*4]===17&&p[i*4+1]===17&&p[i*4+2]===17,`White exterior remains: ${name}`);
 png(cleaned+'/'+name,w,h,p);assert.ok(meta.equals(fs.readFileSync(path+'.meta')),'Metadata changed');
 const written=decode(cleaned+'/'+name);assert.equal(written.w,w);assert.equal(written.h,h);assert.ok(written.pixels.equals(p));
 results.push({name,w,h,removed,fringe,keptInterior,sha256:crypto.createHash('sha256').update(fs.readFileSync(cleaned+'/'+name)).digest('hex')});
}
fs.writeFileSync('tmp/energy-edge-cleanup-results.json',JSON.stringify(results,null,2));
console.log(`PASS: ${results.length} PNGs cleaned; interior RGBA, dimensions, GUIDs and import settings unchanged.`);
console.log(`Removed ${results.reduce((n,r)=>n+r.removed,0)} exterior paper pixels; reconstructed black antialias edges.`);

// Proof on dark red and dark gray backgrounds; before/after, no artwork redraw.
const examples=['hydration.png','restroom.png','support_doubts_gray.png','practice_assembly.png'];
const W=1000,H=examples.length*240,out=Buffer.alloc(W*H*4,255);
for(let row=0;row<examples.length;row++)for(let col=0;col<4;col++){
 const image=decode((col%2?cleaned:backup)+'/'+examples[row]),bg=col<2?[90,30,45]:[35,38,44];
 for(let y=0;y<240;y++)for(let x=0;x<250;x++){
  const dx=col*250+x,dy=row*240+y,j=(dy*W+dx)*4;let color=bg;
  if(x>=15&&x<235&&y>=10&&y<230){const sx=((x-15)*image.w/220)|0,sy=((y-10)*image.h/220)|0,k=(sy*image.w+sx)*4,a=image.pixels[k+3]/255;color=bg.map((v,c)=>Math.round(image.pixels[k+c]*a+v*(1-a)));}
  for(let c=0;c<3;c++)out[j+c]=color[c];out[j+3]=255;
 }
}
png('tmp/energy-edges-before-after.png',W,H,out);
