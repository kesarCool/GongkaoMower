const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/BossDashKnifeV1');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe',ffprobe='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffprobe.exe';
const names=['dash15-motion','dash20-motion','knife-motion','combo-eight-motion','combo-sixteen-motion'];
const rel=p=>path.relative(root,p).replace(/\\/g,'/'),quote=p=>p.replace(/\\/g,'/').replace(/'/g,"'\\''");
function run(exe,args){const p=spawnSync(exe,args,{encoding:'utf8'});if(p.status!==0)throw Error(p.stderr||p.error);return p.stdout;}
const encode=args=>run(ffmpeg,['-hide_banner','-loglevel','error','-y',...args]);
function combine(clips,name){const concat=path.join(dir,name+'.concat'),out=path.join(dir,name+'.mp4');fs.writeFileSync(concat,clips.map(p=>`file '${quote(p)}'`).join('\n'));encode(['-f','concat','-safe','0','-i',concat,'-c','copy','-movflags','+faststart',out]);return out;}
async function comparison(){
 const rows=[['dash15-charge','冲刺：实时方向预警'],['dash20-locked','冲刺：出手锁向与速度线'],['knife-1-flight','小刀：朱红短刃与短尾'],['wall-charge','墙边：按当前 Cast 截断预警']];
 const parts=[];
 for(let row=0;row<rows.length;row++)for(let col=0;col<2;col++){
  const suite=col?'After':'Before';let source=path.join(dir,suite,rows[row][0]+'.png'),captureTime;
  if(row===2){
   const folder=path.join(dir,suite,'knife-motion'),times=JSON.parse(fs.readFileSync(path.join(folder,'frame-times.json'),'utf8'));
   const index=times.reduce((best,t,i)=>Math.abs(t-.65)<Math.abs(times[best]-.65)?i:best,0);
   source=path.join(folder,'frames',`frame-${String(index).padStart(4,'0')}.png`);captureTime=times[index];
  }
  const metadata=await sharp(source).metadata(),scale=metadata.width/1080;
  parts.push({input:await sharp(source).extract({left:0,top:Math.round(420*scale),width:metadata.width,height:Math.round(900*scale)}).resize(432,360).png().toBuffer(),left:18+col*450,top:116+row*402});
  if(captureTime!==undefined)fs.writeFileSync(path.join(dir,suite,'knife-flight-comparison.json'),JSON.stringify({source:rel(source),actualSeconds:captureTime,note:'Existing timestamped frame selected after the knife clears the visible word; no scene or asset changes.'},null,2));
 }
 const text=(x,y,t,size=18)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${t}</text>`;
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="918" height="1740"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(18,34,'Boss 冲刺 / 小刀 · 实战前后对比',27)}${text(18,70,'原表现')}${text(468,70,'新版：共享朱红风格，零新增贴图')}${rows.map((r,i)=>text(18,105+i*402,r[1])).join('')}${text(18,1725,'真实 Game 独立运行；小刀取已有回放约 0.65 秒，视频保留原始时间戳。',16)}</svg>`;
 await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
}
async function main(){
 if(process.argv.includes('--comparison-only')){await comparison();return;}
 const groups={},captures=[],reports=[];
 for(const suite of ['Before','After']){
  const folder=path.join(dir,suite),r=JSON.parse(fs.readFileSync(path.join(folder,'validation-report.json'),'utf8'));
  if(!r.passed||!r.checks.every(c=>c.passed)||fs.readFileSync(path.join(folder,'status.txt'),'utf8').trim()!=='passed')throw Error(suite+' real skill validation must pass');
  reports.push({suite,checks:r.checks.length});groups[suite]=[];
  for(const name of names){
   const motionFolder=path.join(folder,name),times=JSON.parse(fs.readFileSync(path.join(motionFolder,'frame-times.json'),'utf8')),motion=JSON.parse(fs.readFileSync(path.join(motionFolder,'motion.json'),'utf8'));
   if(times.length<2||motion.retimed||motion.timeScale!==1)throw Error('Missing real-time capture contract: '+name);
   let list='';
   for(let i=0;i<times.length;i++){
    if(!Number.isFinite(times[i])||(i&&times[i]<=times[i-1]))throw Error('Invalid timestamps');
    const frame=path.join(motionFolder,'frames',`frame-${String(i).padStart(4,'0')}.png`);if(!fs.existsSync(frame))throw Error('Missing '+frame);
    list+=`file '${quote(frame)}'\nduration ${i+1<times.length?times[i+1]-times[i]:1/30}\n`;
   }
   list+=`file '${quote(path.join(motionFolder,'frames',`frame-${String(times.length-1).padStart(4,'0')}.png`))}'\n`;
   const concat=path.join(motionFolder,'capture.concat'),out=path.join(dir,suite.toLowerCase()+'-'+name+'.mp4');fs.writeFileSync(concat,list);
   encode(['-f','concat','-safe','0','-i',concat,'-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',out]);
   const encodedSeconds=Number(run(ffprobe,['-v','error','-show_entries','format=duration','-of','default=noprint_wrappers=1:nokey=1',out]).trim()),sourceSeconds=times[times.length-1]-times[0];
   if(Math.abs(encodedSeconds-sourceSeconds)>.15)throw Error('Capture duration drift');
   captures.push({...motion,source:rel(motionFolder),video:rel(out),frames:times.length,encodedSeconds,sourceSeconds});groups[suite].push(out);
  }
 }
 combine(groups.Before.slice(0,3),'before');const after=combine(groups.After.slice(0,3),'after');
 combine(groups.Before.slice(3),'combination-before');combine(groups.After.slice(3),'combination-after');
 encode(['-i',after,'-vf','crop=540:600:0:180,scale=648:720','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'after-closeup.mp4')]);
 await comparison();
 fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({passed:true,reports,retimed:false,captures,note:'Separate real Game runs using current visual fallback for the old prefab. Motion uses timeScale 1 and actual timestamps. Output FPS only repeats/drops captured images; this is not device FPS or upload-size validation.'},null,2));
 console.log(JSON.stringify({dir:rel(dir),reports,captures:captures.length,retimed:false}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
