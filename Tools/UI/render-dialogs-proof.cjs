const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const out=path.resolve(__dirname,'../../ArtOutput/UI/Dialogs');
(async()=>{
 const pages=[['level-select','关卡选择'],['confirm','二次确认'],['settings','设置'],['about','关于'],['item-tooltip','物品提示'],['toast','错误码提示']];
 let svg='<svg xmlns="http://www.w3.org/2000/svg" width="1164" height="1460"><rect width="1164" height="1460" fill="#E9E2D0"/><g font-family="Microsoft YaHei, sans-serif" fill="#493627">';
 svg+='<text x="24" y="38" font-size="28" font-weight="700">文字割草机 · 通用弹窗接入</text>';
 const parts=[];
 for(let i=0;i<pages.length;i++){
  const [key,label]=pages[i],x=24+i%3*384,y=70+Math.floor(i/3)*690;
  svg+=`<text x="${x}" y="${y+24}" font-size="23" font-weight="700">${label}</text>`;
  parts.push({input:await sharp(path.join(out,key+'-1080x1920.png')).resize(360,640).toBuffer(),left:x,top:y+36});
 }
 svg+='<text x="1140" y="1440" font-size="18" text-anchor="end" fill="#76634C">Unity Editor 实际渲染 · 临时测试存档</text></g></svg>';
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(out,'overview.png'));
 console.log(path.join(out,'overview.png'));
})();
