// Export genuine level-four -> level-five presentation samples and actual five-strike replays.
const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const version=process.argv[2]||'V1';
if(!['V1','V2'].includes(version))throw Error('Expected V1 or V2.');
const root=path.resolve(__dirname,'../..'),output=path.join(root,'ArtOutput/UI/LightningBreakthrough'+version);
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
const text=(x,y,s,size=21)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${s}</text>`;
async function main(){
  const report=JSON.parse(fs.readFileSync(path.join(output,'validation-report.json'),'utf8'));
  const clips=JSON.parse(fs.readFileSync(path.join(output,'clips.json'),'utf8'));
  if(!report.passed||clips.length!==2||clips.some(c=>!c.damaged||c.strikes!==5||c.visibleFrames<1))throw Error('Actual progression and damage validation must pass.');
  let concat='';
  for(const clip of clips){
    const times=JSON.parse(fs.readFileSync(path.join(output,clip.name+'-times.json'),'utf8'));
    for(let i=0;i<times.length;i++){
      if(!Number.isFinite(times[i])||(i&&times[i]<times[i-1]))throw Error('Invalid timing');
      const file=path.join(output,clip.name+'-frames',`frame-${String(i).padStart(4,'0')}.png`).replace(/\\/g,'/');
      concat+=`file '${file}'\nduration ${i+1<times.length?Math.max(.01,times[i+1]-times[i]):.25}\n`;
    }
  }
  const list=path.join(output,'runtime.concat');fs.writeFileSync(list,concat);
  const run=args=>{const r=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',list,...args],{encoding:'utf8'});if(r.status)throw Error(r.stderr);};
  run(['-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(output,'breakthrough.mp4')]);
  run(['-filter_complex','fps=20,scale=360:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(output,'breakthrough.gif')]);
  const parts=[];
  for(let i=0;i<2;i++){
    const source=version==='V2'&&i===0?path.join(root,'ArtOutput/UI/LightningBreakthroughV1/breakthrough-flash.png'):path.join(output,i?'breakthrough-flash.png':'level4-flash.png');
    parts.push({input:await sharp(source).extract({left:300,top:500,width:480,height:640}).resize(430,573).png().toBuffer(),left:20+i*450,top:98});
  }
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="920" height="720"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(20,33,version==='V2'?'雷霆万钧 · 突破效果重新设计':'雷霆万钧 · 普通 → 突破',27)}${text(20,62,version==='V2'?'上一版 · 红蓝叠加':'普通状态（Lv.4）· 蓝白落雷',21)}${text(470,62,version==='V2'?'新版 · 专用朱红雷电贴图':'突破状态（Lv.5）· 红蓝复合雷',21)}${text(20,86,'真实技能命中采样；保留原伤害、冷却与连击节奏，文字仍在特效前层',17)}${text(20,697,version==='V2'?'独立 1024×512 图集 / PNG 250.4 KiB / 2 粒子 / 未做微信真机性能测试':'不新增贴图 / 共用 54.4 KiB 图集 / 突破最多 3 粒子 / 未做微信真机性能测试',17)}</svg>`;
  await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(output,'comparison.png'));
  fs.writeFileSync(path.join(output,'proof.json'),JSON.stringify({checks:report.checks.length,retimed:false,clips},null,2));
  console.log(JSON.stringify({output,checks:report.checks.length}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
