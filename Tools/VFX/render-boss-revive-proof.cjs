const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/BossReviveV1');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe',ffprobe='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffprobe.exe';
function run(exe,args){const p=spawnSync(exe,args,{encoding:'utf8'});if(p.status!==0)throw Error(p.stderr||p.error);return p.stdout;}
const quote=p=>p.replace(/\\/g,'/').replace(/'/g,"'\\''");
async function main(){
 const evidence=[];
 for(const suite of ['Before','After']){
  const folder=path.join(dir,suite),report=JSON.parse(fs.readFileSync(path.join(folder,'validation-report.json'),'utf8'));
  if(!report.passed||!report.checks.every(c=>c.passed))throw Error(suite+' validation failed');
  const motion=path.join(folder,'revive-motion'),times=JSON.parse(fs.readFileSync(path.join(motion,'frame-times.json'),'utf8')),data=JSON.parse(fs.readFileSync(path.join(motion,'motion.json'),'utf8'));
  if(data.timeScale!==1||data.retimed||times.length<2)throw Error('Not a real-time capture');let concat='';
  for(let i=0;i<times.length;i++){
   if(!Number.isFinite(times[i])||(i&&times[i]<=times[i-1]))throw Error('Invalid timestamps');
   const frame=path.join(motion,'frames',`frame-${String(i).padStart(4,'0')}.png`);if(!fs.existsSync(frame))throw Error('Missing frame');
   concat+=`file '${quote(frame)}'\nduration ${i+1<times.length?times[i+1]-times[i]:1/30}\n`;
  }
  concat+=`file '${quote(path.join(motion,'frames',`frame-${String(times.length-1).padStart(4,'0')}.png`))}'\n`;
  const list=path.join(motion,'capture.concat'),video=path.join(dir,suite.toLowerCase()+'.mp4');fs.writeFileSync(list,concat);
  run(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',list,'-vf','fps=30','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',video]);
  const duration=Number(run(ffprobe,['-v','error','-show_entries','format=duration','-of','default=noprint_wrappers=1:nokey=1',video]).trim());
  if(Math.abs(duration-(times.at(-1)-times[0]))>.15)throw Error('Timing drift');evidence.push({...data,suite,checks:report.checks.length,encodedSeconds:duration});
 }
 run(ffmpeg,['-hide_banner','-loglevel','error','-y','-i',path.join(dir,'after.mp4'),'-vf','crop=540:620:0:160,scale=648:744','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'after-closeup.mp4')]);
 const parts=[],rows=[['scatter','真实字形散开'],['core','余烬等待与悬停'],['return','同一批字形回到原位']];
 for(let row=0;row<rows.length;row++)for(let column=0;column<2;column++)parts.push({input:await sharp(path.join(dir,column?'After':'Before',rows[row][0]+'.png')).extract({left:180,top:650,width:720,height:420}).resize(480,280).png().toBuffer(),left:15+column*490,top:126+row*326});
 const text=(x,y,t,size=20)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${t}</text>`;
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="1105"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(15,40,'Boss 复活 · 真实致命伤前后对比',28)}${text(15,82,'原版：散字消失，整块弹回')}${text(505,82,'新版：散字 / 余烬 / 实际回聚')}${rows.map((r,i)=>text(15,115+i*326,r[1])).join('')}${text(15,1090,'原等待与次数保持；0 新贴图 / 材质，护弧与真实免伤同步。',18)}</svg>`;
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
 fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({passed:true,retimed:false,evidence,note:'Both videos use actual EnemyBase lethal damage and the real table ReviveModule. Timestamp durations are preserved. Static captures pause only to sample each state.'},null,2));console.log(JSON.stringify({passed:true,evidence}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
