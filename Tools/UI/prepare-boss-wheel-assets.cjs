const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const ROOT=path.resolve(__dirname,'../..'),F='Assets/Res/image/Game/bosswheel/',assets=[];
async function svg(name,w,h,body,border=[0,0,0,0]){
 const file=F+name+'.png';
 await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}">${body}</svg>`)).png().toFile(path.join(ROOT,file));
 assets.push({path:file,border,maxSize:Math.max(64,2**Math.ceil(Math.log2(Math.max(w,h))))});
}
async function main(){
 fs.mkdirSync(path.join(ROOT,F),{recursive:true});
 for(const state of ['closed','open']){
  const file=F+`chest_${state}.png`;
  await sharp(path.join(ROOT,`ArtOutput/UI/BossWheelDesignV2/chest-${state}.png`)).resize(512,512).png().toFile(path.join(ROOT,file));
  assets.push({path:file,border:[0,0,0,0],maxSize:512});
 }
 let body='<circle cx="432" cy="439" r="428" fill="#49362722"/><circle cx="432" cy="432" r="428" fill="#DBB16D" stroke="#735638" stroke-width="6"/><circle cx="432" cy="432" r="417" fill="#FFEDB2" stroke="#FFF7DA" stroke-width="5"/><circle cx="432" cy="432" r="400" fill="#EEE2C0" stroke="#C4A56D" stroke-width="3"/>';
 for(let i=0;i<16;i++){
  const a=(i+.5)*Math.PI/8;
  body+=`<circle cx="${432+Math.cos(a)*413}" cy="${432+Math.sin(a)*413}" r="6.5" fill="#BC8A42"/><path d="M${432+Math.cos(a)*264} ${432+Math.sin(a)*264}L${432+Math.cos(a)*390} ${432+Math.sin(a)*390}" stroke="#C7AF7F" stroke-opacity=".35" stroke-width="2"/>`;
 }
 body+='<circle cx="432" cy="432" r="243" fill="#F8F0D8" stroke="#C8B78E" stroke-width="3"/><circle cx="432" cy="432" r="227" fill="#FFF8E4" stroke="#E4D6B7" stroke-width="2"/>';
 await svg('wheel_base',864,864,body);
 for(const [name,fill,stroke,sw]of [['slot_active','#FFF9EA','#CDBB98',2.5],['slot_passive','#E7EDD5','#A9BB8A',2.5],['slot_winner','#FFD47A','#9B6B27',4.5],['reward_active','#FFF9EA','#D7BE89',2],['reward_passive','#EEF2DE','#B4C69A',2]])
  await svg(name+'_9slice',64,64,`<rect x="3" y="3" width="58" height="58" rx="14" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`,[20,20,20,20]);
 await svg('cursor_9slice',64,64,'<rect x="3" y="3" width="58" height="58" rx="14" fill="#FFD66635" stroke="#CA8E32" stroke-width="4"/>',[20,20,20,20]);
 await svg('winner_check',40,40,'<circle cx="20" cy="20" r="17" fill="#A97A2E" stroke="#FFF9EA" stroke-width="3"/><path d="M11 20l6 6 13-15" fill="none" stroke="#FFF9EA" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>');
 await svg('warm_glow',128,128,'<defs><radialGradient id="g"><stop stop-color="#FFFFFF" stop-opacity=".85"/><stop offset=".45" stop-color="#FFFFFF" stop-opacity=".4"/><stop offset="1" stop-color="#FFFFFF" stop-opacity="0"/></radialGradient></defs><circle cx="64" cy="64" r="64" fill="url(#g)"/>');
 let rays='';for(let i=0;i<12;i++){const a=i*Math.PI/6,l=i%2?118:104;rays+=`<path d="M${128+Math.cos(a)*32} ${128+Math.sin(a)*32}L${128+Math.cos(a-.08)*l} ${128+Math.sin(a-.08)*l}L${128+Math.cos(a+.08)*l} ${128+Math.sin(a+.08)*l}Z" fill="#FFFFFF"/>`;}
 await svg('reveal_rays',256,256,rays);
 await svg('sparkle',32,32,'<path d="M16 1Q20 12 31 16Q20 20 16 31Q12 20 1 16Q12 12 16 1Z" fill="#FFFFFF"/>');
 fs.writeFileSync(path.join(ROOT,'Tools/UI/boss-wheel-assets.json'),JSON.stringify({assets},null,2));
 console.log(JSON.stringify({files:assets.length,sourcePngBytes:assets.reduce((s,a)=>s+fs.statSync(path.join(ROOT,a.path)).size,0)}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
