// Export real Game captures at recorded time intervals. This is media packaging, not asset generation.
const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/FieldGeneratorV3');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
function run(args){const p=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y',...args],{encoding:'utf8'});if(p.status)throw Error(p.stderr);}
const label=(x,y,s,size=19)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${s}</text>`;
async function main(){
  const report=JSON.parse(fs.readFileSync(path.join(dir,'After/validation-report.json'),'utf8'));
  if(!report.passed)throw Error('Actual Game validation must pass before export.');
  const summary=[];
  for(const name of ['normal-motion','breakthrough-motion']){
    const folder=path.join(dir,'After',name),motion=JSON.parse(fs.readFileSync(path.join(folder,'motion.json'),'utf8'));
    if(!motion.damaged)throw Error('Motion must capture real periodic damage.');
    const times=JSON.parse(fs.readFileSync(path.join(folder,'frame-times.json'),'utf8'));
    if(times.length!==motion.frames)throw Error('Frame count mismatch.');
    let list='';
    for(let i=0;i<times.length;i++){
      if(!Number.isFinite(times[i])||(i&&times[i]<=times[i-1]))throw Error('Invalid timing.');
      const frame=path.join(folder,'frames',`frame-${String(i).padStart(4,'0')}.png`).replace(/\\/g,'/');
      if(!fs.existsSync(frame))throw Error('Missing frame '+i);
      list+=`file '${frame}'\nduration ${i+1<times.length?times[i+1]-times[i]:.04}\n`;
    }
    const concat=path.join(folder,'runtime.concat');fs.writeFileSync(concat,list);
    run(['-f','concat','-safe','0','-i',concat,'-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,name+'.mp4')]);
    summary.push({name,...motion,firstCaptureSeconds:times[0],lastCaptureSeconds:times.at(-1)});
  }
  // Combine the two independent, unretimed clips for convenient review.
  const clips=path.join(dir,'states.concat');
  fs.writeFileSync(clips,['normal-motion','breakthrough-motion'].map(n=>`file '${path.join(dir,n+'.mp4').replace(/\\/g,'/')}'`).join('\n'));
  run(['-f','concat','-safe','0','-i',clips,'-c','copy','-movflags','+faststart',path.join(dir,'after.mp4')]);
  run(['-i',path.join(dir,'after.mp4'),'-filter_complex','fps=20,scale=360:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(dir,'after.gif')]);
  // Same captured time base, cropped around the real player for readable rotation review.
  run(['-i',path.join(dir,'normal-motion.mp4'),'-i',path.join(dir,'breakthrough-motion.mp4'),'-filter_complex','[0:v]crop=340:270:110:430,scale=510:406[a];[1:v]crop=340:270:110:430,scale=510:406[b];[a][b]hstack=inputs=2:shortest=1[v]','-map','[v]','-c:v','libx264','-crf','19','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'rotation-comparison.mp4')]);
  run(['-i',path.join(dir,'rotation-comparison.mp4'),'-filter_complex','fps=20,scale=720:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(dir,'rotation-comparison.gif')]);
  const parts=[];
  for(let row=0;row<2;row++)for(let col=0;col<2;col++){
    const folder=col?path.join(dir,'After'):path.join(root,'ArtOutput/UI/FieldGeneratorV2/After');
    parts.push({input:await sharp(path.join(folder,row?'max.png':'normal.png')).extract({left:230,top:866,width:680,height:512}).resize(425,320).png().toBuffer(),left:18+col*445,top:105+row*362});
  }
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="908" height="835"><rect width="100%" height="100%" fill="#F4E9D0"/>${label(18,32,'罡气护体 · 反向旋转与突破加强',26)}${label(18,64,'上一版：固定朝向，突破换色')}${label(463,64,'本版：内外反转，突破强化轮廓')}${label(18,91,'普通状态 · Lv1',18)}${label(18,453,'突破状态 · Lv5',18)}${label(18,818,'同一地图与技能数值 / 真实 Game 场景 / 本版无新增贴图',18)}</svg>`;
  await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
  const proof={retimed:false,checks:report.checks.length,summary,note:'Ordinary motion uses level 4; breakthrough uses level 5. Stills compare identical levels. No device FPS claim.'};
  fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify(proof,null,2));console.log(JSON.stringify(proof));
}
main().catch(e=>{console.error(e);process.exitCode=1;});

