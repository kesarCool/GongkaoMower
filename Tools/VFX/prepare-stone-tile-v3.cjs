// Mechanical asset export: trim the source's empty alpha margin, preserve aspect, and downsample one reusable tile.
const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtOutput/World/BossArenaStoneV3');
async function main(){
 const source=path.join(out,'source/stone_block_generated.png');
 const {data,info}=await sharp(source).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let left=info.width,top=info.height,right=-1,bottom=-1;
 for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]>24){left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}
 const tile=await sharp(source).extract({left,top,width:right-left+1,height:bottom-top+1}).resize(60,60,{fit:'contain',background:'#00000000'}).extend({top:2,bottom:2,left:2,right:2,background:'#00000000'}).png().toBuffer();
 const asset=path.join(root,'Assets/Res/image/Game/arena/boss_stone_tile_v3.png');fs.writeFileSync(asset,tile);
 await sharp({create:{width:256,height:256,channels:4,background:'#e5ecee'}}).composite([{input:await sharp(tile).resize(192,192,{kernel:'nearest'}).toBuffer(),left:32,top:32}]).png().toFile(path.join(out,'tile-preview.png'));
 fs.writeFileSync(path.join(out,'asset-info.json'),JSON.stringify({asset,width:64,height:64,sourceRect:{left,top,width:right-left+1,height:bottom-top+1},pngBytes:tile.length},null,2));
 console.log(JSON.stringify({asset,width:64,height:64,bytes:tile.length}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
