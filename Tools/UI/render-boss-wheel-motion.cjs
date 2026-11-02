// Design-only motion study: no scene, prefab, award weights or player save changes.
const fs=require('node:fs'), path=require('node:path'), {spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const M=require('./render-boss-wheel-design.cjs');
const {ROOT,P,rect,circle,text,rule,img,star,button,rewardCard,slots,winners,layout,CX,CY}=M;
const OUT=path.join(ROOT,'ArtOutput/UI/BossWheelDesignV2');
const FPS=20, W=540, H=960;
const clamp=x=>Math.min(1,Math.max(0,x));
const ease=x=>1-Math.pow(1-clamp(x),3);
const pop=x=>{x=clamp(x)-1;return 1+2.70158*x*x*x+1.70158*x*x;};
const lerp=(a,b,t)=>a+(b-a)*t;
const group=(content,opacity=1,transform='')=>`<g opacity="${clamp(opacity)}" transform="${transform}">${content}</g>`;
const scaleAt=(x,y,scale)=>`translate(${x} ${y}) scale(${scale}) translate(${-x} ${-y})`;
const chestFile=(open)=>`ArtOutput/UI/BossWheelDesignV2/chest-${open?'open':'closed'}.png`;
function timing(count=3,skip=false){
  const locks=skip?winners.slice(0,count).map(()=>.8):winners.slice(0,count).map((_,i)=>2.5+i*.4);
  const charge=locks.at(-1)+.05, opening=skip?.86:charge+.45, flight=opening+(skip?.13:.32), duration=skip?.29:.65, stagger=skip?.08:.18;
  const finish=flight+(count-1)*stagger+duration+.16;
  return {locks,charge,opening,flight,duration,stagger,finish,total:finish+1.15};
}
function wheelBase(){
  let a=circle(CX,CY+14,430,'#49362722')+circle(CX,CY,430,'#DBB16D','#735638',6);
  a+=circle(CX,CY,419,'#FFEDB2','#FFF7DA',5)+circle(CX,CY,402,'#EEE2C0','#C4A56D',3);
  for(let i=0;i<16;i++){
    const rad=(i+.5)*Math.PI/8;
    a+=circle(CX+Math.cos(rad)*415,CY+Math.sin(rad)*415,6.5,'#BC8A42');
    a+=`<path d="M${CX+Math.cos(rad)*266} ${CY+Math.sin(rad)*266}L${CX+Math.cos(rad)*392} ${CY+Math.sin(rad)*392}" stroke="#C7AF7F" stroke-opacity=".35" stroke-width="2"/>`;
  }
  return a+circle(CX,CY,244,'#F8F0D8','#C8B78E',3)+circle(CX,CY,228,'#FFF8E4','#E4D6B7',2);
}
const BASE=wheelBase();
function cursorAt(t,T,skip){
  if(t<.8||t>=T.charge||skip)return -1;
  if(t<2.4)return Math.floor(Math.max(0,t-.8)*20)%16;
  let previous=15,start=2.4;
  for(let i=0;i<T.locks.length;i++){
    const end=T.locks[i],target=winners[i];
    if(t<=end){const steps=(target-previous+16)%16||16;return (previous+Math.floor(ease((t-start)/(end-start))*steps))%16;}
    previous=target;start=end;
  }
  return -1;
}
function selectedCell(s,locked,current,t,T){
  let a=M.slotCell(s,locked);
  if(current&&!locked)a+=rect(s.x-3,s.y-3,106,114,23,'#FFD66635','#CA8E32',5);
  if(locked){
    const lock=T.locks[winners.indexOf(s.index)], pulse=clamp(1-(t-lock)/.25);
    if(pulse>0)a+=group(rect(s.x-6,s.y-6,112,120,24,'none','#FFF3BC',6),pulse);
  }
  return a;
}
function burst(t,T,count){
  const q=t-T.opening;
  if(q<-.25||q>1.25)return '';
  const strength=count===3?1:count===2?.8:.6;
  const aIn=clamp((q+.25)/.3),aOut=clamp(1-q/1.25),alpha=aIn*aOut*strength;
  let a=circle(540,784,215+q*18,'url(#warmGlow)');
  for(let i=0;i<12;i++){
    const r=i*Math.PI/6+q*.22,inner=66,outer=194+(i%2)*40;
    const ax=540+Math.cos(r-.055)*inner,ay=784+Math.sin(r-.055)*inner;
    const bx=540+Math.cos(r-.08)*outer,by=784+Math.sin(r-.08)*outer;
    const cx=540+Math.cos(r+.08)*outer,cy=784+Math.sin(r+.08)*outer;
    a+=`<path d="M${ax} ${ay}L${bx} ${by}L${cx} ${cy}Z" fill="#FFE39A" opacity=".68"/>`;
  }
  return group(a,alpha);
}
function sparkles(t,T,count){
  const q=t-T.opening;if(q<0||q>1.2)return '';
  let a='';
  for(let i=0;i<6+count*3;i++){
    const seed=i*2.39996,r=lerp(50,180,clamp(q*1.1))+(i%3)*12;
    const x=540+Math.cos(seed)*r,y=770+Math.sin(seed)*r-q*58;
    a+=star(x,y,5+(i%3)*3,i%2?'#C5A052':'#FFE9A7');
  }
  return group(a,clamp(q/.08)*clamp(1-q/1.2));
}
function chest(t,T,skip){
  const ready=t<.8,charged=t>=T.charge&&t<T.opening;
  const progress=clamp((t-T.charge)/(T.opening-T.charge));
  const q=t-T.opening,opened=q>=0;
  let scale=1,rotation=0,dx=0;
  if(ready)scale=1+Math.sin(t*4)*.012;
  if(charged&&!skip){scale=1+progress*.055;rotation=Math.sin(progress*38)*3.1*progress;dx=Math.sin(progress*45)*5.5*progress;}
  if(opened&&q<.32)scale=.92+.08*pop(q/.32);
  const transform=`translate(${dx} 0) rotate(${rotation} 540 884) ${scaleAt(540,895,scale)}`;
  let a='';
  if(charged)a+=group(circle(540,824,152,'url(#warmGlow)'),progress*.9);
  if(!opened)a+=group(img(chestFile(false),320,520,440,440),1,transform);
  else {
    a+=group(img(chestFile(false),320,520,440,440),1-clamp(q/.10),transform);
    a+=group(img(chestFile(true),320,520,440,440),clamp(q/.10),transform);
  }
  if(q>=-.03&&q<.19)a+=group(circle(540,790,200,'url(#revealFlash)'),Math.sin(clamp((q+.03)/.22)*Math.PI)*.66);
  return a;
}
function energyFromSlots(t,T){
  let a='';
  T.locks.forEach((when,i)=>{
    const q=(t-when-.02)/.32;if(q<0||q>1)return;
    const s=layout[winners[i]],x=s.x+50,y=s.y+50,p=ease(q);
    const px=(1-p)*(1-p)*x+2*(1-p)*p*540+p*p*540;
    const py=(1-p)*(1-p)*y+2*(1-p)*p*650+p*p*820;
    a+=`<path d="M${x} ${y}Q540 650 540 820" fill="none" stroke="#E4B54F" stroke-width="3" opacity="${(1-q)*.6}"/>`;
    a+=circle(px,py,14,'#FFE19A','#FFF9EA',3)+star(px,py,7,P.white);
  });
  return a;
}
function targetPosition(index,count){
  const w=count===1?480:count===2?374:264,gap=count===3?36:32;
  return {x:(1080-count*w-(count-1)*gap)/2+index*(w+gap)+w/2,y:1348};
}
function flightPosition(p,index,count){
  const dest=targetPosition(index,count),side=index-(count-1)/2;
  const u=1-p;
  return {x:u*u*u*540+3*u*u*p*(540+side*170)+3*u*p*p*dest.x+p*p*p*dest.x,y:u*u*u*738+3*u*u*p*475+3*u*p*p*1060+p*p*p*dest.y};
}
function prizes(t,T,count){
  if(t<T.flight)return '';
  let a=group(text(108,1273,'本次强化',32,P.ink,true,'start')+text(972,1273,`${count} 次升级`,27,P.green,false,'end'),clamp((t-T.flight)/.16));
  const bonuses=new Map();
  for(let i=0;i<count;i++){
    const s=slots[winners[i]],bonus=bonuses.get(s.name)||0;bonuses.set(s.name,bonus+1);
    const start=T.flight+i*T.stagger,q=(t-start)/T.duration;
    if(q<0)continue;
    if(q<1){
      const p=ease(q),pos=flightPosition(p,i,count),size=112+Math.sin(q*Math.PI)*24;
      for(let k=1;k<=3;k++){
        const trail=flightPosition(ease(Math.max(0,q-k*.055)),i,count);
        a+=circle(trail.x,trail.y,12-k*2,'#E3BD66',undefined,0).replace('/>',` opacity="${.32-k*.06}"/>`);
      }
      let card=rect(-size/2,-size/2+5,size,size,23,'#49362720')+rect(-size/2,-size/2,size,size,23,'#FFE4A1','#B88B43',3)+img(s.icon,-size*.39,-size*.39,size*.78,size*.78);
      a+=group(card,clamp(q/.09),`translate(${pos.x} ${pos.y}) rotate(${Math.sin(q*Math.PI)*(i%2?8:-8)})`);
    }else{
      const dest=targetPosition(i,count),settle=clamp((q-1)*T.duration/.18);
      a+=group(rewardCard(s,i,count,bonus),1,scaleAt(dest.x,1395,.9+.1*pop(settle)));
      if(settle<1)a+=group(star(dest.x+76,1333,19,'#D3A33D'),1-settle);
    }
  }
  return a;
}
function screen(t,count=3,skip=false){
  const T=timing(count,skip),ready=t<.8,opened=t>=T.opening;
  let a=`<svg xmlns="http://www.w3.org/2000/svg" width="1080" height="1920"><defs><radialGradient id="warmGlow"><stop stop-color="#FFE9A0" stop-opacity=".8"/><stop offset=".5" stop-color="#FFE9A0" stop-opacity=".4"/><stop offset="1" stop-color="#FFE9A0" stop-opacity="0"/></radialGradient><radialGradient id="revealFlash"><stop stop-color="#FFFCEE"/><stop offset=".4" stop-color="#FFF4CF" stop-opacity=".9"/><stop offset="1" stop-color="#FFF4CF" stop-opacity="0"/></radialGradient></defs>`;
  a+=img('ArtOutput/UI/Battle/gameplay-1080x1920.png',0,0,1080,1920)+rect(0,0,1080,1920,0,'#263C2C9C');
  a+=rect(60,219,960,1540,44,'#2A211B30')+rect(60,206,960,1540,44,P.paper,P.ink,7);
  a+=rect(365,178,350,58,26,'#DDE7C4','#A6B68A',2.5)+text(540,218,'首领已击败',29,P.green,true);
  a+=star(286,290,18)+text(540,307,'首领奖励',64,P.ink,true)+star(794,290,18)+text(540,356,'随机获得 1–3 次技能升级',32,P.muted);
  a+=BASE+burst(t,T,count);
  const cursor=cursorAt(t,T,skip);
  layout.forEach(s=>a+=selectedCell(s,winners.slice(0,count).some((v,i)=>v===s.index&&t>=T.locks[i]),s.index===cursor,t,T));
  a+=energyFromSlots(t,T)+chest(t,T,skip)+sparkles(t,T,count);
  const main=opened?(count===3?'三重强化！':count===2?'双重强化！':'技能强化！'):t>=T.charge?'惊喜即将开启':ready?'幸运转一转':'好运转动中';
  a+=text(540,975,main,opened?46:40,opened?'#A46F23':P.ink,true);
  a+=text(540,1018,opened?`收获 ${count} 次技能升级`:t>=T.charge?'准备迎接新的力量':ready?'闭合的宝箱，藏着新的力量':'即将揭晓强化奖励',28,P.muted);
  if(!opened){
    a+=rule(176,1302,728)+circle(349,1345,9,'#F2CF83','#B79453',1.5)+text(374,1356,'输出技能',29,P.muted,false,'start');
    a+=circle(613,1345,9,'#B8CC92','#839C6A',1.5)+text(638,1356,'被动增益',29,P.muted,false,'start');
    a+=text(540,1414,'亮起的技能，每次提升 1 级',34,P.ink,true)+text(540,1460,'仅从本局已拥有的可升级技能中抽取',27,P.muted);
    if(ready){a+=rect(409,1494,30,30,8,P.white,'#AFA081',2.5)+text(462,1519,'跳过动画',28,P.muted,false,'start')+button('开始转盘');}
    else a+=text(540,1635,t>=T.charge?'宝箱开启中…':'强化抽取中…',38,P.muted,true);
  }else{
    a+=prizes(t,T,count);
    if(t>=T.finish)a+=group(button('继续战斗'),clamp((t-T.finish)/.18));
    else a+=text(540,1635,'正在收取强化奖励…',34,P.muted,true);
  }
  a+=text(540,1718,ready?'击败首领奖励 · 无需消耗':t>=T.finish?'技能已提升，继续清扫字灵！':'',27,P.muted);
  return a+'</svg>';
}
async function renderStill(t,file,count=3){
  const svg=screen(t,count);
  fs.writeFileSync(path.join(OUT,'source',file+'.svg'),svg);
  await sharp(Buffer.from(svg)).png().toFile(path.join(OUT,file+'-1080x1920.png'));
}
async function main(){
  fs.mkdirSync(path.join(OUT,'frames'),{recursive:true});fs.mkdirSync(path.join(OUT,'source'),{recursive:true});
  const T=timing(),frames=Math.ceil(T.total*FPS);
  console.log(`Rendering ${frames} frames at ${W}x${H}, ${FPS}fps.`);
  for(let i=0;i<frames;i++){
    await sharp(Buffer.from(screen(i/FPS))).resize(W,H).png().toFile(path.join(OUT,'frames',`frame-${String(i).padStart(4,'0')}.png`));
    if(i%30===0)console.log(`Rendered ${i}/${frames}`);
  }
  const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
  const sequence=path.join(OUT,'frames','frame-%04d.png');
  let run=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-framerate',String(FPS),'-i',sequence,'-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(OUT,'wheel-reveal-preview.mp4')],{encoding:'utf8'});
  if(run.status!==0)throw new Error(run.stderr);
  run=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-framerate',String(FPS),'-i',sequence,'-filter_complex','fps=12.5,scale=400:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=160:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(OUT,'wheel-reveal-preview.gif')],{encoding:'utf8'});
  if(run.status!==0)throw new Error(run.stderr);
  const keys=[['01-chest-closed',.3,'闭合宝箱 · 等待抽取'],['02-chest-charge',T.opening-.13,'光标停住 · 宝箱蓄力'],['03-chest-open',T.opening+.2,'开箱闪光 · 强化揭晓'],['04-rewards-landed',T.finish+.3,'技能飞入 · 逐项落位']];
  for(const[file,t]of keys)await renderStill(t,file);
  await renderStill(timing(1).finish+.3,'result-single',1);await renderStill(timing(2).finish+.3,'result-pair',2);
  const width=1576,height=890,parts=[];
  let board=`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">${rect(0,0,width,height,0,'#E8E1CE')}${text(28,42,'首领奖励转盘 · 闭合 → 开箱 → 奖励落位',30,P.ink,true,'start')}${text(28,76,'两张宝箱图 + 缩放 / 轻晃 / 暖光 / 技能飞入；保留原有 16 格抽取结构',22,P.muted,false,'start')}`;
  for(let i=0;i<keys.length;i++){
    const x=28+i*387;
    board+=text(x,116,keys[i][2],23,P.ink,true,'start');
    parts.push({input:await sharp(path.join(OUT,keys[i][0]+'-1080x1920.png')).resize(360,640).png().toBuffer(),left:x,top:136});
  }
  board+=text(28,820,'完整演出约 4.5 秒（不含预览起止停留） · 跳过动画保留约 0.8 秒的开箱与落位反馈',23,P.ink,false,'start');
  board+=text(28,858,'设计动效预览，非 Unity 运行截图 · GIF / MP4 无声 · 本轮未接入工程',21,P.muted,false,'start')+'</svg>';
  await sharp(Buffer.from(board)).composite(parts).png().toFile(path.join(OUT,'storyboard.png'));
  fs.writeFileSync(path.join(OUT,'motion-spec.json'),JSON.stringify({version:2,designSize:[1080,1920],previewSize:[W,H],fps:FPS,timing:T,skipTiming:timing(3,true),awardCount:3,closedSprite:'chest-closed.png',openSprite:'chest-open.png',assetRect:{x:320,y:520,width:440,height:440},pivot:{x:540,y:895},sound:'Design proposal only: existing wheel tick, latch, soft open impact, staggered upgrade chimes. This preview is silent.',integration:'Review-only. Keep fixed winning indices and one-time award callback; view animation does not change reward probabilities.'},null,2));
  console.log(JSON.stringify({folder:OUT,frames,duration:T.total,fullAnimationAfterClick:T.finish-.8}));
}
module.exports={screen,timing};
if(require.main===module)main().catch(error=>{console.error(error);process.exitCode=1;});
