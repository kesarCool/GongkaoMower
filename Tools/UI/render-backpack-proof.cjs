const path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const out=path.resolve(__dirname,'../../ArtOutput/UI/Backpack');
async function board(file,keys,labels){
 let svg='<svg xmlns="http://www.w3.org/2000/svg" width="1224" height="824"><rect width="1224" height="824" fill="#E9E2D0"/><g font-family="Microsoft YaHei, sans-serif" fill="#493627"><text x="36" y="48" font-size="29" font-weight="700">背包 · Unity 实际渲染</text>';
 labels.forEach((s,i)=>svg+=`<text x="${36+i*396}" y="93" font-size="23" font-weight="700">${s}</text>`);
 svg+='<text x="1188" y="798" font-size="18" text-anchor="end" fill="#76634C">1080 × 1920 · 临时测试存档 · 35 项检查通过</text></g></svg>';
 const parts=[];for(const[i,key]of keys.entries())parts.push({input:await sharp(path.join(out,`backpack-${key}-1080x1920.png`)).resize(360,640).toBuffer(),left:36+i*396,top:116});
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(out,file));
}
(async()=>{await board('overview.png',['all','currency','materials'],['全部 · 7 种物品','货币 · 实时钱包余额','材料 · 角色碎片']);await board('interaction-states.png',['tooltip','materials-empty','empty'],['点击物品 · ItemTooltip','材料分类为空','背包为空']);console.log(path.join(out,'overview.png'));})();
