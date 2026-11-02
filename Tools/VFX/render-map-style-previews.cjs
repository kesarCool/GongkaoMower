// Presentation export only; the maps were generated separately with imagegen, not repainted or recolored here.
const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtOutput/World/MapStyleExplorations');
const maps=[
 {id:'01_bamboo_stream',name:'竹溪绿境',palette:'翠绿 · 青石 · 碧水',swatches:['#397451','#91bd6f','#4f91a0']},
 {id:'02_slate_town',name:'雨巷青石',palette:'蓝灰 · 深青 · 墨绿',swatches:['#818f9d','#30495b','#456657']},
 {id:'03_snow_pines',name:'雪原松林',palette:'冰蓝 · 冷白 · 深青',swatches:['#b2d9ee','#e2f0f6','#225c6b']},
 {id:'04_wisteria_garden',name:'紫藤月庭',palette:'淡紫 · 银灰 · 蓝绿',swatches:['#b1a0ce','#b7bcc8','#347883']},
 {id:'05_crimson_basalt',name:'赤岩红枫',palette:'朱红 · 石墨灰 · 深碧',swatches:['#af3f53','#5b626a','#286b71']}
];
async function main(){
 fs.mkdirSync(path.join(out,'previews'),{recursive:true});
 const cells=[],manifest=[];
 const t=(x,y,s,size=20)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#344552">${s}</text>`;
 let svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1840" height="805"><rect width="100%" height="100%" fill="#e7edf0"/>${t(24,39,'五张地图 · 风格效果图',30)}${t(24,70,'用于比较配色、地表和布局；尚未拆成复用瓦片或接入关卡',19)}`;
 for(let i=0;i<maps.length;i++){
  const m=maps[i],src=path.join(out,'source',m.id+'.png'),dest=path.join(out,'previews',m.id+'_1080x1920.png');
  const original=await sharp(src).metadata();await sharp(src).resize(1080,1920,{fit:'cover',position:'centre'}).png().toFile(dest);
  const x=24+i*364;
  svg+=t(x,108,`${i+1}. ${m.name}`,24)+t(x,134,m.palette,17);
  cells.push({input:await sharp(dest).resize(342,608).png().toBuffer(),left:x,top:150});
  m.swatches.forEach((color,j)=>svg+=`<rect x="${x+j*116}" y="773" width="110" height="10" rx="4" fill="${color}"/>`);
  manifest.push({...m,sourceSize:[original.width,original.height],preview:'previews/'+m.id+'_1080x1920.png',status:'Style concept only; not runtime tiles or a level replacement.'});
 }
 svg+='</svg>';await sharp(Buffer.from(svg)).composite(cells).png().toFile(path.join(out,'overview.png'));
 fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({maps:manifest},null,2));
 const fence=path.join(root,'ArtOutput/World/BossArenaFenceV2');
 const shots=[path.join(root,'ArtOutput/World/BossArenaWall/overview-1080x1920.png'),path.join(fence,'overview-1080x1920.png')];
 const back=Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="1164" height="1120"><rect width="100%" height="100%" fill="#e7edf0"/>${t(28,42,'Boss 围墙 V2 · Unity 实机对比',30)}${t(28,82,'旧版 · 石墙与木柱',23)}${t(600,82,'新版 · 冷灰铁网与倒刺',23)}${t(28,1090,'保留四面墙的碰撞范围 · 新版只有统一金属围栏 · 总览临时缩远镜头',19)}</svg>`);
 await sharp(back).composite(await Promise.all(shots.map(async(s,i)=>({input:await sharp(s).resize(536,953).png().toBuffer(),left:28+i*572,top:104})))).png().toFile(path.join(fence,'comparison.png'));
 console.log(JSON.stringify({maps:manifest.length,mapOverview:path.join(out,'overview.png'),fenceOverview:path.join(fence,'comparison.png')}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
