// Sprite export: remove low-opacity matte residue, trim, resize, and pack the generated wire cutouts.
const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/World/BossArenaFenceV2');
const source=path.join(dir,'source/fence_clean_generated.png');
async function piece(roi,w,h){
 const {data,info}=await sharp(source).extract(roi).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let left=info.width,top=info.height,right=-1,bottom=-1;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++){
  const p=(y*info.width+x)*4;let a=data[p+3];a=a<=208?0:Math.round((a-208)*255/47);data[p+3]=a;
  if(a>24){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}
 }
 if(right<left)throw Error('Empty fence cutout');
 return sharp(data,{raw:info}).extract({left,top,width:right-left+1,height:bottom-top+1}).resize(w,h).png().toBuffer();
}
async function main(){
 const horizontal=await piece({left:30,top:345,width:1130,height:310},256,64);
 const vertical=await piece({left:1200,top:45,width:290,height:925},64,256);
 fs.writeFileSync(path.join(dir,'fence_horizontal.png'),horizontal);fs.writeFileSync(path.join(dir,'fence_vertical.png'),vertical);
 const atlas=path.join(root,'Assets/Res/image/Game/arena/boss_fence_atlas_v2.png');
 await sharp({create:{width:512,height:256,channels:4,background:'#00000000'}}).composite([{input:horizontal,left:0,top:0},{input:vertical,left:256,top:0}]).png().toFile(atlas);
 await sharp({create:{width:680,height:300,channels:4,background:'#e4eee9'}}).composite([{input:await sharp(horizontal).resize(512,128).toBuffer(),left:20,top:90},{input:vertical,left:570,top:20}]).png().toFile(path.join(dir,'asset-check.png'));
 console.log(JSON.stringify({atlas,bytes:fs.statSync(atlas).size}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
