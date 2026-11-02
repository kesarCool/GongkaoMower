// Mechanical export of the generated sheet: trim alpha, resize, and pack; no artwork is redrawn.
const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..');
const source=path.join(root,'ArtOutput/World/BossArenaWall/source/boss_arena_wall_generated.png');
const out=path.join(root,'Assets/Res/image/Game/arena');
const proof=path.join(root,'ArtOutput/World/BossArenaWall');
async function crop(roi,width,height,inset){
 const {data,info}=await sharp(source).extract(roi).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let left=info.width,top=info.height,right=-1,bottom=-1;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>16){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}
 if(right<left)throw Error('Empty source crop');
 const rect={left:roi.left+left,top:roi.top+top,width:right-left+1,height:bottom-top+1};
 // Remove the generated rounded end caps on the repeating axis, so tiled walls have no transparent gaps.
 if(inset==='x'){rect.left+=6;rect.width-=12;}
 if(inset==='y'){rect.top+=10;rect.height-=20;}
 return {buffer:await sharp(source).extract(rect).resize(width,height,{fit:'fill'}).png().toBuffer(),sourceRect:rect};
}
async function main(){
 fs.mkdirSync(out,{recursive:true});
 const defs=[
  {name:'boss_wall_horizontal',roi:{left:0,top:170,width:900,height:400},x:0,y:0,w:256,h:64,pivot:[.5,.5],inset:'x'},
  {name:'boss_wall_vertical',roi:{left:920,top:0,width:334,height:710},x:256,y:0,w:64,h:256,pivot:[.5,.5],inset:'y'},
  {name:'boss_wall_post',roi:{left:0,top:720,width:600,height:534},x:320,y:0,w:96,h:144,pivot:[.5,.17]}
 ];
 const pieces=[],manifest=[];
 for(const d of defs){const r=await crop(d.roi,d.w,d.h,d.inset);pieces.push({input:r.buffer,left:d.x,top:d.y});
  if(d.inset){const pixels=await sharp(r.buffer).ensureAlpha().raw().toBuffer();
   const at=(x,y)=>pixels[(y*d.w+x)*4+3];
   const samples=d.inset==='x'?[at(0,d.h>>1),at(d.w-1,d.h>>1)]:[at(d.w>>1,0),at(d.w>>1,d.h-1)];
   if(samples.some(a=>a<240))throw Error('Transparent wall seam at '+d.name+': '+samples.join(','));
  }
  manifest.push({name:d.name,x:d.x,y:256-d.y-d.h,width:d.w,height:d.h,pivot:d.pivot,sourceRect:r.sourceRect});
  fs.writeFileSync(path.join(proof,d.name+'.png'),r.buffer);
 }
 await sharp({create:{width:512,height:256,channels:4,background:'#00000000'}}).composite(pieces).png().toFile(path.join(out,'boss_wall_atlas_v1.png'));
 fs.writeFileSync(path.join(proof,'sprite-layout.json'),JSON.stringify({width:512,height:256,pixelsPerUnit:96,sprites:manifest},null,2));
 console.log(JSON.stringify({atlas:'Assets/Res/image/Game/arena/boss_wall_atlas_v1.png',bytes:fs.statSync(path.join(out,'boss_wall_atlas_v1.png')).size,sprites:manifest}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
