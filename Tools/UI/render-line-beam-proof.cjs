// Assemble only captured Editor frames; no invented gameplay frames or skill asset edits.
const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const ROOT=path.resolve(__dirname,'../..');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
const before=path.join(ROOT,'ArtOutput/UI/LineBeamBefore'),after=path.join(ROOT,'ArtOutput/UI/LineBeamAfter');
const esc=s=>String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;');
const t=(x,y,s,size=22)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei, sans-serif" font-size="${size}" fill="#493627">${esc(s)}</text>`;
async function motion(dir,label){
 const timing=path.join(dir,'frame-times.json');if(!fs.existsSync(timing))return;
 const validation=path.join(dir,'motion-validation.json');
 if(!fs.existsSync(validation))throw Error(`Missing real-cast validation for ${label}; rerun the Unity beam preview.`);
 const proof=JSON.parse(fs.readFileSync(validation,'utf8'));
 if(!proof.passed||!proof.fired||!proof.actualHit||proof.visibleFrames<1||proof.timedOut)throw Error(`Motion capture did not prove a visible real damaging cast for ${label}: ${proof.error||'incomplete capture'}`);
 const times=JSON.parse(fs.readFileSync(timing,'utf8'));if(!times.length)return;
 if(proof.frameCount!==times.length||times.some((t,i)=>!Number.isFinite(t)||(i>0&&t<times[i-1])))throw Error(`Invalid frame timing/count for ${label}.`);
 const preview=path.join(dir,'preview-frames');fs.mkdirSync(preview,{recursive:true});
 const header=await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="540" height="60"><rect width="100%" height="100%" fill="#FFF4DB"/>${t(14,25,`气贯长虹 · ${label}`,21)}${t(14,48,'单次施放回放 · 原技能间隔与伤害保持不变',15)}</svg>`)).png().toBuffer();
 let concat='';
 for(let i=0;i<times.length;i++){
  const source=path.join(dir,'frames',`frame-${String(i).padStart(4,'0')}.png`),target=path.join(preview,`frame-${String(i).padStart(4,'0')}.png`);
  const game=await sharp(source).resize(540,960).png().toBuffer();
  await sharp({create:{width:540,height:1020,channels:4,background:'#FFF4DB'}}).composite([{input:header,left:0,top:0},{input:game,left:0,top:60}]).png().toFile(target);
  concat+=`file '${target.replace(/\\/g,'/')}'\nduration ${i+1<times.length?Math.max(.02,times[i+1]-times[i]):.35}\n`;
 }
 concat+=`file '${path.join(preview,`frame-${String(times.length-1).padStart(4,'0')}.png`).replace(/\\/g,'/')}'\n`;
 const list=path.join(preview,'recording.concat');fs.writeFileSync(list,concat);
 const run=args=>{const r=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',list,...args],{encoding:'utf8'});if(r.status!==0)throw Error(r.stderr);};
 run(['-vf','fps=20','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,'runtime-cast.mp4')]);
 run(['-filter_complex','fps=20,scale=400:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=160:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(dir,'runtime-cast.gif')]);
}
async function main(){
 if(fs.existsSync(before))await motion(before,'升级前');if(fs.existsSync(after))await motion(after,'表现升级后');
 const screens=[[before,'single-1080x1920.png','升级前 · 细红线'],[after,'single-1080x1920.png','升级后 · 单束贯穿'],[after,'triple-1080x1920.png','满级常规 · 三束'],[after,'rainbow-1080x1920.png','原满级变体 · 七色齐发']].filter(([dir,file])=>fs.existsSync(path.join(dir,file)));
 if(!screens.length)return;
 const width=28+screens.length*370,height=826,parts=[];
 let svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="100%" height="100%" fill="#E8E1CE"/>${t(28,42,'气贯长虹 · Unity 实际表现对比',30)}${t(28,76,'固定相同站位，真实敌人碰撞体和技能表；静态图冻结于施放帧，方便比较层次',20)}`;
 for(let i=0;i<screens.length;i++){
  const [dir,file,label]=screens[i],x=28+i*370;svg+=t(x,118,label,22);
  parts.push({input:await sharp(path.join(dir,file)).resize(342,608).png().toBuffer(),left:x,top:138});
 }
 svg+=t(28,791,'Editor 隔离验证 · 无持久存档变更 · 实际命中与配表不变 · 未做微信真机验证',20)+'</svg>';
 const out=fs.existsSync(after)?after:before;await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(out,'comparison.png'));
 console.log(JSON.stringify({output:out,screenCount:screens.length}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
