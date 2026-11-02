const fs=require('fs'),path=require('path'),sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const out=path.resolve(__dirname,'../../ArtOutput/UI/Battle');
async function board(file,pages,cols){
 const gap=28,thumb=324,thumbH=576,head=106,rowH=632,w=gap+(thumb+gap)*cols,h=head+Math.ceil(pages.length/cols)*rowH+30;
 let svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}"><rect width="100%" height="100%" fill="#E9E2D0"/><g font-family="Microsoft YaHei, sans-serif" fill="#493627"><text x="28" y="42" font-size="27" font-weight="700">文字割草机 · Unity 接入截图</text><text x="28" y="77" font-size="18">实际 Game 场景 · 示例战斗数据 · 沿用当前关卡地形</text>`;
 const parts=[];
 for(let i=0;i<pages.length;i++){const [name,label]=pages[i],x=gap+i%cols*(thumb+gap),y=head+Math.floor(i/cols)*rowH;svg+=`<text x="${x}" y="${y+23}" font-size="21" font-weight="700">${label}</text>`;parts.push({input:await sharp(path.join(out,name+'.png')).resize(thumb,thumbH,{fit:'contain',background:'#EEE6D4'}).png().toBuffer(),left:x,top:y+38});}
 svg+='</g></svg>';await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(out,file));
}
(async()=>{
 await board('overview.png',[['gameplay-1080x1920','战斗 HUD · 能量珠'],['pause-1080x1920','暂停 · 技能伤害统计'],['revive-1080x1920','复活 · 居中倒计时'],['result-victory-1080x1920','胜利结算'],['result-defeat-1080x1920','失败结算'],['gameplay-boss-1080x1920','Boss 状态']],3);
 await board('adaptation.png',[['pause-short-1080x1440','短屏 · 暂停'],['result-short-1080x1440','短屏 · 结算'],['result-long-1080x2400','长屏 · 结算']],3);
 console.log('Battle screenshot boards saved.');
})();
