const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/BossZoneV1');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe',ffprobe='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffprobe.exe';
function run(exe,args){const p=spawnSync(exe,args,{encoding:'utf8'});if(p.status!==0)throw Error(p.stderr||p.error);return p.stdout;}
const quote=p=>p.replace(/\\/g,'/').replace(/'/g,"'\\''");
async function main(){
 const evidence=[];
 for(const suite of ['Before','After']){
  const folder=path.join(dir,suite),report=JSON.parse(fs.readFileSync(path.join(folder,'validation-report.json'),'utf8'));
  if(!report.passed||!report.checks.every(c=>c.passed))throw Error(suite+' validation failed');
  const motion=path.join(folder,'zone-motion'),times=JSON.parse(fs.readFileSync(path.join(motion,'frame-times.json'),'utf8')),data=JSON.parse(fs.readFileSync(path.join(motion,'motion.json'),'utf8'));
  if(data.timeScale!==1||data.retimed||times.length<2)throw Error('Not a real-time capture');
  let concat='';for(let i=0;i<times.length;i++){
   if(!Number.isFinite(times[i])||(i&&times[i]<=times[i-1]))throw Error('Invalid timestamps');
   const frame=path.join(motion,'frames',`frame-${String(i).padStart(4,'0')}.png`);if(!fs.existsSync(frame))throw Error('Missing frame');
   concat+=`file '${quote(frame)}'\nduration ${i+1<times.length?times[i+1]-times[i]:1/30}\n`;
  }
  concat+=`file '${quote(path.join(motion,'frames',`frame-${String(times.length-1).padStart(4,'0')}.png`))}'\n`;
  const list=path.join(motion,'capture.concat'),video=path.join(dir,suite.toLowerCase()+'.mp4');fs.writeFileSync(list,concat);
  run(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',list,'-vf','fps=30','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',video]);
  const duration=Number(run(ffprobe,['-v','error','-show_entries','format=duration','-of','default=noprint_wrappers=1:nokey=1',video]).trim());
  if(Math.abs(duration-(times.at(-1)-times[0]))>.15)throw Error('Timing drift');
  evidence.push({...data,suite,checks:report.checks.length,encodedSeconds:duration});
 }
 run(ffmpeg,['-hide_banner','-loglevel','error','-y','-i',path.join(dir,'after.mp4'),'-vf','crop=540:480:0:290,scale=756:672','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'after-closeup.mp4')]);
 const parts=[];
 for(const [i,suite] of ['Before','After'].entries()){
  parts.push({input:await sharp(path.join(dir,suite,'warning.png')).extract({left:120,top:720,width:900,height:700}).resize(480,370).png().toBuffer(),left:15+i*490,top:120});
  parts.push({input:await sharp(path.join(dir,suite,'rupture.png')).extract({left:120,top:720,width:900,height:700}).resize(480,370).png().toBuffer(),left:15+i*490,top:560});
 }
 const text=(x,y,t,size=20)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${t}</text>`;
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="990"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(15,38,'Boss 地裂 · 实际事件前后对比',28)}${text(15,80,'原版')}${text(505,80,'新版：独立危险圈 / 裂纹与碎石')}${text(15,110,'真实世界落点预警')}${text(15,540,'各自实际时序的爆裂阶段；旧版 fallback 坐标偏移')}${text(15,970,'零新增贴图与材质；单圈预警明确由约 0.14 秒增至 0.7 秒。',18)}</svg>`;
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
 fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({passed:true,retimed:false,evidence,note:'Real common Boss and ZoneModule in the Game scene. Test-only zone injection, unchanged live tables. Baseline captured before production edits; old fallback indicators are displaced off the camera. Per-zone warning intentionally changed from 0.14s to 0.7s. Footage uses actual frame timestamps. After camera was widened from orthographic size 11 at x=15 to size 12 at x=15.8 to include all five circles; comparison is not a pixel-aligned camera match.'},null,2));
 console.log(JSON.stringify({passed:true,evidence}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
