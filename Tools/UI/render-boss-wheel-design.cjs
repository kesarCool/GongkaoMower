// Review-only BossWheelPanel design. Uses the project's real skill icons and one font family.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const ROOT = path.resolve(__dirname, '../..');
const OUT = path.join(ROOT, 'ArtOutput/UI/BossWheelDesignV1');
const P = { ink:'#493627', muted:'#76634C', paper:'#FFF4DB', white:'#FFF9EA', line:'#D7C8A6', gold:'#F6B94E', green:'#4D7651', sage:'#E7EDD5' };
const esc = value => String(value).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/"/g,'&quot;');
const rect = (x,y,w,h,r,fill,stroke='none',sw=0) => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const circle = (x,y,r,fill,stroke='none',sw=0) => `<circle cx="${x}" cy="${y}" r="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const text = (x,y,label,size=32,color=P.ink,bold=false,anchor='middle') => `<text x="${x}" y="${y}" font-family="Microsoft YaHei, sans-serif" font-size="${size}" fill="${color}" font-weight="${bold?700:400}" text-anchor="${anchor}">${esc(label)}</text>`;
const rule = (x,y,w,color=P.line) => `<path d="M${x} ${y}h${w}" fill="none" stroke="${color}" stroke-width="2"/>`;
const uriCache = new Map();
const uri = file => { if (!uriCache.has(file)) uriCache.set(file,'data:image/png;base64,' + fs.readFileSync(path.join(ROOT,file)).toString('base64')); return uriCache.get(file); };
const img = (file,x,y,w,h,extra='') => `<image x="${x}" y="${y}" width="${w}" height="${h}" href="${uri(file)}" preserveAspectRatio="xMidYMid meet" ${extra}/>`;
const star = (x,y,size=13,color=P.gold) => `<path d="M${x} ${y-size}Q${x+size*.2} ${y-size*.2} ${x+size} ${y}Q${x+size*.2} ${y+size*.2} ${x} ${y+size}Q${x-size*.2} ${y+size*.2} ${x-size} ${y}Q${x-size*.2} ${y-size*.2} ${x} ${y-size}Z" fill="${color}"/>`;
const iconDir = 'Assets/Res/image/Game/SkillIcon';
const icons = new Map();
for (const file of fs.readdirSync(path.join(ROOT,iconDir)).filter(x=>x.endsWith('.png.meta'))) {
  const guid = fs.readFileSync(path.join(ROOT,iconDir,file),'utf8').match(/^guid: (\w+)/m)?.[1];
  if (guid) icons.set(guid, `${iconDir}/${file.slice(0,-5)}`);
}
function skill(file,level,passive=false) {
  const source = `Assets/ScriptableObject/Skill/${file}`;
  const yaml = fs.readFileSync(path.join(ROOT,source),'utf8');
  const raw = yaml.match(/displayName:\s*(.+)/)[1].trim();
  const name = raw.startsWith('"') ? JSON.parse(raw) : raw;
  const icon = icons.get(yaml.match(/icon:.*guid: (\w+)/)[1]);
  if (!icon) throw new Error(`Missing icon: ${file}`);
  return {name,icon,level,passive,source};
}
const pool = [
  skill('ActiveSkill/SkillDef_AutoProjectileSword.asset',2),
  skill('ActiveSkill/SkillDef_OrbitingBlades.asset',3),
  skill('ActiveSkill/SkillDef_ThrowGrenade.asset',1),
  skill('PassiveSkill/PassiveMaxHp.asset',1,true),
  skill('ActiveSkill/SkillDef_LineBeam.asset',2),
  skill('PassiveSkill/PassiveRegen.asset',2,true),
  skill('PassiveSkill/PassiveCritRate.asset',2,true)
];
const order = [0,1,2,3,4,5,6,0,1,3,2,6,4,5,1,0];
const slots = order.map(i=>pool[i]);
const winners = [0,3,11];
const CX=540, CY=810, R=356, SLOT_W=100, SLOT_H=108;
const layout = slots.map((s,i)=> {
  // Deliberately follows the current BossWheelPanel math so existing slot indices are preserved.
  const angle = (90 + 360*i/slots.length)*Math.PI/180;
  return {...s,index:i,x:CX+Math.cos(angle)*R-SLOT_W/2,y:CY-Math.sin(angle)*R-SLOT_H/2,width:SLOT_W,height:SLOT_H};
});
function slotCell(s,selected) {
  const {x,y}=s;
  let a='';
  if(selected) a+=rect(x-7,y-7,SLOT_W+14,SLOT_H+14,25,'#FFD77880');
  a+=rect(x,y+5,SLOT_W,SLOT_H,20,'#4936271C');
  a+=rect(x,y,SLOT_W,SLOT_H,20,selected?'#FFD47A':s.passive?P.sage:P.white,selected?'#9B6B27':s.passive?'#A9BB8A':'#CDBB98',selected?5:2.5);
  a+=img(s.icon,x+12,y+7,76,70);
  a+=rect(x+13,y+77,74,26,9,selected?'#FFF0B7':s.passive?'#F2F5E5':'#F6EEDB');
  a+=text(x+50,y+98,`Lv.${s.level}`,25,P.ink,true);
  if(selected) a+=circle(x+SLOT_W-4,y+4,15,'#A97A2E',P.white,2)+`<path d="M${x+SLOT_W-12} ${y+4}l6 6 10-12" fill="none" stroke="#FFF9EA" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>`;
  return a;
}
function wheel(state,count) {
  const chosen=state==='result'?new Set(winners.slice(0,count)):new Set(state==='spinning'?[6]:[]);
  let a=circle(CX,CY+14,430,'#49362722')+circle(CX,CY,430,'#DBB16D','#735638',6);
  a+=circle(CX,CY,419,'#FFEDB2','#FFF7DA',5)+circle(CX,CY,402,'#EEE2C0','#C4A56D',3);
  for(let i=0;i<16;i++) {
    const rad=(i+.5)*2*Math.PI/16;
    a+=circle(CX+Math.cos(rad)*415,CY+Math.sin(rad)*415,6.5,'#BC8A42');
    const x=CX+Math.cos(rad)*266,y=CY+Math.sin(rad)*266;
    a+=`<path d="M${x} ${y}L${CX+Math.cos(rad)*392} ${CY+Math.sin(rad)*392}" stroke="#C7AF7F" stroke-opacity=".35" stroke-width="2"/>`;
  }
  a+=circle(CX,CY,244,'#F8F0D8','#C8B78E',3)+circle(CX,CY,228,'#FFF8E4','#E4D6B7',2);
  a+=`<g opacity=".35">${star(383,707,18,'#A3B881')}${star(712,847,20,'#C3A362')}${star(672,650,12,'#A3B881')}</g>`;
  a+=img('ArtOutput/UI/BossWheelDesignV1/reward-chest-source.png',352,560,376,376);
  a+=text(CX,968,state==='result'?'强化完成':state==='spinning'?'好运转动中':'幸运转一转',40,P.ink,true);
  a+=text(CX,1013,state==='result'?`共获得 ${count} 次升级`:state==='spinning'?'即将揭晓强化奖励':'准备收获新的力量',28,P.muted);
  for(const s of layout) a+=slotCell(s,chosen.has(s.index));
  return a;
}
function button(label) {
  return rect(215,1570,650,112,34,'#9C733F')+rect(215,1561,650,112,34,P.gold,P.ink,5)+`<path d="M249 1574H831" stroke="#FFE3A1" stroke-width="5" stroke-linecap="round"/>`+text(540,1635,label,45,P.ink,true);
}
function rewardCard(slot,index,count,bonus=0) {
  const width=count===1?480:count===2?374:264;
  const gap=count===3?36:32;
  const x=(1080-count*width-(count-1)*gap)/2+index*(width+gap), y=1300;
  let a=rect(x,y+5,width,190,25,'#49362712')+rect(x,y,width,190,25,slot.passive?'#EEF2DE':'#FFF9EA',slot.passive?'#B4C69A':'#D7BE89',2.5);
  a+=img(slot.icon,x+(width-82)/2,y+10,82,76);
  a+=text(x+width/2,y+123,slot.name,32,P.ink,true);
  a+=text(x+width/2,y+167,`Lv.${slot.level+bonus} → Lv.${slot.level+bonus+1}`,30,P.green,true);
  return a;
}
function screen(state='ready',count=3) {
  let a=`<svg xmlns="http://www.w3.org/2000/svg" width="1080" height="1920" viewBox="0 0 1080 1920">`;
  a+=img('ArtOutput/UI/Battle/gameplay-1080x1920.png',0,0,1080,1920);
  a+=rect(0,0,1080,1920,0,'#263C2C9C');
  a+=rect(60,219,960,1540,44,'#2A211B30');
  a+=rect(60,206,960,1540,44,P.paper,P.ink,7);
  a+=rect(365,178,350,58,26,'#DDE7C4','#A6B68A',2.5)+text(540,218,'首领已击败',29,P.green,true);
  a+=star(286,290,18)+text(540,307,'首领奖励',64,P.ink,true)+star(794,290,18);
  a+=text(540,356,'随机获得 1–3 次技能升级',32,P.muted);
  a+=wheel(state,count);
  if(state==='result') {
    a+=text(108,1273,'本次强化',32,P.ink,true,'start')+text(972,1273,`${count} 次升级`,27,P.green,false,'end');
    const bonuses=new Map();
    winners.slice(0,count).forEach((idx,i)=>{const s=slots[idx],b=bonuses.get(s.name)||0; a+=rewardCard(s,i,count,b);bonuses.set(s.name,b+1);});
    a+=button('继续战斗');
    a+=text(540,1718,'技能已提升，继续清扫字灵！',27,P.muted);
  } else {
    a+=rule(176,1302,728);
    a+=circle(349,1345,9,'#F2CF83','#B79453',1.5)+text(374,1356,'输出技能',29,P.muted,false,'start');
    a+=circle(613,1345,9,'#B8CC92','#839C6A',1.5)+text(638,1356,'被动增益',29,P.muted,false,'start');
    a+=text(540,1414,'亮起的技能，每次提升 1 级',34,P.ink,true);
    a+=text(540,1460,'仅从本局已拥有的可升级技能中抽取',27,P.muted);
    if(state==='ready') {
      a+=rect(409,1494,30,30,8,P.white,'#AFA081',2.5)+text(462,1519,'跳过动画',28,P.muted,false,'start');
      a+=button('开始转盘');
      a+=text(540,1718,'击败首领奖励 · 无需消耗',27,P.muted);
    } else {
      a+=rect(215,1561,650,112,34,'#F2D08B','#C0A16D',3)+text(540,1635,'强化抽取中…',41,'#8E754B',true);
      a+=text(540,1718,'高亮依次移动，抽中后保留金色标记',27,P.muted);
    }
  }
  return a+'</svg>';
}
async function main() {
  fs.mkdirSync(path.join(OUT,'source'),{recursive:true});
  const outputs=[['wheel-ready','ready',3],['wheel-spinning','spinning',3],['wheel-result','result',3],['wheel-result-single','result',1],['wheel-result-pair','result',2]];
  for(const [file,state,count] of outputs) {
    const svg=screen(state,count);
    fs.writeFileSync(path.join(OUT,'source',`${file}.svg`),svg);
    const image=await sharp(Buffer.from(svg)).png().toBuffer();
    await sharp(image).toFile(path.join(OUT,`${file}-1080x1920.png`));
    await sharp(image).resize(360,640).png().toFile(path.join(OUT,`${file}-360x640.png`));
  }
  const width=1220,height=1256,parts=[];
  let board=`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">${rect(0,0,width,height,0,'#E8E1CE')}${text(34,49,'首领奖励转盘 · 设计稿 V1',34,P.ink,true,'start')}${text(34,83,'16 格技能灯环 / 沿用现有技能图标 / 奶油纸面与暖金点缀',22,P.muted,false,'start')}`;
  for(const [i,label,file] of [[0,'抽奖前 · 保留跳过动画','wheel-ready'],[1,'奖励揭晓 · 支持 1–3 次升级','wheel-result']]) {
    const x=34+i*592;
    board+=text(x,125,label,26,P.ink,true,'start');
    parts.push({input:await sharp(path.join(OUT,`${file}-1080x1920.png`)).resize(560,996).png().toBuffer(),left:x,top:146});
  }
  board+=text(34,1189,'金色勾选 = 抽中项　　浅绿底 = 被动技能　　技能图标保持正向',24,P.ink,false,'start');
  board+=text(34,1226,'1080 × 1920 设计基准 · 本轮为效果图，尚未接入 Unity',22,P.muted,false,'start')+'</svg>';
  await sharp(Buffer.from(board)).composite(parts).png().toFile(path.join(OUT,'overview.png'));
  fs.writeFileSync(path.join(OUT,'layout-spec.json'),JSON.stringify({version:1,designSize:[1080,1920],panel:{x:60,y:206,width:960,height:1540},wheel:{cx:CX,cy:CY,radius:R,slotCount:16,slotWidth:SLOT_W,slotHeight:SLOT_H,outerRadius:430},fontFamily:'Microsoft YaHei',colors:P,button:{x:215,y:1561,width:650,height:112},slots:layout,exampleWinningIndices:winners,implementation:'Design only. Existing weighted selection and award timing are unchanged.'},null,2));
  const checks=[];
  const check=(name,pass)=>{checks.push({name,pass});if(!pass)throw new Error(name);};
  check('Sixteen slots match the scene controller',layout.length===16);
  check('Slot cards stay inside the paper panel',layout.every(s=>s.x>60&&s.y>206&&s.x+s.width<1020&&s.y+s.height<1746));
  check('Adjacent slot rectangles do not overlap',layout.every((s,i)=>{const t=layout[(i+1)%16];return s.x+s.width<=t.x||t.x+t.width<=s.x||s.y+s.height<=t.y||t.y+t.height<=s.y;}));
  check('All skill icons are actual project assets',layout.every(s=>fs.existsSync(path.join(ROOT,s.icon))));
  check('Reward examples use valid slot indices',winners.every(i=>i>=0&&i<16));
  check('One, two and three reward layouts rendered',outputs.filter(x=>x[1]==='result').length===3);
  fs.writeFileSync(path.join(OUT,'design-checks.json'),JSON.stringify(checks,null,2));
  console.log(JSON.stringify({output:OUT,checks:checks.length,passed:checks.every(x=>x.pass)}));
}
module.exports = {ROOT,P,rect,circle,text,rule,img,star,slotCell,button,rewardCard,pool,slots,winners,layout,CX,CY,R};
if (require.main === module) main().catch(error=>{console.error(error);process.exitCode=1;});
