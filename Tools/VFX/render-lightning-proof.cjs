// Real Unity frame exports. Preserve elapsed capture times; titles stay outside gameplay.
const fs=require('node:fs'), path=require('node:path');
const {spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'), output=path.join(root,'ArtOutput/UI/LightningPresentationV1');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
const text=(x,y,s,size=21)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${s}</text>`;
async function main(){
  fs.mkdirSync(output,{recursive:true}); const summary={};
  for(const version of ['Before','After']){
    const source=path.join(root,'ArtOutput/UI/Lightning'+version);
    const report=JSON.parse(fs.readFileSync(path.join(source,'validation-report.json'),'utf8'));
    const clips=JSON.parse(fs.readFileSync(path.join(source,'clips.json'),'utf8'));
    if(!report.passed||clips.length!==2||clips.some(c=>!c.damaged||c.strikes!==5||c.visibleFrames<1))throw Error('Complete real gameplay validation must pass.');
    summary[version]={checks:report.checks.length,clips}; let combined='';
    for(const clip of clips){
      const times=JSON.parse(fs.readFileSync(path.join(source,clip.name+'-times.json'),'utf8'));
      const folder=path.join(output,version.toLowerCase(),clip.name);fs.mkdirSync(folder,{recursive:true});
      const title=(version==='After'?'新版':'旧版')+' · '+(clip.name==='mixed'?'雷电＋爆炸＋符令':'满级 5 连击');
      const header=await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="540" height="62"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(12,26,title,23)}${text(12,50,'实际技能施放；保留原伤害与采集帧间时长',16)}</svg>`)).png().toBuffer();
      let list='';
      for(let i=0;i<times.length;i++){
        if(!Number.isFinite(times[i])||(i&&times[i]<times[i-1]))throw Error('Invalid timing');
        const game=await sharp(path.join(source,clip.name+'-frames',`frame-${String(i).padStart(4,'0')}.png`)).resize(540,960).png().toBuffer();
        const target=path.join(folder,`frame-${String(i).padStart(4,'0')}.png`);
        await sharp({create:{width:540,height:1022,channels:4,background:'#F4E9D0'}}).composite([{input:header,left:0,top:0},{input:game,left:0,top:62}]).png().toFile(target);
        list+=`file '${target.replace(/\\/g,'/')}'\nduration ${i+1<times.length?Math.max(.01,times[i+1]-times[i]):.25}\n`;
      }
      combined+=list;
    }
    const concat=path.join(output,version.toLowerCase()+'.concat');fs.writeFileSync(concat,combined);
    const run=args=>{const r=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',concat,...args],{encoding:'utf8'});if(r.status)throw Error(r.stderr);};
    run(['-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(output,version.toLowerCase()+'.mp4')]);
    run(['-filter_complex','fps=20,scale=360:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(output,version.toLowerCase()+'.gif')]);
  }
  for(const ground of ['max','grass']){
    let svg=`<svg xmlns="http://www.w3.org/2000/svg" width="920" height="720"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(20,33,'雷霆万钧 · '+(ground==='grass'?'草地':'浅色地图')+'真实落雷采样',27)}${text(20,62,'旧版：9 层粒子',21)}${text(470,62,'新版：闪击＋余电，2 层共用图集',21)}${text(20,86,'同一原技能配置；已触发粒子采样至 0.035 秒，文字保持在新版特效前层',17)}`;
    const parts=[];
    for(let i=0;i<2;i++){
      const input=await sharp(path.join(root,'ArtOutput/UI/Lightning'+(i?'After':'Before'),ground+'-flash.png')).extract({left:300,top:500,width:480,height:640}).resize(430,573).png().toBuffer();
      parts.push({input,left:20+i*450,top:98});
    }
    svg+=text(20,697,'512×256 / PNG 54.4 KiB / 单道最多 2 粒子；未做微信真机帧率与最终包体验证',17)+'</svg>';
    await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(output,ground==='max'?'comparison.png':'comparison-grass.png'));
  }
  fs.writeFileSync(path.join(output,'proof.json'),JSON.stringify({source:'real Unity captures',retimed:false,...summary},null,2));
  console.log(JSON.stringify({output,Before:summary.Before.checks,After:summary.After.checks}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
