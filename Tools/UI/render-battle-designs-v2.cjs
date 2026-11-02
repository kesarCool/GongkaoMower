// Review-only V2: energy orbs, unobstructed battle HUD, standalone defeat, centered revive countdown.
const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const ROOT=path.resolve(__dirname,'../..'),OUT=path.join(ROOT,'ArtOutput/UI/BattleDesignV2');
const P={ink:'#493627',muted:'#76634C',weak:'#95836B',paper:'#FFF4DB',surface:'#FFF9EA',line:'#D7C8A6',gold:'#F6B94E',green:'#4D7651',mint:'#D9E5BE',red:'#C97159',teal:'#4D9092'};
const esc=s=>String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/"/g,'&quot;');
const rect=(x,y,w,h,r,fill,stroke='none',sw=0)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const text=(x,y,s,size=38,color=P.ink,bold=false,anchor='start',extra='')=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei, sans-serif" font-size="${size}" fill="${color}" font-weight="${bold?700:400}" text-anchor="${anchor}" ${extra}>${esc(s)}</text>`;
const center=(x,y,w,h,s,size=38,color=P.ink,bold=true)=>text(x+w/2,y+h/2+size*.35,s,size,color,bold,'middle');
const rule=(x,y,w,c=P.line)=>`<path d="M${x} ${y}h${w}" stroke="${c}" stroke-width="2"/>`;
const circle=(x,y,r,fill,stroke='none',sw=0)=>`<circle cx="${x}" cy="${y}" r="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const uriCache=new Map(),assets=new Map(),requests=new Map(),nineCache=new Map();
for(const phase of ['home','character','home-pages','dialogs','backpack'])for(const a of JSON.parse(fs.readFileSync(path.join(ROOT,`Tools/UI/${phase}-assets.json`),'utf8')).assets)assets.set(path.basename(a.path,path.extname(a.path)),a);
for(const key of ['skill_slot_base_9slice','skill_slot_passive_9slice','skill_slot_empty_9slice','skill_level_badge_9slice'])assets.set(key,{path:`Assets/Res/image/Game/select/${key}.png`,border:[24,24,24,24]});
function uri(file){if(!uriCache.has(file))uriCache.set(file,'data:image/png;base64,'+fs.readFileSync(path.join(ROOT,file)).toString('base64'));return uriCache.get(file);}
const image=(file,x,y,w,h,extra='')=>`<image x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="xMidYMid meet" href="${uri(file)}" ${extra}/>`;
function asset(key,x,y,w,h){
 const a=assets.get(key);if(!a)throw Error('Missing asset '+key);
 if(a.border.every(n=>!n))return image(a.path,x,y,w,h);
 w=Math.round(w);h=Math.round(h);const token=`__NINE_${key}_${w}_${h}__`;requests.set(token,{a,w,h});
 return `<image x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="none" href="${token}"/>`;
}
async function resolve(source){
 for(const[token,{a,w,h}]of requests){
  if(!source.includes(token))continue;
  if(!nineCache.has(token)){
   const file=path.join(ROOT,a.path),m=await sharp(file).metadata(),[l,b,r,t]=a.border;
   const sx=[0,l,m.width-r],sy=[0,t,m.height-b],sw=[l,m.width-l-r,r],sh=[t,m.height-t-b,b],dx=[0,l,w-r],dy=[0,t,h-b],dw=[l,w-l-r,r],dh=[t,h-t-b,b],parts=[];
   for(let j=0;j<3;j++)for(let i=0;i<3;i++)if(sw[i]>0&&sh[j]>0&&dw[i]>0&&dh[j]>0)parts.push({input:await sharp(file).extract({left:sx[i],top:sy[j],width:sw[i],height:sh[j]}).resize(dw[i],dh[j],{fit:'fill'}).png().toBuffer(),left:dx[i],top:dy[j]});
   const data=await sharp({create:{width:w,height:h,channels:4,background:'#0000'}}).composite(parts).png().toBuffer();nineCache.set(token,'data:image/png;base64,'+data.toString('base64'));
  }
  source=source.split(token).join(nineCache.get(token));
 }
 return source;
}
const skillFolder='Assets/Res/image/Game/SkillIcon';
const guidMap=new Map();for(const file of fs.readdirSync(path.join(ROOT,skillFolder)).filter(n=>n.endsWith('.png.meta'))){const guid=fs.readFileSync(path.join(ROOT,skillFolder,file),'utf8').match(/^guid: (\w+)/m)?.[1];if(guid)guidMap.set(guid,skillFolder+'/'+file.slice(0,-5));}
function skill(file,level,damage=0){
 const yaml=fs.readFileSync(path.join(ROOT,'Assets/ScriptableObject/Skill',file),'utf8');
 const raw=yaml.match(/displayName:\s*(.+)/)?.[1].trim(),name=raw?.startsWith('"')?JSON.parse(raw):raw;
 const guid=yaml.match(/icon:.*guid: (\w+)/)?.[1],icon=guidMap.get(guid);if(!name||!icon)throw Error('Skill binding '+file);
 return {name,icon,level,damage,maxLevel:Number(yaml.match(/maxLevel:\s*(\d+)/)?.[1]||5),source:file};
}
const active=[skill('ActiveSkill/SkillDef_AutoProjectileSword.asset',2,31179),skill('ActiveSkill/SkillDef_OrbitingBlades.asset',4,4714),skill('ActiveSkill/SkillDef_ThrowGrenade.asset',1,2817),skill('ActiveSkill/SkillDef_LineBeam.asset',2,3303)];
const passive=[skill('PassiveSkill/PassiveRegen.asset',2),skill('PassiveSkill/PassiveMaxHp.asset',1),skill('PassiveSkill/PassiveCritRate.asset',2)];
const victory=active.map((s,i)=>({...s,level:[5,4,3,4][i],damage:[63520,23840,12680,9980][i]}));
const defeat=active.map((s,i)=>({...s,damage:[35242,7129,3126,4047][i]}));
const mapFile='美术资源/地图设计/明快卡通_战斗地图_v1/02_地图效果图/04_田园食集_手机视口_1080x1920.png';
const mapFull='美术资源/地图设计/明快卡通_战斗地图_v1/02_地图效果图/04_田园食集_地面实拼_30x30格.png';
const hero='Assets/Res/image/Game/role/player02.png';
function defs(){return '<defs><linearGradient id="topFade" x1="0" x2="0" y1="0" y2="1"><stop stop-color="#FFF4DB" stop-opacity=".98"/><stop offset=".75" stop-color="#FFF4DB" stop-opacity=".88"/><stop offset="1" stop-color="#FFF4DB" stop-opacity="0"/></linearGradient><radialGradient id="energyGlow"><stop stop-color="#B8FFE0" stop-opacity=".72"/><stop offset=".55" stop-color="#85D9B7" stop-opacity=".26"/><stop offset="1" stop-color="#8FE5B3" stop-opacity="0"/></radialGradient><radialGradient id="energyCore" cx=".36" cy=".28" r=".8"><stop stop-color="#F1FFE3"/><stop offset=".5" stop-color="#ACEEA1"/><stop offset="1" stop-color="#5CB78C"/></radialGradient></defs>';}
function capsule(){return rect(805,106,235,76,38,'#49362735')+`<g fill="#FFF9EA"><circle cx="847" cy="144" r="5"/><circle cx="865" cy="144" r="5"/><circle cx="883" cy="144" r="5"/><circle cx="987" cy="144" r="19" fill="none" stroke="#FFF9EA" stroke-width="4"/><circle cx="987" cy="144" r="7"/></g>`;}
function timer(x,y,size=34){return `<g transform="translate(${x},${y}) scale(${size/40})" stroke="${P.muted}" fill="none" stroke-width="3" stroke-linecap="round"><circle cx="20" cy="22" r="15"/><path d="M15 2H25M20 7V2M20 13V22L27 26"/></g>`;}
function pauseIcon(x,y,size=40){return `<g transform="translate(${x},${y}) scale(${size/40})" fill="${P.ink}"><rect x="7" y="5" width="9" height="30" rx="3"/><rect x="24" y="5" width="9" height="30" rx="3"/></g>`;}
function btn(x,y,w,h,label,type='primary',icon=null){
 let s=asset(type==='primary'?'character_start_normal_9slice':type==='secondary'?'growth_normal_9slice':'home_card_cream_9slice',x,y,w,h);
 const size=label.length>5?34:40;if(icon){s+=asset(icon,x+(w-label.length*size-62)/2,y+(h-46)/2,46,46);s+=center(x+46,y,w-46,h,label,size);}
 else s+=center(x,y,w,h,label,size);return s;
}
function pill(x,y,w,h,label,fill='#FFF9EAEF',size=32){return rect(x,y,w,h,22,fill,'#CDBC99',2)+center(x,y,w,h,label,size,P.ink,false);}
function skillSlot(skill,x,y,size,pass=false,showLevel=true){
 let s=asset(skill?(pass?'skill_slot_passive_9slice':'skill_slot_base_9slice'):'skill_slot_empty_9slice',x,y,size,size);
 if(skill){s+=image(skill.icon,x+10,y+7,size-20,size-24);if(showLevel)s+=rect(x+size-54,y+size-30,53,27,9,pass?'#E3EBD0':'#FFF4D4','#B8A987',1.5)+center(x+size-54,y+size-30,53,27,'Lv.'+skill.level,20,P.ink,true);}
 else s+=center(x,y,size,size,'—',28,'#BAAD96',false);
 return s;
}
function word(x,y,label,rotation=0,size=44,color='#466D60'){return `<g transform="rotate(${rotation} ${x} ${y})">${text(x,y,label,size,color,true,'middle','stroke="#FFF9EAA0" stroke-width="4" paint-order="stroke"')}</g>`;}
function energyOrb(x,y,scale=1){
 return `<g data-role="energy-pickup" transform="translate(${x} ${y}) scale(${scale})">${circle(0,0,29,'url(#energyGlow)')}${circle(0,0,13,'url(#energyCore)','#4D9977',2)}<ellipse cx="-4" cy="-5" rx="5" ry="3.5" fill="#F6FFF1" opacity=".9"/><path d="M-5 8Q0 11 5 7" fill="none" stroke="#BDF6AF" stroke-width="2" stroke-linecap="round"/></g>`;
}
function battlefield(boss=false,dead=false){
 let s=image(mapFile,0,0,1080,1920);
 const words=[[170,684,'北京烤鸭',-10],[818,755,'炸酱面',8],[130,973,'豆汁儿',5],[835,1154,'驴打滚',-12],[245,1258,'胡辣汤',-6],[726,1454,'牡丹燕菜',6],[448,597,'豌豆黄',-4],[878,1533,'炒肝',-9]];
 for(const[x,y,label,r]of words)s+=word(x,y,label,r,44);
 for(const[x,y]of [[490,720],[758,1016],[369,1198],[600,1326],[852,1390],[198,1508],[674,672]])s+=energyOrb(x+14,y+16);
 if(boss){s+=circle(520,754,142,'#AF756014','#B4705C65',4)+word(520,775,'字灵首领',-2,70,'#A15149');}
 s+=`<ellipse cx="540" cy="1136" rx="91" ry="18" fill="#4936272B"/>`;
 s+=image(hero,455,958,170,194,dead?'opacity=".55"':'');
 s+=rect(432,908,216,24,12,'#493627', 'none')+rect(437,913,dead?1:boss?139:206,14,7,dead?'#B8A88C':'#95B66B');
 s+=center(432,873,216,32,dead?'0 / 86':boss?'58 / 86':'86 / 86',24,P.ink,true);
 if(!dead){
  s+=`<g fill="#E8F0F0" stroke="#5C6570" stroke-width="3" transform="translate(731 973) rotate(48)"><path d="M0 -46L8 5L0 18L-8 5Z"/><path d="M0 18V43" stroke="#70472E" stroke-width="9"/></g><g fill="#E8F0F0" stroke="#5C6570" stroke-width="3" transform="translate(366 1049) rotate(-65)"><path d="M0 -46L8 5L0 18L-8 5Z"/><path d="M0 18V43" stroke="#70472E" stroke-width="9"/></g>`;
  s+=text(802,1064,'46',38,'#C18C21',true,'middle','stroke="#FFF9EA" stroke-width="2" paint-order="stroke"');
 }
 return s;
}
function hud(boss=false,context=null){
 const run=context||{};
 let s=rect(0,0,1080,390,0,'url(#topFade)')+capsule();
 s+=text(40,155,'美食篇 · 关卡1-2',36,P.ink,true);
 s+=rect(530,106,226,76,26,'#FFF9EAEF','#D7C8A6',2)+timer(552,125,34)+center(592,106,150,76,run.time||(boss?'03:12':'01:46'),40,P.ink,true);
 s+=pill(40,212,262,64,run.wave||(boss?'波次 7 / 7':'波次 4 / 7'));
 s+=pill(326,212,468,64,run.kills||(boss?'击杀 608 / 620':'击杀 257 / 620'));
 s+=asset('surface_square_cream_9slice',948,210,92,92)+pauseIcon(974,236,40);
 if(boss){
  s+=text(40,340,'字灵首领',33,P.red,true);
  s+=rect(246,314,680,28,14,'#E0D1B8')+rect(246,314,367,28,14,'#C77960')+center(246,350,680,34,'432 / 800',26,P.ink,false);
 }else{
  s+=rect(40,303,128,60,20,'#F8CE77','#BA9148',2)+center(40,303,128,60,run.level||'Lv.14',30);
  s+=rect(190,322,736,24,12,'#DCD8BF','#C1B08E',2)+rect(193,325,449,18,9,'#83AB74');
 }
 // The minimap retains its existing single player-position marker, with no new markers or commands.
 s+=asset('home_card_cream_9slice',40,1630,236,236);
 s+=`<image x="52" y="1642" width="212" height="212" preserveAspectRatio="none" href="${uri(mapFull)}"/>`+circle(166,1761,8,'#C9674F','#FFF9EA',3);
 // Equipped skills remain accessible in Pause and Card Selection, not as a permanent battle overlay.
 return s;
}
function gameplay(boss=false){
 let s=battlefield(boss)+hud(boss);
 // A held joystick is shown only as an interaction state, not a permanently fixed control.
 s+=circle(337,1440,104,'#FFF9EA22','#66523A45',3)+circle(360,1426,39,'#FFF9EA55','#66523A55',3);
 return s;
}
function modalBackground(dead=false,context=null){return battlefield(false,dead)+hud(false,context)+rect(0,0,1080,1920,0,'#1C21136B');}
function paper(x,y,w,h){return asset('shadow_round_9slice',x-4,y+10,w+8,h+8)+asset('dialog_panel_cream_9slice',x,y,w,h);}
function audio(x,y,label,music=true){
 let s=asset(music?'icon_audio_music':'icon_audio_sound',x,y+13,48,48)+text(x+66,y+48,label,32,P.muted,true);
 s+=rect(x+258,y+13,104,52,26,'#93B077','#7B925F',2)+circle(x+335,y+39,22,'#FFFCF0','#B9AD91',1.5);return s;
}
function damageRows(x,y,w,rows,rowHeight=108){
 let s='',total=rows.reduce((n,r)=>n+r.damage,0),colors=['#D2A14A','#8FAB73','#72A2A7','#A18BB1','#C58470'];
 rows.forEach((r,i)=>{
  const ry=y+i*rowHeight;
  s+=rect(x,ry,w,rowHeight-12,20,i%2?'#F8F1DD':'#FFFAED');
  s+=asset('skill_slot_base_9slice',x+8,ry+9,72,72)+image(r.icon,x+15,ry+16,58,58);
  s+=text(x+101,ry+39,r.name,34,P.ink,true);
  s+=text(x+101,ry+73,'Lv.'+r.level,26,P.muted,false);
  if(r.level>=r.maxLevel)s+=rect(x+176,ry+50,76,31,10,'#E8DEEF')+center(x+176,ry+50,76,31,'突破',23,'#82668F');
  s+=text(x+w-15,ry+42,r.damage.toLocaleString('en-US'),37,P.ink,true,'end');
  s+=rect(x+290,ry+64,w-314,10,5,'#E7E0CE')+rect(x+290,ry+64,Math.max(8,(w-314)*r.damage/total),10,5,colors[i]);
 });return s;
}
function passiveCards(x,y,w,showEmpty=true,compact=false){
 let s='',gap=15,card=(w-4*gap)/5;
 for(let i=0;i<5;i++){
  const p=passive[i],cx=x+i*(card+gap);
  if(!p&&!showEmpty)continue;
  s+=rect(cx,y,card,compact?148:170,19,p?'#EFF1DF':'#F0EBDC',p?'#CDD5B4':'#E1D7C0',2);
  s+=skillSlot(p,cx+(card-76)/2,y+(compact?8:12),76,true,false);
  s+=center(cx,y+(compact?85:98),card,37,p?p.name:'未装备',p?26:24,p?P.ink:P.weak,!!p);
  if(p)s+=center(cx,y+(compact?119:136),card,27,'Lv.'+p.level,24,P.muted,false);
 }return s;
}
function pausePage(){
 let s=modalBackground()+paper(72,252,936,1430);
 s+=pauseIcon(114,293,44)+text(176,335,'游戏暂停',52,P.ink,true)+text(964,328,'美食篇 · 关卡1-2',29,P.muted,false,'end');
 s+=rule(114,372,852);
 s+=statRow(114,392,852,[['当前波次','4 / 7'],['本局时长','01:46'],['击杀数量','257']],92);
 s+=audio(120,512,'背景音乐',true)+audio(564,512,'音效',false)+rule(114,602,852);
 s+=text(114,654,'输出技能',38,P.ink,true)+text(304,651,'4 / 5',29,P.muted,false)+text(966,651,'累计伤害',29,P.muted,false,'end');
 s+=damageRows(114,678,852,active,93);
 s+=text(114,1180,'被动技能',38,P.ink,true)+text(966,1177,'3 / 5',29,P.muted,false,'end');
 s+=passiveCards(114,1206,852);
 s+=btn(114,1512,310,112,'退出关卡','quiet')+btn(452,1512,514,112,'继续战斗','primary');
 return s;
}
function statRow(x,y,w,stats,h=116){
 let s=rect(x,y,w,h,24,'#EDE9D5');
 const cw=w/stats.length;
 stats.forEach(([label,value],i)=>{s+=text(x+cw*(i+.5),y+34,label,27,P.muted,false,'middle')+text(x+cw*(i+.5),y+h-20,value,42,P.ink,true,'middle');if(i)s+=`<path d="M${x+cw*i} ${y+20}V${y+h-20}" stroke="#D5CDB6" stroke-width="2"/>`;});return s;
}
function rewardChip(x,y,w,label,amount,key,fragment=false){
 let s=rect(x,y,w,96,20,'#FFF9EA','#D7C8A6',2);
 if(fragment){s+=image('Assets/Res/image/Game/role/player02.png',x+14,y+10,58,72)+asset('icon_fragment_piece',x+8,y+58,28,28);}
 else s+=asset(key,x+20,y+24,48,48);
 s+=text(x+88,y+34,label,26,P.muted,false)+text(x+88,y+74,amount,36,P.ink,true);return s;
}
function resultPage(win=true){
 let s=modalBackground(!win,win?{time:'03:28',wave:'波次 7 / 7',kills:'击杀 620 / 620',level:'Lv.21',actives:victory}:{time:'01:58'})+paper(64,186,952,1570);
 s+=center(112,214,856,58,'美食篇 · 关卡1-2',32,P.muted,false);
 s+=asset(win?'nav_trophy':'icon_flag',489,278,102,96);
 s+=center(112,382,856,76,win?'战斗胜利':'战斗失败',68,win?'#B5812A':P.muted,true);
 if(win)for(let i=0;i<3;i++)s+=asset('star_filled',424+i*84,470,64,64);
 else s+=center(112,470,856,60,'调整搭配，再来一局',33,P.muted,false);
 s+=statRow(112,562,856,[['完成波次',win?'7 / 7':'3 / 7'],['本局时长',win?'03:28':'01:58'],['击杀数量',win?'620':'257']],112);
 s+=text(112,735,'输出技能',38,P.ink,true)+text(302,731,'4 / 5',28,P.muted,false)+text(968,731,'累计伤害',29,P.muted,false,'end');
 s+=damageRows(112,755,856,win?victory:defeat,93);
 s+=text(112,1171,'被动技能',36,P.ink,true)+text(968,1167,'3 / 5',28,P.muted,false,'end');
 s+=passiveCards(112,1195,856,true,true);
 s+=text(112,1390,'获得奖励',36,P.ink,true);
 s+=rewardChip(112,1411,256,'金币',win?'+180':'+60','icon_coins');
 if(win)s+=rewardChip(388,1411,330,'飞刀娃碎片','+2',null,true);
 if(win)s+=rect(112,1524,856,66,18,'#E9EDCF')+asset('icon_check',130,1544,28,28)+text(176,1566,'新角色解锁：爆爆',31,P.green,true)+rect(760,1534,192,46,15,'#D7E2BA','#A2AF80',1.5)+center(760,1534,192,46,'前往查看',28,P.ink,true);
 s+=btn(112,1612,322,110,'返回主界面','quiet')+btn(458,1612,510,110,win?'下一关':'再试一次','primary');
 return s;
}
function revivePage(){
 let s=modalBackground(true,{time:'01:58'})+paper(96,466,888,988);
 s+=center(138,502,804,92,'再战一次？',60,P.ink,true);
 s+=center(138,609,804,48,'美食篇 · 关卡1-2',30,P.muted,false);
 s+=center(138,678,804,58,'满血复活，继续当前关卡',36,P.muted,false);
 s+=circle(540,916,126,'#F8F0D9','#E2D8BA',15);
 const circumference=2*Math.PI*126;
 s+=`<circle cx="540" cy="916" r="126" fill="none" stroke="#E8B04D" stroke-width="15" stroke-linecap="round" stroke-dasharray="${circumference*.8} ${circumference}" transform="rotate(-90 540 916)"/>`;
 s+=text(540,942,'8',100,P.ink,true,'middle')+text(540,992,'秒',28,P.muted,false,'middle');
 s+=text(540,1088,'复活倒计时',29,P.muted,false,'middle');
 s+=pill(164,1122,752,64,'波次 4 / 7   ·   击杀 257   ·   时长 01:58','#F2ECD7',29);
 s+=btn(140,1224,800,108,'看广告复活','primary','icon_ad_video');
 s+=center(140,1345,800,62,'结束本局',34,P.muted,false);
 s+=center(140,1412,800,32,'倒计时结束将进入结算',27,P.weak,false);
 return s;
}
const pages=[['gameplay','战斗 HUD',()=>gameplay(false)],['pause','游戏暂停',pausePage],['result-victory','胜利结算',()=>resultPage(true)],['revive','复活',revivePage],['result-defeat','失败结算',()=>resultPage(false)],['gameplay-boss','Boss 战斗 HUD',()=>gameplay(true)]];
async function board(file,keys,labels,columns=2){
 const rows=Math.ceil(keys.length/columns),cw=396,pad=36,head=122,rowh=704,w=columns*cw+pad,h=head+rows*rowh+40;
 let source=`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}">${rect(0,0,w,h,0,'#E9E2D0')}${text(36,48,'文字割草机 · 局内 UI 第二版',29,P.ink,true)}${text(36,88,'能量珠 · 精简战斗 HUD · 独立失败页 · 倒计时居中',20,P.muted,false)}`;
 const parts=[];
 for(let i=0;i<keys.length;i++){const x=pad+i%columns*cw,y=head+Math.floor(i/columns)*rowh;source+=text(x,y+24,labels[i],23,P.ink,true);parts.push({input:await sharp(path.join(OUT,keys[i]+'-1080x1920.png')).resize(360,640).toBuffer(),left:x,top:y+44});}
 source+=text(w-36,h-13,'效果图 · 示例数据 · 未改动 Unity 工程界面',18,P.muted,false,'end')+'</svg>';
 await sharp(Buffer.from(source)).composite(parts).png().toFile(path.join(OUT,file));
}
async function main(){
 fs.mkdirSync(path.join(OUT,'source'),{recursive:true});
 for(const[key,label,render]of pages){
  const source=await resolve(`<svg xmlns="http://www.w3.org/2000/svg" width="1080" height="1920" viewBox="0 0 1080 1920">${defs()}${render()}</svg>`);
  fs.writeFileSync(path.join(OUT,'source',key+'.svg'),source);
  await sharp(Buffer.from(source)).png().toFile(path.join(OUT,key+'-1080x1920.png'));
  await sharp(path.join(OUT,key+'-1080x1920.png')).resize(360,640).png().toFile(path.join(OUT,key+'-360x640.png'));
 }
 await board('overview.png',['gameplay','result-victory','result-defeat','revive'],['01 · 战斗 HUD（精简）','02 · 战斗胜利（保留）','03 · 战斗失败','04 · 复活（居中）']);
 await board('changes.png',['gameplay','result-defeat','revive'],['战斗 HUD · 移除常驻技能栏','战斗失败 · 单独展示','复活 · 去立绘、居中倒计时'],3);
 await board('states.png',['gameplay-boss','result-victory','result-defeat'],['Boss 态 · 替换升级能量条','结算 · 胜利态','结算 · 失败态'],3);
 fs.writeFileSync(path.join(OUT,'design-data.json'),JSON.stringify({version:2,canvas:{width:1080,height:1920},activeCapacity:5,passiveCapacity:5,equippedBattleHud:false,energyPickup:'round mint-green energy orb, no currency image',revivePortrait:false,reviveCountdownCenter:{x:540,y:916},active,passive,victory,defeat,referenceMap:mapFile,mode:'Review-only design, not a Unity runtime screenshot',unchanged:['victory panel layout','pause panel layout','Assets scenes','Assets prefabs','inventory and reward logic','revive/ad provider']},null,2));
 const spec=JSON.parse(fs.readFileSync(path.join(ROOT,'ArtOutput/UI/BattleDesignV1/layout-spec.json'),'utf8'));
 spec.version=2;
 for(const field of ['equippedSummary','activeSlots','passiveSlots','slotSize','slotPitch'])delete spec.gameplay[field];
 spec.gameplay.permanentEquippedSkills=false;
 spec.gameplay.equippedSkillAccess=['pause panel','skill selection panel'];
 spec.gameplay.bottomFullWidthOverlay=false;
 spec.gameplay.energyPickup={shape:'circular mint-green energy orb',bodyRadius:13,glowRadius:29,stroke:'#4D9977',core:['#F1FFE3','#ACEEA1','#5CB78C'],currencyIcon:false};
 spec.revive.ring={cx:540,cy:916,radius:126,stroke:15};
 spec.revive.characterPortrait=false;spec.revive.context=[164,1122,752,64];
 fs.writeFileSync(path.join(OUT,'layout-spec.json'),JSON.stringify(spec,null,2));
 const orbSource=`<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="-32 -32 64 64">${defs()}${energyOrb(0,0)}</svg>`;
 fs.writeFileSync(path.join(OUT,'source','energy-pickup.svg'),orbSource);
 await sharp(Buffer.from(orbSource)).png().toFile(path.join(OUT,'energy-pickup-64.png'));
 console.log(JSON.stringify({output:OUT,pages:pages.length,canvas:'1080x1920',mode:'design-only'}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
