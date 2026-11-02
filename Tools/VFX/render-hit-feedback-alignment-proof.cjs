const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/HitFeedbackAlignmentV1'),current=path.join(dir,'Sword');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
function run(args){const p=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y',...args],{encoding:'utf8'});if(p.status)throw Error(p.stderr);}
async function main(){
 const report=JSON.parse(fs.readFileSync(path.join(current,'validation-report.json'),'utf8'));
 if(!report.passed||fs.readFileSync(path.join(current,'status.txt'),'utf8').trim()!=='passed')throw Error('Real skill validation must pass first.');
 const clips=[];
 for(const name of ['normal-motion','burst-motion','legend-motion']){
  const folder=path.join(current,name),times=JSON.parse(fs.readFileSync(path.join(folder,'frame-times.json'),'utf8'));let list='';
  for(let i=0;i<times.length;i++){
   if(!Number.isFinite(times[i])||(i&&times[i]<=times[i-1]))throw Error('Invalid capture timing.');
   list+=`file '${path.join(folder,'frames',`frame-${String(i).padStart(4,'0')}.png`).replace(/\\/g,'/')}'\nduration ${i+1<times.length?times[i+1]-times[i]:.04}\n`;
  }
  const concat=path.join(folder,'capture.concat');fs.writeFileSync(concat,list);const output=path.join(dir,name+'.mp4');
  run(['-f','concat','-safe','0','-i',concat,'-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',output]);clips.push(output);
 }
 const concat=path.join(dir,'clips.concat');fs.writeFileSync(concat,clips.map(p=>`file '${p.replace(/\\/g,'/')}'`).join('\n'));
 run(['-f','concat','-safe','0','-i',concat,'-c','copy','-movflags','+faststart',path.join(dir,'sword-after.mp4')]);
 run(['-i',clips[1],'-vf','crop=400:390:70:280,scale=640:624','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'sword-closeup.mp4')]);
 const rows=['normal-flight','max-flight','normal-hit','max-hit'],parts=[];
 for(let row=0;row<rows.length;row++)for(let col=0;col<2;col++){
  const p=col?path.join(current,rows[row]+'.png'):path.join(root,'ArtOutput/UI/SwordPresentationV1/After',rows[row]+'.png');
  parts.push({input:await sharp(p).extract({left:140,top:560,width:800,height:780}).resize(400,390).png().toBuffer(),left:18+col*420,top:100+row*420});
 }
 const text=(x,y,s,size=18)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${s}</text>`;
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="856" height="1810"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(18,32,'飞刃长度与命中落点修正',26)}${text(18,62,'调整前')}${text(438,62,'调整后：长度 -20%，命中贴合字形')}${text(18,90,'普通飞刃')}${text(18,510,'满级飞刃')}${text(18,930,'普通命中')}${text(18,1350,'满级命中')}${text(18,1795,'真实地图、实际碰撞；效果取可见字形，不改变伤害与碰撞',17)}</svg>`;
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
 fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({passed:true,checks:report.checks.length,retimed:false,clips:clips.map(p=>path.relative(root,p)),note:'Timestamped real Game captures; comparison rows are separate real collision cases, not matched simulation frames.'},null,2));
 console.log(JSON.stringify({dir,checks:report.checks.length}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
