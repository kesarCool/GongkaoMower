const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const out=path.resolve(__dirname,'../../ArtOutput/UI/HomePages');
async function main(){
 const labels=['兑换 · 实际商品与限购','词汇 · 双列虚拟列表','成就 · 实际领取状态'];
 let svg='<svg xmlns="http://www.w3.org/2000/svg" width="1212" height="812"><rect width="1212" height="812" fill="#E9E2D0"/><g fill="#493627" font-family="Microsoft YaHei, sans-serif"><text x="36" y="46" font-size="28" font-weight="700">文字割草机 · Unity 页面接入</text>';
 for(let i=0;i<3;i++)svg+=`<text x="${36+i*396}" y="88" font-size="23" font-weight="700">${labels[i]}</text>`;
 svg+='<text x="1176" y="789" font-size="18" text-anchor="end" fill="#76634C">1080 × 1920 渲染缩略图 · 内存测试存档</text></g></svg>';
 const parts=[];let i=0;for(const name of ['exchange-initial-1080x1920.png','lexicon-food-1080x1920.png','achievements-ready-1080x1920.png'])parts.push({input:await sharp(path.join(out,name)).resize(360,640).toBuffer(),left:36+i++*396,top:112});
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(out,'overview.png'));
 console.log(path.join(out,'overview.png'));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
