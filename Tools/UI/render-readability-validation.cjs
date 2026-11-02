const fs=require('fs'),path=require('path'),sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const dir=path.resolve(__dirname,'../../ArtOutput/UI/BattleReadability');
(async()=>{
 const panels=[['before-level-1-1-1080x1920.png','修改前：顶层偏实、小地图黑屏'],['after-level-1-1-1080x1920.png','修改后：降低遮挡、修复小地图'],['after-level-1-1-grass-1080x1920.png','草地与沙地：共用同一字库']];
 const width=1164,height=808,parts=[];
 let svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="100%" height="100%" fill="#E9E2D0"/><g font-family="Microsoft YaHei, sans-serif" fill="#493627"><text x="28" y="39" font-size="28" font-weight="700">战斗页 · 地图与文字可读性调整</text><text x="28" y="73" font-size="19">Unity Editor 运行截图 · 同一 msyh SDF 字库 · 仅调整配色、描边与 SDF 字面粗细</text>`;
 for(let i=0;i<panels.length;i++){const x=28+i*380;svg+=`<text x="${x}" y="115" font-size="21" font-weight="700">${panels[i][1]}</text>`;parts.push({input:await sharp(path.join(dir,panels[i][0])).resize(352,626).png().toBuffer(),left:x,top:136});}
 svg+='<text x="28" y="793" font-size="18">TopGradient Alpha：1 → 0.82　　怪物：深色字面＋奶油描边　　伤害：米白 / 金 / 橙红＋深棕描边</text></g></svg>';
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
 console.log('Readability comparison saved.');
})();
