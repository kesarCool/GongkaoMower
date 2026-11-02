const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/BossCloneV1');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe',ffprobe='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffprobe.exe';
const quote=p=>p.replace(/\\/g,'/').replace(/'/g,"'\\''"),rel=p=>path.relative(root,p).replace(/\\/g,'/');
function run(exe,args){const p=spawnSync(exe,args,{encoding:'utf8'});if(p.status!==0)throw Error(p.stderr||p.error);return p.stdout;}
const encode=args=>run(ffmpeg,['-hide_banner','-loglevel','error','-y',...args]);
async function main(){
 const evidence=[];
 for(const suite of ['Before','After']){
  const folder=path.join(dir,suite),r=JSON.parse(fs.readFileSync(path.join(folder,'validation-report.json'),'utf8'));
  if(!r.passed||!r.checks.every(c=>c.passed)||fs.readFileSync(path.join(folder,'status.txt'),'utf8').trim()!=='passed')throw Error(suite+' validation has not passed');
  const motion=path.join(folder,'clone-motion'),times=JSON.parse(fs.readFileSync(path.join(motion,'frame-times.json'),'utf8')),data=JSON.parse(fs.readFileSync(path.join(motion,'motion.json'),'utf8'));
  if(data.timeScale!==1||data.retimed||times.length<2)throw Error('Not a real-time capture');
  let list='';for(let i=0;i<times.length;i++){if(!Number.isFinite(times[i])||(i&&times[i]<=times[i-1]))throw Error('Invalid timestamps');const frame=path.join(motion,'frames',`frame-${String(i).padStart(4,'0')}.png`);if(!fs.existsSync(frame))throw Error('Missing '+frame);list+=`file '${quote(frame)}'\nduration ${i+1<times.length?times[i+1]-times[i]:1/30}\n`;}
  list+=`file '${quote(path.join(motion,'frames',`frame-${String(times.length-1).padStart(4,'0')}.png`))}'\n`;
  const concat=path.join(motion,'capture.concat'),out=path.join(dir,suite.toLowerCase()+'.mp4');fs.writeFileSync(concat,list);
  encode(['-f','concat','-safe','0','-i',concat,'-vf','fps=30','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',out]);
  const seconds=Number(run(ffprobe,['-v','error','-show_entries','format=duration','-of','default=noprint_wrappers=1:nokey=1',out]).trim());
  if(Math.abs(seconds-(times.at(-1)-times[0]))>.15)throw Error('Capture duration drift');
  evidence.push({...data,suite,checks:r.checks.length,video:rel(out),encodedSeconds:seconds});
 }
 encode(['-i',path.join(dir,'after.mp4'),'-vf','crop=540:620:0:160,scale=648:744','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'after-closeup.mp4')]);
 const rows=[['dash-charge','本体蓄力：字形重影与聚拢'],['blade-birth','分身出生：文字与装饰统一半透明'],['knife-birth','本体实心金印，分身朱红断续印']],parts=[];
 for(let row=0;row<rows.length;row++)for(let col=0;col<2;col++)parts.push({input:await sharp(path.join(dir,col?'After':'Before',rows[row][0]+'.png')).extract({left:0,top:420,width:1080,height:1000}).resize(432,400).png().toBuffer(),left:18+col*450,top:116+row*442});
 const text=(x,y,t,size=18)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${t}</text>`;
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="918" height="1440"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(18,34,'Boss 分身 · 实战前后对比',27)}${text(18,70,'原表现')}${text(468,70,'新版：修正继承，零新增贴图')}${rows.map((r,i)=>text(18,105+i*442,r[1])).join('')}${text(18,1428,'真实 CloneModule 生成与 Start；独立运行截图，视频保留原始时间戳。',16)}</svg>`;
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
 fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({passed:true,retimed:false,evidence,note:'Actual CloneModule births, not manually placed CloneMarkers. Before is one original cast; After contains two rounds and natural expiry. No device FPS or final upload package measurement.'},null,2));console.log(JSON.stringify({passed:true,evidence}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
