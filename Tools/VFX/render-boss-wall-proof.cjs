const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..');
const out=path.join(root,'ArtOutput/World/BossArenaWall');
async function main(){
 const dirs=['Before','After'].map(x=>path.join(root,'ArtOutput/UI/BossWall'+x));
 for(const dir of dirs){const report=JSON.parse(fs.readFileSync(path.join(dir,'validation-report.json'),'utf8'));if(!report.passed)throw Error('Validation failed: '+dir);}
 const text=(x,y,s,size=24)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${s}</text>`;
 const bg=Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="1164" height="1120"><rect width="100%" height="100%" fill="#E8E1CE"/>${text(28,42,'Boss 围墙 · Unity 实际画面对比',30)}${text(28,82,'升级前 · 树木贴图铺墙')}${text(600,82,'升级后 · 石墙与朱红护柱')}${text(28,1090,'同一地图与竞技场尺寸；总览镜头临时缩远，方便检查四面围墙',20)}</svg>`);
 const parts=[];
 for(let i=0;i<2;i++)parts.push({input:await sharp(path.join(dirs[i],'overview-1080x1920.png')).resize(536,953).png().toBuffer(),left:28+i*572,top:104});
 await sharp(bg).composite(parts).png().toFile(path.join(out,'comparison.png'));
 fs.copyFileSync(path.join(dirs[1],'gameplay-1080x1920.png'),path.join(out,'gameplay-1080x1920.png'));
 fs.copyFileSync(path.join(dirs[1],'overview-1080x1920.png'),path.join(out,'overview-1080x1920.png'));
 console.log(JSON.stringify({output:out}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
