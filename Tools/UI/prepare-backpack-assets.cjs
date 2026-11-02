const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),assets=[];
(async()=>{
 const folder='Assets/Res/image/Home/backpack';fs.mkdirSync(path.join(root,folder),{recursive:true});
 for(const [name,fill,stroke,sw]of [['item_card_normal_9slice','#FFFAEC','#D7C8A6',2.5],['item_card_selected_9slice','#FFF1CC','#B4863B',5],['item_icon_surface_9slice','#FFFFFF','none',0]]){
  const file=`${folder}/${name}.png`,source=`<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128"><rect x="3" y="3" width="122" height="122" rx="22" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/></svg>`;
  fs.writeFileSync(path.join(root,file),await sharp(Buffer.from(source)).png().toBuffer());assets.push({path:file,border:[28,28,28,28],maxSize:128});
 }
 const glowPath=`${folder}/empty_backpack_glow.png`;
 fs.writeFileSync(path.join(root,glowPath),await sharp(Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="292" height="232"><ellipse cx="146" cy="116" rx="146" ry="116" fill="#EFEAD8"/></svg>')).png().toBuffer());
 assets.push({path:glowPath,border:[0,0,0,0],maxSize:512});
 fs.writeFileSync(path.join(root,'Tools/UI/backpack-assets.json'),JSON.stringify({assets},null,2));console.log(JSON.stringify({assets:assets.length,bytes:assets.reduce((n,a)=>n+fs.statSync(path.join(root,a.path)).size,0)}));
})();
