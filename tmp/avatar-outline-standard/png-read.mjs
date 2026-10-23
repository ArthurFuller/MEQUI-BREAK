import fs from 'node:fs';
import zlib from 'node:zlib';
import crypto from 'node:crypto';
const root = 'Assets/Art/EnergyStaion';
function decode(path) {
  const b=fs.readFileSync(path), chunks=[]; let w,h,type;
  for(let p=8;p<b.length;) {const n=b.readUInt32BE(p),t=b.toString('ascii',p+4,p+8),d=b.subarray(p+8,p+8+n); if(t==='IHDR'){w=d.readUInt32BE(0);h=d.readUInt32BE(4);type=d[9];if(d[8]!==8||d[12]!==0)throw Error('Unsupported PNG');} if(t==='IDAT')chunks.push(d);p+=n+12;}
  const channels=type===6?4:type===2?3:type===4?2:type===0?1:0;if(!channels)throw Error('Unsupported color '+type);
  const raw=zlib.inflateSync(Buffer.concat(chunks)),stride=w*channels,pixels=Buffer.alloc(w*h*channels);let p=0;
  for(let y=0;y<h;y++){let filter=raw[p++];for(let x=0;x<stride;x++){let a=x>=channels?pixels[y*stride+x-channels]:0,up=y?pixels[(y-1)*stride+x]:0,c=y&&x>=channels?pixels[(y-1)*stride+x-channels]:0,v=raw[p++];if(filter===1)v+=a;else if(filter===2)v+=up;else if(filter===3)v+=Math.floor((a+up)/2);else if(filter===4){let q=a+up-c,pa=Math.abs(q-a),pb=Math.abs(q-up),pc=Math.abs(q-c);v+=pa<=pb&&pa<=pc?a:pb<=pc?up:c;}pixels[y*stride+x]=v&255;}}
  return {w,h,channels,pixels};
}
function crc(b){let c=0xffffffff;for(const v of b){c^=v;for(let k=0;k<8;k++)c=(c>>>1)^((c&1)?0xedb88320:0);}return (c^0xffffffff)>>>0;}
function chunk(t,d){let tb=Buffer.from(t),b=Buffer.alloc(d.length+12);b.writeUInt32BE(d.length);tb.copy(b,4);d.copy(b,8);b.writeUInt32BE(crc(Buffer.concat([tb,d])),d.length+8);return b;}
function png(path,w,h,p){let hdr=Buffer.alloc(13);hdr.writeUInt32BE(w);hdr.writeUInt32BE(h,4);hdr[8]=8;hdr[9]=6;let raw=Buffer.alloc(h*(w*4+1));for(let y=0;y<h;y++)p.copy(raw,y*(w*4+1)+1,y*w*4,(y+1)*w*4);fs.writeFileSync(path,Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',hdr),chunk('IDAT',zlib.deflateSync(raw)),chunk('IEND',Buffer.alloc(0))]));}

export {decode,png};
