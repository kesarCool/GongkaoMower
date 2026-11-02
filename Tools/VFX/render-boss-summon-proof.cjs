const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/BossSummonV1');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe',ffprobe='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffprobe.exe';
function run(exe,args){const p=spawnSync(exe,args,{encoding:'utf8'});if(p.status!==0)throw Error(p.stderr||p.error);return p.stdout;}
const quote=p=>p.replace(/\\/g,'/').replace(/'/g,"'\\''");
async function main(){
 const evidence=[];
 for(const suite of ['Before','After']){
  const folder=path.join(dir,suite),report=JSON.parse(fs.readFileSync(path.join(folder,'validation-report.json'),'utf8'));
  if(!report.passed||!report.checks.every(c=>c.passed))throw Error(suite+' validation failed');
  const motion=path.join(folder,'summon-motion'),times=JSON.parse(fs.readFileSync(path.join(motion,'frame-times.json'),'utf8')),data=JSON.parse(fs.readFileSync(path.join(motion,'motion.json'),'utf8'));
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
 run(ffmpeg,['-hide_banner','-loglevel','error','-y','-i',path.join(dir,'after.mp4'),'-vf','crop=540:640:0:160,scale=648:768','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'after-closeup.mp4')]);
 const parts=[];
 for(const [i,suite] of ['Before','After'].entries()){
  parts.push({input:await sharp(path.join(dir,suite,'chapter1-charge.png')).extract({left:240,top:680,width:600,height:300}).resize(480,240).png().toBuffer(),left:15+i*490,top:120});
  parts.push({input:await sharp(path.join(dir,suite,'chapter1-birth.png')).extract({left:370,top:400,width:640,height:320}).resize(480,240).png().toBuffer(),left:15+i*490,top:415});
 }
 const text=(x,y,t,size=20)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${t}</text>`;
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="710"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(15,38,'Boss 召唤 · 实际事件前后对比',28)}${text(15,80,'原版')}${text(505,80,'新版：旋转敕令符环 / 真实出生门印')}${text(15,110,'原 0.45 秒蓄力')}${text(15,400,'相同随机抽样下的真实出生位置')}${text(15,696,'零新增贴图与材质；数量、属性、随机状态与生成时机保持。',18)}</svg>`;
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
 fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({passed:true,retimed:false,evidence,note:'Before numeric/reference capture holds newborns for inspection. After final video restores real EnemyAI movement. Static before/after comparisons use the same held numeric fixtures and random seeds.'},null,2));
 console.log(JSON.stringify({passed:true,evidence}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
