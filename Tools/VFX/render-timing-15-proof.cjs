// Export real re-recorded gameplay at its captured elapsed times, without slowing the movie itself.
const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/Timing15');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
const series=[];
for(const folder of ['Explosions','Lightning','Talisman']){
 const report=JSON.parse(fs.readFileSync(path.join(dir,folder,'validation-report.json'),'utf8'));
 if(!report.passed)throw Error(folder+' validation must pass.');
 if(folder==='Explosions'){
  const {clips}=JSON.parse(fs.readFileSync(path.join(dir,folder,'clips.json'),'utf8'));
  for(const clip of clips){if(!clip.damaged||!clip.visibleFrames)throw Error('Missing actual explosion impact');series.push({name:clip.name,folder,frames:clip.name+'-frames',times:clip.times});}
 }else if(folder==='Lightning'){
  const clips=JSON.parse(fs.readFileSync(path.join(dir,folder,'clips.json'),'utf8'));
  const clip=clips.find(c=>c.name==='mixed');if(!clip.damaged||clip.strikes!==5)throw Error('Missing actual lightning volley');
  series.push({name:'lightning-mixed',folder,frames:'mixed-frames',times:JSON.parse(fs.readFileSync(path.join(dir,folder,'mixed-times.json'),'utf8'))});
 }else{
  const clip=JSON.parse(fs.readFileSync(path.join(dir,folder,'motion.json'),'utf8'));if(!clip.damaged||!clip.visible)throw Error('Missing actual talisman hit');
  series.push({name:'talisman',folder,frames:'frames',times:JSON.parse(fs.readFileSync(path.join(dir,folder,'frame-times.json'),'utf8'))});
 }
}
let combined='';
for(const clip of series){let list='';for(let i=0;i<clip.times.length;i++){
 const t=clip.times[i];if(!Number.isFinite(t)||(i&&t<clip.times[i-1]))throw Error('Invalid capture times');
 const file=path.join(dir,clip.folder,clip.frames,`frame-${String(i).padStart(4,'0')}.png`).replace(/\\/g,'/');
 list+=`file '${file}'\nduration ${i+1<clip.times.length?Math.max(.01,clip.times[i+1]-t):.25}\n`;
}combined+=list;}
const concat=path.join(dir,'runtime.concat');fs.writeFileSync(concat,combined);
const run=args=>{const r=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',concat,...args],{encoding:'utf8'});if(r.status)throw Error(r.stderr);};
run(['-vf','fps=30,scale=540:960','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'runtime-1.5x.mp4')]);
run(['-filter_complex','fps=20,scale=360:640:flags=lanczos,split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(dir,'runtime-1.5x.gif')]);
fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({retimed:false,clips:series.map(c=>({name:c.name,frames:c.times.length}))},null,2));
console.log(JSON.stringify({dir,clips:series.length,retimed:false}));
