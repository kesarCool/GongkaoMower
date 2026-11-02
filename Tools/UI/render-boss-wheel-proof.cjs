const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const ROOT=path.resolve(__dirname,'../..'),OUT=path.join(ROOT,'ArtOutput/UI/BossWheel');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
async function main(){
 const times=JSON.parse(fs.readFileSync(path.join(OUT,'frame-times.json'),'utf8'));
 const file=p=>`file '${path.join(OUT,p).replace(/\\/g,'/')}'\n`;
 let concat=file('ready-1080x1920.png')+'duration 0.7\n';
 for(let i=0;i<times.length;i++)concat+=file(`frames/frame-${String(i).padStart(4,'0')}.png`)+`duration ${i+1<times.length?Math.max(.02,times[i+1]-times[i]):.12}\n`;
 concat+=file('result-three-1080x1920.png')+'duration 1.2\n'+file('result-three-1080x1920.png');
 const list=path.join(OUT,'frames','recording.concat');fs.writeFileSync(list,concat);
 let run=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',list,'-vf','fps=20,scale=540:-2:flags=lanczos','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(OUT,'runtime-reveal.mp4')],{encoding:'utf8'});
 if(run.status!==0)throw new Error(run.stderr);
 run=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',list,'-filter_complex','fps=12.5,scale=400:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=160:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(OUT,'runtime-reveal.gif')],{encoding:'utf8'});
 if(run.status!==0)throw new Error(run.stderr);
 const width=1200,height=860,parts=[];
 let svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="100%" height="100%" fill="#E8E1CE"/><g fill="#493627" font-family="Microsoft YaHei, sans-serif"><text x="28" y="42" font-size="30" font-weight="700">首领奖励转盘 · Unity 实际运行</text><text x="28" y="77" font-size="21">闭箱抽取 → 蓄力开箱 → 奖励逐项落位 · 继续使用原技能、概率与字库</text>`;
 const screens=[['ready-1080x1920.png','抽取前 · 闭合宝箱'],['opening-1080x1920.png','开箱揭晓 · 局部暖光'],['result-three-1080x1920.png','三项强化 · 继续战斗']];
 for(let i=0;i<screens.length;i++){
  const x=28+i*391;svg+=`<text x="${x}" y="119" font-size="23" font-weight="700">${screens[i][1]}</text>`;
  parts.push({input:await sharp(path.join(OUT,screens[i][0])).resize(360,640).png().toBuffer(),left:x,top:140});
 }
 svg+='<text x="28" y="825" font-size="21">28 项 Editor 运行检查通过 · 三个 Prefab GUID 保留 · 无缺失脚本或引用 · 未做微信真机验证</text></g></svg>';
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(OUT,'overview.png'));
 console.log(JSON.stringify({output:OUT,recordedFrames:times.length,previewSeconds:times.at(-1)-times[0]+2.02}));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
