const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/SwordPresentationV1');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
function run(args){const p=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y',...args],{encoding:'utf8'});if(p.status)throw Error(p.stderr);}
const label=(x,y,s,size=18)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${s}</text>`;
async function main(){
 const summary=[];
 for(const version of ['Before','After']){
  const report=JSON.parse(fs.readFileSync(path.join(dir,version,'validation-report.json'),'utf8'));if(!report.passed)throw Error(version+' real Game validation failed.');
  for(const name of ['normal-motion','burst-motion','legend-motion']){
   const folder=path.join(dir,version,name),motion=JSON.parse(fs.readFileSync(path.join(folder,'motion.json'),'utf8')),times=JSON.parse(fs.readFileSync(path.join(folder,'frame-times.json'),'utf8'));if(!motion.damaged||times.length!==motion.frames)throw Error('Capture must contain original collision damage.');let list='';
   for(let i=0;i<times.length;i++){if(!Number.isFinite(times[i])||(i&&times[i]<=times[i-1]))throw Error('Invalid frame timing.');const file=path.join(folder,'frames',`frame-${String(i).padStart(4,'0')}.png`).replace(/\\/g,'/');list+=`file '${file}'\nduration ${i+1<times.length?times[i+1]-times[i]:.04}\n`;}
   const concat=path.join(folder,'capture.concat');fs.writeFileSync(concat,list);run(['-f','concat','-safe','0','-i',concat,'-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,version+'-'+name+'.mp4')]);summary.push({version,name,checks:report.checks.length,...motion});
  }
 }
 const concat=path.join(dir,'new-states.concat');fs.writeFileSync(concat,['normal-motion','burst-motion','legend-motion'].map(n=>`file '${path.join(dir,'After-'+n+'.mp4').replace(/\\/g,'/')}'`).join('\n'));
 run(['-f','concat','-safe','0','-i',concat,'-c','copy','-movflags','+faststart',path.join(dir,'after.mp4')]);
 // Crop the actual battle area around the player, flying swords and target, preserving captured timing.
 run(['-i',path.join(dir,'After-burst-motion.mp4'),'-vf','crop=400:390:70:280,scale=640:624','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'burst-closeup.mp4')]);
 run(['-i',path.join(dir,'After-legend-motion.mp4'),'-vf','crop=400:390:70:280,scale=640:624','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'legend-closeup.mp4')]);
 run(['-i',path.join(dir,'burst-closeup.mp4'),'-filter_complex','fps=20,scale=410:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(dir,'burst-closeup.gif')]);
 const parts=[];const names=['normal-flight','max-flight','burst-ring'];
 for(let row=0;row<3;row++)for(let col=0;col<2;col++)parts.push({input:await sharp(path.join(dir,col?'After':'Before',names[row]+'.png')).extract({left:140,top:560,width:800,height:780}).resize(400,390).png().toBuffer(),left:18+col*420,top:100+row*425});
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="856" height="1395"><rect width="100%" height="100%" fill="#F4E9D0"/>${label(18,32,'刀光剑影 · 实际技能前后对照',26)}${label(18,62,'旧版：共用灰白刀片与短白尾迹')}${label(438,62,'新版：青锋 / 金青双刃与柔边剑气')}${label(18,90,'普通飞刃',17)}${label(18,515,'满级飞刃',17)}${label(18,940,'八剑环绕',17)}${label(18,1380,'同一技能数值与地图；真实飞行 / 环绕 / 碰撞；一张共用图集',17)}</svg>`;
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({retimed:false,summary,note:'Real original shots, orbit and Legend. No device frame-rate conclusion.'},null,2));console.log(JSON.stringify({dir,summary}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
