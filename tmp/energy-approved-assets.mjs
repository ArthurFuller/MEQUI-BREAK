import fs from 'node:fs';
import zlib from 'node:zlib';
import crypto from 'node:crypto';
const root = 'Assets/Art/EnergyStaion';
function decode(path) {
  const b=fs.readFileSync(path), chunks=[]; let w,h,type;
  for(let p=8;p<b.length;) {const n=b.readUInt32BE(p),t=b.toString('ascii',p+4,p+8),d=b.subarray(p+8,p+8+n); if(t==='IHDR'){w=d.readUInt32BE(0);h=d.readUInt32BE(4);type=d[9];if(d[8]!==8||d[12]!==0)throw Error('Unsupported PNG');} if(t==='IDAT')chunks.push(d);p+=n+12;}
  const channels=type===6?4:type===2?3:0;if(!channels)throw Error('Unsupported color '+type);
  const raw=zlib.inflateSync(Buffer.concat(chunks)),stride=w*channels,pixels=Buffer.alloc(w*h*channels);let p=0;
  for(let y=0;y<h;y++){let filter=raw[p++];for(let x=0;x<stride;x++){let a=x>=channels?pixels[y*stride+x-channels]:0,up=y?pixels[(y-1)*stride+x]:0,c=y&&x>=channels?pixels[(y-1)*stride+x-channels]:0,v=raw[p++];if(filter===1)v+=a;else if(filter===2)v+=up;else if(filter===3)v+=Math.floor((a+up)/2);else if(filter===4){let q=a+up-c,pa=Math.abs(q-a),pb=Math.abs(q-up),pc=Math.abs(q-c);v+=pa<=pb&&pa<=pc?a:pb<=pc?up:c;}pixels[y*stride+x]=v&255;}}
  return {w,h,channels,pixels};
}
function crc(b){let c=0xffffffff;for(const v of b){c^=v;for(let k=0;k<8;k++)c=(c>>>1)^((c&1)?0xedb88320:0);}return (c^0xffffffff)>>>0;}
function chunk(t,d){let tb=Buffer.from(t),b=Buffer.alloc(d.length+12);b.writeUInt32BE(d.length);tb.copy(b,4);d.copy(b,8);b.writeUInt32BE(crc(Buffer.concat([tb,d])),d.length+8);return b;}
function png(path,w,h,p){let hdr=Buffer.alloc(13);hdr.writeUInt32BE(w);hdr.writeUInt32BE(h,4);hdr[8]=8;hdr[9]=6;let raw=Buffer.alloc(h*(w*4+1));for(let y=0;y<h;y++)p.copy(raw,y*(w*4+1)+1,y*w*4,(y+1)*w*4);fs.writeFileSync(path,Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',hdr),chunk('IDAT',zlib.deflateSync(raw)),chunk('IEND',Buffer.alloc(0))]));}
const sets=[['hydration','break','stretch','restroom'],['support_doubts','support_priorities','support_reinforcement','support_materials'],['practice_service','practice_preparation','practice_assembly','practice_organization']];
const manifest=[];
for(let s=0;s<3;s++){
 const im=decode(`${root}/Prancha${s+1}.png`),{w,h,channels:c,pixels:p}=im,seen=new Uint8Array(w*h),queue=new Int32Array(w*h),boxes=[];
 const dark=i=>p[i*c]<55&&p[i*c+1]<55&&p[i*c+2]<55;
 for(let i=0;i<w*h;i++){if(seen[i]||!dark(i))continue;let head=0,tail=1;queue[0]=i;seen[i]=1;let left=w,right=0,top=h,bottom=0;
 while(head<tail){const v=queue[head++],x=v%w,y=(v/w)|0;left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);for(const n of [x?v-1:-1,x<w-1?v+1:-1,y?v-w:-1,y<h-1?v+w:-1])if(n>=0&&!seen[n]&&dark(n)){seen[n]=1;queue[tail++]=n;}}
 if(right-left>w*.15&&bottom-top>h*.28&&top<h*.88)boxes.push({left,right,top,bottom});
 }
 boxes.sort((a,b)=>Math.abs(a.top-b.top)>h*.15?a.top-b.top:a.left-b.left);
 if(boxes.length!==8)throw Error(`Expected 8 card borders in sheet ${s+1}, found ${JSON.stringify(boxes)}`);
 fs.mkdirSync(`${root}/Cards`,{recursive:true});
 for(let k=0;k<8;k++){
  let b=boxes[k],cw=b.right-b.left+5,ch=b.bottom-b.top+5,left=b.left-2,top=b.top-2,side=Math.max(cw,ch)+4,out=Buffer.alloc(side*side*4),ox=((side-cw)/2)|0,oy=((side-ch)/2)|0;
  for(let y=0;y<ch;y++)for(let x=0;x<cw;x++){let si=((top+y)*w+left+x)*c,di=((oy+y)*side+ox+x)*4;p.copy(out,di,si,si+3);out[di+3]=255;}
  // Remove only the connected exterior paper, preserving white inside the artwork.
  let visited=new Uint8Array(side*side),q=new Int32Array(side*side),head=0,tail=0;
  for(let x=0;x<side;x++){q[tail++]=x;visited[x]=1;q[tail++]=(side-1)*side+x;visited[(side-1)*side+x]=1;}
  for(let y=1;y<side-1;y++){q[tail++]=y*side;visited[y*side]=1;q[tail++]=y*side+side-1;visited[y*side+side-1]=1;}
  while(head<tail){let v=q[head++],j=v*4;if(out[j+3]&&Math.min(out[j],out[j+1],out[j+2])<205)continue;out[j+3]=0;let x=v%side,y=(v/side)|0;for(let n of [x?v-1:-1,x<side-1?v+1:-1,y?v-side:-1,y<side-1?v+side:-1])if(n>=0&&!visited[n]){visited[n]=1;q[tail++]=n;}}
  let name=sets[s][k%4]+(k>=4?'_gray':''),path=`${root}/Cards/${name}.png`;png(path,side,side,out);
  let guid=crypto.createHash('md5').update(`energy-approved-v1:${name}`).digest('hex'),meta=fs.readFileSync('Assets/Art/UI/EnergyBreak/Runtime/energy-slot.png.meta','utf8').replace(/^guid: \w+/m,`guid: ${guid}`).replace(/maxTextureSize: \d+/g,'maxTextureSize: 512').replace(/textureCompression: \d+/g,'textureCompression: 0');
  fs.writeFileSync(path+'.meta',meta);manifest.push({name,path,guid,side,source:`Prancha${s+1}.png`,bounds:b});
 }
 console.log(`Sheet ${s+1}: ${w}x${h}, 8 intact cards extracted`);
}
fs.writeFileSync('tmp/energy-card-manifest.json',JSON.stringify(manifest,null,2));
console.log('24 approved PNGs extracted without resampling or repainting.');
