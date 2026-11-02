const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/BossHeadCrestV1');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe',ffprobe='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffprobe.exe';
function run(exe,args){const p=spawnSync(exe,args,{encoding:'utf8'});if(p.status!==0)throw Error(p.stderr||p.error);return p.stdout;}
const quote=p=>p.replace(/\\/g,'/').replace(/'/g,"'\\''");
async function main(){
 const before=path.join(root,'ArtOutput/UI/BossCloneV1/After'),after=path.join(dir,'After');
 const report=JSON.parse(fs.readFileSync(path.join(after,'validation-report.json'),'utf8'));
 if(!report.passed||!report.checks.every(c=>c.passed))throw Error('Validation failed');
 const motion=path.join(after,'clone-motion'),times=JSON.parse(fs.readFileSync(path.join(motion,'frame-times.json'),'utf8')),data=JSON.parse(fs.readFileSync(path.join(motion,'motion.json'),'utf8'));
 if(data.timeScale!==1||data.retimed||times.length<2)throw Error('Not a real-time capture');
 let concat='';
 for(let i=0;i<times.length;i++){
  if(!Number.isFinite(times[i])||(i&&times[i]<=times[i-1]))throw Error('Invalid timestamps');
  const frame=path.join(motion,'frames',`frame-${String(i).padStart(4,'0')}.png`);if(!fs.existsSync(frame))throw Error('Missing frame');
  concat+=`file '${quote(frame)}'\nduration ${i+1<times.length?times[i+1]-times[i]:1/30}\n`;
 }
 concat+=`file '${quote(path.join(motion,'frames',`frame-${String(times.length-1).padStart(4,'0')}.png`))}'\n`;
 const list=path.join(motion,'capture.concat'),video=path.join(dir,'after.mp4');fs.writeFileSync(list,concat);
 const encode=args=>run(ffmpeg,['-hide_banner','-loglevel','error','-y',...args]);
 encode(['-f','concat','-safe','0','-i',list,'-vf','fps=30','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',video]);
 const duration=Number(run(ffprobe,['-v','error','-show_entries','format=duration','-of','default=noprint_wrappers=1:nokey=1',video]).trim());
 if(Math.abs(duration-(times.at(-1)-times[0]))>.15)throw Error('Video timing drift');
 encode(['-i',video,'-vf','crop=540:620:0:160,scale=648:744','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'after-closeup.mp4')]);
 const parts=[];
 for(const [i,folder] of [before,after].entries()){
  parts.push({input:await sharp(path.join(folder,'dash-charge.png')).extract({left:200,top:720,width:680,height:360}).resize(480,254).png().toBuffer(),left:15+i*490,top:130});
  parts.push({input:await sharp(path.join(folder,'knife-birth.png')).extract({left:0,top:500,width:1080,height:980}).resize(480,436).png().toBuffer(),left:15+i*490,top:438});
 }
 const text=(x,y,t,size=20)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${t}</text>`;
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="940"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(15,40,'Boss 头顶标识 · 实际关卡对比',28)}${text(15,84,'原版：悬空双刀')}${text(505,84,'新版：紧凑冠徽 / 分身空心标记')}${text(15,118,'本体蓄力')}${text(15,424,'真实分身出生')}${text(15,923,'两次独立实战截图；复用现有材质，零新增贴图。',18)}</svg>`;
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
 await sharp(path.join(after,'head-crest-clones.png')).extract({left:190,top:600,width:700,height:820}).resize(560,656).png().toFile(path.join(dir,'crest-closeup.png'));
 fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({passed:true,checks:report.checks.length,retimed:false,encodedSeconds:duration,capture:data,before:'ArtOutput/UI/BossCloneV1/After',after:'ArtOutput/UI/BossHeadCrestV1/After'},null,2));
 console.log(JSON.stringify({passed:true,checks:report.checks.length,encodedSeconds:duration}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
