// Offline motion concept. Does not import assets or alter Unity gameplay.
const fs=require('node:fs'),path=require('node:path'),{spawn,spawnSync}=require('node:child_process');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtOutput/VFX/PlayerActionReferenceV1');
fs.mkdirSync(path.join(out,'Parts'),{recursive:true});fs.mkdirSync(path.join(out,'font-cache'),{recursive:true});
const fontconfig=path.join(out,'fontconfig.xml');fs.writeFileSync(fontconfig,`<?xml version="1.0"?><!DOCTYPE fontconfig SYSTEM "fonts.dtd"><fontconfig><dir>C:/Windows/Fonts</dir><cachedir>${path.join(out,'font-cache').replace(/\\/g,'/')}</cachedir></fontconfig>`);process.env.FONTCONFIG_FILE=fontconfig;
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe',ffprobe='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffprobe.exe';
const W=960,H=720,FPS=30,DURATION=13;
const esc=s=>s.replace(/&/g,'&amp;').replace(/</g,'&lt;');
const text=(x,y,s,size=22,fill='#443728',weight=400)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="${fill}" font-weight="${weight}">${esc(s)}</text>`;
const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v));
const smooth=v=>{v=clamp(v);return v*v*(3-2*v);};
let parts;
// Isolate the main cutout in each grid cell; neighboring-cell fragments are not rig parts.
async function mainCutout(cell){
 const {data,info}=await sharp(cell).ensureAlpha().raw().toBuffer({resolveWithObject:true}),n=info.width*info.height,labels=new Int32Array(n),queue=new Int32Array(n);
 let id=0,largest=0,largestSize=0;
 for(let start=0;start<n;start++){
  if(labels[start]||data[start*4+3]<=8)continue;id++;let head=0,tail=0;queue[tail++]=start;labels[start]=id;
  while(head<tail){const p=queue[head++],x=p%info.width,y=Math.floor(p/info.width);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const nx=x+dx,ny=y+dy;if(nx<0||nx>=info.width||ny<0||ny>=info.height)continue;const q=ny*info.width+nx;if(!labels[q]&&data[q*4+3]>8){labels[q]=id;queue[tail++]=q;}}}
  if(tail>largestSize){largestSize=tail;largest=id;}
 }
 const keep=new Uint8Array(n);for(let p=0;p<n;p++)if(labels[p]===largest){const x=p%info.width,y=Math.floor(p/info.width);for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const nx=x+dx,ny=y+dy;if(nx>=0&&nx<info.width&&ny>=0&&ny<info.height)keep[ny*info.width+nx]=1;}}
 for(let p=0;p<n;p++)if(!keep[p])data[p*4+3]=0;
 return sharp(data,{raw:info}).png().toBuffer();
}
function img(part,x,y,h,more='') {const w=part.width/part.height*h;return `<image href="${part.uri}" x="${x}" y="${y}" width="${w}" height="${h}" ${more}/>`;}
function state(t){
 if(t<2)return {row:0,x:395,y:510,moving:false,shooting:false,label:'站立：轻呼吸，枪口保持稳定',move:1};
 if(t<5)return {row:1,x:395+(t-2)*42,y:510,moving:true,shooting:false,label:'移动：一套跑步循环',move:1};
 if(t<9)return {row:1,x:521-(t-5)*20,y:510,moving:true,shooting:true,label:'边走边射：跑步上叠加枪械回弹',move:-1};
 if(t<11)return {row:2,x:441+(t-9)*17,y:510-(t-9)*32,moving:true,shooting:true,label:'转向：换背面姿态，沿用相同步态',move:1};
 return {row:0,x:475,y:446,moving:false,shooting:false,label:'停止：回到待机，不增加额外动作',move:1};
}
function pose(t,s){
 const stride=t*Math.PI*2.6*s.move,bob=s.moving?Math.abs(Math.sin(stride))*2.2:Math.sin(t*Math.PI*1.2)*.8;
 const beat=((t-5)% .48+.48)% .48,shot=s.shooting?Math.exp(-beat*25):0;
 const row=parts[s.row],bodyH=139,bw=row[0].width/row[0].height*bodyH,legH=45,legW=row[1].width/row[1].height*legH;
 let result=`<ellipse cx="0" cy="5" rx="42" ry="11" fill="#695134" opacity=".16"/>`;
 const leg=(i,sign)=>{const p=stride+(i===1?0:Math.PI),pace=s.row===2?.8:s.move<0?.55:1,a=s.moving?Math.sin(p)*14*pace:0,lift=s.moving?Math.max(0,Math.sin(p))*3:0;return `<g transform="translate(${sign*17},${-43-lift}) rotate(${a})">${img(row[i],-legW*.5,-5,legH+5)}</g>`;};
 const gunH=s.row===2?88:55,gunW=row[3].width/row[3].height*gunH,gx=-bw*.36-shot*5+(s.row===2?48:0),gy=-103-bob+shot*.7-(s.row===2?30:0),angle=s.row===2?-13:Math.sin(t*1.8)*.5;
 const px=gx+gunW*.34,py=gy+gunH*.61;
 const gun=`<g transform="rotate(${angle},${px},${py})">${img(row[3],gx,gy,gunH)}</g>`;
 result+=leg(1,-1)+leg(2,1);
 if(s.row===2)result+=gun;
 result+=`<g transform="translate(0,${-bob}) rotate(${s.moving?-1.2:0},0,-50)">${img(row[0],-bw*.5,-43-bodyH,bodyH)}</g>`;
 if(s.row!==2)result+=gun;
 const mx=gx+gunW*.985,my=gy+gunH*(s.row===2?.1:.26),rad=angle*Math.PI/180;
 const muzzle={x:px+(mx-px)*Math.cos(rad)-(my-py)*Math.sin(rad),y:py+(mx-px)*Math.sin(rad)+(my-py)*Math.cos(rad)};
 const dir=s.row===2?-.62:0;
 if(s.shooting&&beat<.065){const size=7+11*(1-beat/.065);result+=`<g transform="translate(${muzzle.x},${muzzle.y}) rotate(${dir*180/Math.PI})"><path d="M -2 -3 L ${size*.45} -${size*.55} L ${size*.6} -2 L ${size} 0 L ${size*.55} 3 L ${size*.4} ${size*.55} L -2 3 Z" fill="#f9c743" stroke="#c66827" stroke-width="1.5"/><path d="M 0 -2 L ${size*.6} 0 L 0 2 Z" fill="#fff7cb"/></g>`;}
 return {svg:result,muzzle,dir,beat};
}
function frame(t){
 const s=state(t),hero=pose(t,s),phase=t<2?0:t<5?1:t<11?2:0;
 let svg=`<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}"><defs><pattern id="sand" width="70" height="66" patternUnits="userSpaceOnUse"><rect width="70" height="66" fill="#eadbb5"/><path d="M 8 17 l 5 -1 M 39 48 l 4 1 M 61 10 l 3 1" stroke="#bfa879" stroke-width="2" opacity=".24"/></pattern><clipPath id="arena"><rect x="32" y="150" width="896" height="408" rx="22"/></clipPath></defs><rect width="960" height="720" fill="#f5ecda"/>`;
 svg+=text(34,51,'弹仔 · 简化动作参考',32,'#3c3024',700)+text(34,89,'两个基础循环 ＋ 一次开枪回弹',21,'#75644b');
 svg+=`<rect x="746" y="30" width="180" height="40" rx="20" fill="#e8ddc3"/>`+text(770,57,'离线动作样片',18,'#79664c');
 svg+=`<rect x="32" y="150" width="896" height="408" rx="22" fill="url(#sand)" stroke="#cdbb94" stroke-width="2"/><g clip-path="url(#arena)"><path d="M 32 441 Q 130 393 208 458 L 218 558 L 32 558 Z" fill="#9eac66" opacity=".55"/><path d="M 756 150 L 928 150 L 928 219 Q 839 248 783 209 Z" fill="#9eac66" opacity=".55"/>`;
 for(const [x,y,r] of [[146,285,8],[239,530,6],[687,477,8],[872,360,5],[560,220,5]])svg+=`<ellipse cx="${x}" cy="${y}" rx="${r}" ry="${r*.45}" fill="#b5a080" opacity=".3"/>`;
 svg+=`<g transform="translate(803,421)"><ellipse cy="18" rx="30" ry="8" fill="#695134" opacity=".13"/><path d="M -14 20 L -6 -34 L 6 -34 L 14 20" fill="#a98758"/><circle cy="-39" r="25" fill="#d9c299" stroke="#8d6c45" stroke-width="4"/><circle cy="-39" r="13" fill="none" stroke="#a16d4e" stroke-width="3"/><circle cy="-39" r="4" fill="#a16d4e"/></g>`;
 if(s.shooting){const elapsed=((t-5)% .48+.48)% .48,dist=elapsed*980;const mx=s.x+hero.muzzle.x,my=s.y+hero.muzzle.y;if(dist<235){const x=mx+Math.cos(hero.dir)*dist,y=my+Math.sin(hero.dir)*dist;svg+=`<path d="M ${x-12*Math.cos(hero.dir)} ${y-12*Math.sin(hero.dir)} L ${x} ${y}" stroke="#b78832" stroke-width="4" stroke-linecap="round"/><circle cx="${x}" cy="${y}" r="2.4" fill="#ffe2a0"/>`;}}
 svg+=`<g transform="translate(${s.x},${s.y})">${hero.svg}</g></g>`;
 svg+=`<rect x="49" y="165" width="676" height="43" rx="13" fill="#fff7e7" fill-opacity=".87"/>`+text(65,194,s.label,21,'#5c4932');
 const names=['① 待机','② 跑步','③ 开枪叠加'],notes=['轻微呼吸','左右脚交替','枪与双手短回弹'];
 for(let i=0;i<3;i++){const x=34+i*301,active=phase===i;svg+=`<rect x="${x}" y="582" width="288" height="70" rx="15" fill="${active?'#dac293':'#ebe0c8'}" stroke="${active?'#a78345':'#d9ccb1'}" stroke-width="${active?2:1}"/>`+text(x+17,611,names[i],22,'#4b3c29',active?700:400)+text(x+17,636,notes[i],16,'#7b6749');}
 svg+=text(35,680,'边走边射是组合，不必再画一整套动作。',20,'#5b4a34')+text(35,705,'三向草图与动作节奏示意 · 非 Unity 实装 · 非最终角色美术',14,'#938168');
 svg+=`<rect x="32" y="126" width="896" height="4" rx="2" fill="#dfd0ad"/><rect x="32" y="126" width="${896*clamp(t/DURATION)}" height="4" rx="2" fill="#a8864d"/></svg>`;
 return Buffer.from(svg);
}
async function prepare(){
 const source=path.join(out,'Sources/parts-concept.png'),m=await sharp(source).metadata();parts=[];
 if(!m.hasAlpha||m.width%4||m.height%3)throw Error('Expected transparent 4×3 sheet');
 const cw=m.width/4,ch=m.height/3;
 for(let row=0;row<3;row++){parts[row]=[];for(let col=0;col<4;col++){
  const cell=await sharp(source).extract({left:col*cw,top:row*ch,width:cw,height:ch}).png().toBuffer();
  const isolated=await mainCutout(cell);
  const buffer=await sharp(isolated).trim({background:'#00000000',threshold:8}).resize({width:320,height:320,fit:'inside',withoutEnlargement:true}).png().toBuffer();
  const info=await sharp(buffer).metadata();fs.writeFileSync(path.join(out,'Parts',`view-${row}-part-${col}.png`),buffer);parts[row][col]={width:info.width,height:info.height,uri:'data:image/png;base64,'+buffer.toString('base64')};
 }}
 const stats=await sharp(source).stats();return {sourceWidth:m.width,sourceHeight:m.height,sourceHasAlpha:m.hasAlpha,sourceOpaque:stats.isOpaque};
}
async function main(){
 const source=await prepare();
 const at=[1,3.7,7.3,10.1],preview=[];
 for(let i=0;i<at.length;i++){const b=await sharp(frame(at[i])).png().toBuffer();fs.writeFileSync(path.join(out,`preview-${i}.png`),b);preview.push({input:await sharp(b).resize(480,360).png().toBuffer(),left:(i%2)*480,top:Math.floor(i/2)*360});}
 await sharp({create:{width:960,height:720,channels:4,background:'#f5ecda'}}).composite(preview).png().toFile(path.join(out,'contact-sheet.png'));
 if(process.argv.includes('--preview-only')){console.log(JSON.stringify({preview:true,...source}));return;}
 const video=path.join(out,'reference.mp4'),child=spawn(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','image2pipe','-vcodec','png','-framerate',String(FPS),'-i','pipe:0','-an','-c:v','libx264','-preset','veryfast','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',video],{windowsHide:true});let errors='';child.stderr.on('data',b=>errors+=b);child.stdin.on('error',()=>{});
 const completed=new Promise((resolve,reject)=>{child.on('error',reject);child.on('exit',code=>code===0?resolve():reject(Error(errors)));});
 for(let i=0;i<DURATION*FPS;i++){const png=await sharp(frame(i/FPS)).png().toBuffer();if(!child.stdin.write(png))await new Promise(resolve=>child.stdin.once('drain',resolve));if(i%90===0)console.log('Rendered '+i+'/'+DURATION*FPS+' frames');}
 child.stdin.end();await completed;
 const probe=spawnSync(ffprobe,['-v','error','-show_entries','format=duration','-of','default=noprint_wrappers=1:nokey=1',video],{encoding:'utf8',windowsHide:true});if(probe.status!==0)throw Error(probe.stderr);const seconds=Number(probe.stdout.trim());if(Math.abs(seconds-DURATION)>.05)throw Error('Unexpected video length');
 const result={passed:true,type:'offline-motion-concept-not-unity-capture',...source,views:3,partsPerView:4,frames:DURATION*FPS,fps:FPS,seconds,videoBytes:fs.statSync(video).size,baseLoops:['idle','walk'],overlay:['aim/view selection','gun recoil'],addedRuntimeAssets:0,note:'Imagegen concept art and deterministic part animation. No production art, physics or Unity runtime changes.'};fs.writeFileSync(path.join(out,'proof.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
