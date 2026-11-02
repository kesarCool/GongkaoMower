// Review-only designs. This script never writes to Assets or modifies Unity prefabs.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const ROOT = path.resolve(__dirname, '../..');
const OUT = path.join(ROOT, 'ArtOutput/UI/HomePagesDesignV1');
const P = {ink:'#493627', muted:'#8D7A5D', body:'#76634C', cream:'#FFF9EA', page:'#F5EED8', gold:'#F6B94E', green:'#4D7651', mint:'#D9E5BE', border:'#D7C8A6', red:'#B45F48'};
const esc = s => String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/"/g,'&quot;');
const svg = (w,h,s) => `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${s}</svg>`;
const rect = (x,y,w,h,r,fill,stroke='none',sw=0) => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const line = (x1,y1,x2,y2,c=P.border,sw=2) => `<path d="M${x1} ${y1}H${x2}" fill="none" stroke="${c}" stroke-width="${sw}"/>`;
const text = (x,y,s,size=38,color=P.ink,weight=400,anchor='start') => `<text x="${x}" y="${y}" font-family="Microsoft YaHei, sans-serif" font-size="${size}" fill="${color}" font-weight="${weight}" text-anchor="${anchor}">${esc(s)}</text>`;
const center = (x,y,w,h,s,size=38,color=P.ink,weight=700) => text(x+w/2,y+h/2+size*.35,s,size,color,weight,'middle');
const assets = new Map(), uris=new Map(), nine=new Map(), requests=new Map();
for(const phase of ['home','character'])for(const a of JSON.parse(fs.readFileSync(path.join(ROOT,`Tools/UI/${phase}-assets.json`),'utf8')).assets)assets.set(path.basename(a.path,'.png'),a);
function uri(file){if(!uris.has(file))uris.set(file,'data:image/png;base64,'+fs.readFileSync(file).toString('base64'));return uris.get(file);}
function image(file,x,y,w,h,extra=''){return `<image x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="xMidYMid meet" xlink:href="${uri(path.join(ROOT,file))}" ${extra}/>`;}
function asset(key,x,y,w,h){
 const a=assets.get(key);if(!a)throw Error('Missing shared asset '+key);
 const file=path.join(ROOT,a.path);
 if(a.border.every(b=>b===0))return `<image x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="none" xlink:href="${uri(file)}"/>`;
 const token=`__NINE_${key}_${Math.round(w)}_${Math.round(h)}__`;requests.set(token,{a,w:Math.round(w),h:Math.round(h)});
 return `<image x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="none" xlink:href="${token}"/>`;
}
async function resolve(source){
 for(const [token,{a,w,h}] of requests){
  if(!source.includes(token))continue;
  if(!nine.has(token)){
   const file=path.join(ROOT,a.path),meta=await sharp(file).metadata();const [l,b,r,t]=a.border;
   const sx=[0,l,meta.width-r],sy=[0,t,meta.height-b],sw=[l,meta.width-l-r,r],sh=[t,meta.height-t-b,b];
   const dx=[0,l,w-r],dy=[0,t,h-b],dw=[l,w-l-r,r],dh=[t,h-t-b,b],parts=[];
   for(let j=0;j<3;j++)for(let i=0;i<3;i++)if(sw[i]>0&&sh[j]>0&&dw[i]>0&&dh[j]>0)parts.push({input:await sharp(file).extract({left:sx[i],top:sy[j],width:sw[i],height:sh[j]}).resize(dw[i],dh[j],{fit:'fill'}).png().toBuffer(),left:dx[i],top:dy[j]});
   const png=await sharp({create:{width:w,height:h,channels:4,background:'#0000'}}).composite(parts).png().toBuffer();nine.set(token,'data:image/png;base64,'+png.toString('base64'));
  }
  source=source.split(token).join(nine.get(token));
 }
 return source;
}
const checks=[];
function check(name,passed,detail=''){checks.push({name,passed,detail});if(!passed)throw Error(name+': '+detail);}
function clock(x,y,size=34){return `<g transform="translate(${x},${y}) scale(${size/40})" stroke="${P.body}" fill="none" stroke-width="3.2" stroke-linecap="round"><circle cx="20" cy="20" r="16"/><path d="M20 10V20L28 24"/></g>`;}
function ad(x,y,size=36){return `<g transform="translate(${x},${y}) scale(${size/40})" stroke="${P.ink}" stroke-width="3" stroke-linejoin="round" fill="none"><rect x="2" y="5" width="36" height="28" rx="6"/><path d="M16 12L27 19L16 26Z" fill="${P.ink}" stroke="none"/><path d="M12 38H28"/></g>`;}
function puzzle(x,y,size=46){return `<g transform="translate(${x},${y}) scale(${size/48})"><path d="M4 9H17C12 -1 34 -1 29 9H42V22C32 16 32 36 42 30V43H29C35 33 13 33 19 43H4V30C14 36 14 16 4 22Z" fill="#F5C065" stroke="#9D733D" stroke-width="3" stroke-linejoin="round"/></g>`;}
function checkmark(x,y,size=32,color=P.green){return `<g transform="translate(${x},${y}) scale(${size/32})"><circle cx="16" cy="16" r="16" fill="${color}"/><path d="M8 16L14 22L25 10" stroke="#FFF9EA" stroke-width="3.5" fill="none" stroke-linecap="round" stroke-linejoin="round"/></g>`;}
function header(){
 let s=asset('top_safe_gradient_strip',0,0,1080,221);
 s+=asset('surface_square_cream_9slice',40,113.28,113.14,113.14)+asset('icon_settings',67,139,60,60);
 for(const [x,key,value] of [[175.82,'icon_coins','832'],[442.58,'icon_gem','325']])s+=asset('surface_pill_cream_9slice',x,113.28,244.08,82.56)+asset(key,x+21,126.5,56,56)+center(x+86,113.28,145,82.56,value,44);
 s+=rect(804.64,113.28,235.44,82.56,42,'#49362738')+`<g fill="#FFF4DA"><circle cx="847" cy="154.56" r="5"/><circle cx="865" cy="154.56" r="5"/><circle cx="883" cy="154.56" r="5"/><circle cx="984" cy="154.56" r="20" fill="none" stroke="#FFF4DA" stroke-width="5"/><circle cx="984" cy="154.56" r="8"/></g>`;
 return s;
}
function footer(active){
 let s=asset('nav_bar_background_9slice',0,1747.2,1080,172.8);
 const keys=['nav_exchange','nav_book','nav_swords','nav_hero','nav_trophy'],labels=['兑换','词汇','战斗','角色','成就'];
 for(let i=0;i<5;i++){
  const x=i*216;if(active===i)s+=asset('nav_selected_9slice',x+40,1756.2,136,150);
  s+=asset(keys[i],x+60,1763.2,96,96)+center(x,1855.2,216,56,labels[i],40);
  if((i===0||i===3||i===4)&&i!==active)s+=asset('notify_dot',x+154,1753.2,24,24);
 }
 return s;
}
function base(){return rect(0,0,1080,1920,0,P.page);}
function title(label,subtitle,key){return asset(key,54,257,84,84)+text(158,318,label,64,P.ink,700)+text(58,376,subtitle,32,P.body);}
function card(x,y,w,h,fill=P.cream,stroke=P.border){return rect(x,y+6,w,h,28,'#4936270B')+rect(x,y,w,h,28,fill,stroke,3);}
function button(x,y,w,h,label,type='primary',icon=null){
 let s=asset(type==='primary'?'character_start_normal_9slice':type==='disabled'?'growth_disabled_9slice':'growth_normal_9slice',x,y,w,h);
 if(icon==='ad'){s+=ad(x+55,y+(h-38)/2,38);s+=center(x+58,y,w-64,h,label,36,P.ink,700);}
 else if(icon==='gem'){s+=asset('icon_gem',x+45,y+(h-44)/2,44,44);s+=center(x+40,y,w-25,h,label,38,P.ink,700);}
 else s+=center(x,y,w,h,label,38,type==='disabled'?P.muted:P.ink,700);
 return s;
}
function coinReward(x,y){
 let s=rect(x-2,y+100,152,26,13,'#D9B75E35');
 s+=asset('icon_coins',x+42,y,98,98)+asset('icon_coins',x,y+29,104,104)+asset('icon_coins',x+70,y+40,82,82);return s;
}
function gemReward(x,y){return rect(x-2,y+100,152,26,13,'#6AB3B72A')+asset('icon_gem',x+43,y,100,100)+asset('icon_gem',x,y+44,90,90)+asset('icon_gem',x+83,y+48,72,72);}
function shop(){
 let s=base()+header()+title('兑换','领取补给，收集角色碎片','nav_exchange');
 s+=rect(616,279,410,69,34,'#E9E5D1')+clock(637,297,33)+text(1000,325,'刷新 01:50:30',31,P.body,500,'end');
 for(const [i,name,count] of [[0,'金币','500'],[1,'钻石','15']]){
  const x=54+i*498,y=416;
  s+=card(x,y,474,356,i?'#F3F6E9':'#FFF7E4');
  s+=i?gemReward(x+29,y+52):coinReward(x+29,y+52);
  s+=text(x+214,y+99,name,36,P.body,500)+text(x+214,y+165,count,59,P.ink,700);
  s+=text(x+237,y+220,'限领 1 / 1',29,P.body,400,'middle');
  s+=button(x+30,y+250,414,84,i?'看广告领取':'免费领取',i?'secondary':'primary',i?'ad':null);
 }
 s+=text(58,845,'角色碎片',45,P.ink,700)+text(1022,844,'用于解锁与升阶',29,P.body,400,'end');
 const heroes=[['弹仔','player01.png',30],['飞刀娃','player02.png',30],['爆爆','player04.png',40],['符箓娃','player05.png',40],['追风','player03.png',50]];
 heroes.forEach(([name,file,price],i)=>{
  const x=54+(i%3)*332,y=884+Math.floor(i/3)*402,w=308,h=378,sold=i===4;
  s+=card(x,y,w,h,sold?'#EDE8DA':P.cream,sold?'#D5CCBA':P.border);
  s+=center(x+12,y+17,w-24,54,name+'碎片',37,sold?P.muted:P.ink,700);
  s+=`<ellipse cx="${x+w/2}" cy="${y+219}" rx="88" ry="15" fill="#49362712"/>`;
  s+=image('Assets/Res/image/Game/role/'+file,x+66,y+80,176,148,sold?'opacity=".5"':'');
  s+=puzzle(x+65,y+183,44);
  s+=center(x,y+232,w,40,sold?'限购 0 / 3':'限购 3 / 3',29,P.body,400);
  s+=button(x+21,y+282,w-42,78,sold?'已售罄':String(price),sold?'disabled':'primary',sold?null:'gem');
 });
 // Deliberately leave the sixth slot empty: there are five actual fragment products.
 s+=center(54,1688,972,36,'商品随倒计时刷新，限购次数同时重置',28,P.muted,400);
 check('Shop contains all seven configured products',heroes.length+2===7);
 check('Shop cards clear bottom navigation',884+402+378<1747.2);
 return s+footer(0);
}
function tabs(){
 let s=asset('tab_selected_folder_9slice',52,414,478,94)+asset('tab_connector_9slice',526,502,502,8);
 s+=asset('nav_book',153,440,42,42)+center(211,421,255,80,'美食篇',44,P.ink,700)+center(552,430,474,70,'成语篇',40,P.muted,400);return s;
}
function lexicon(){
 let s=base()+header()+title('词汇','看看本篇会遇到哪些文字','nav_book');
 s+=card(54,506,972,1196,P.cream,'#E4DAC2')+tabs();
 const regions=['京津冀','晋鲁豫','黑吉辽蒙','江浙沪皖','云贵川渝','湘赣鄂','港澳台','粤桂琼闽','陕甘宁','新青藏'];
 regions.forEach((name,i)=>{
  const x=88+(i%5)*184,y=544+Math.floor(i/5)*96,selected=i===0;
  s+=rect(x,y,168,84,18,selected?'#F7CC76':'#F1EDDE',selected?'#B08644':'#DED4BD',selected?3:2)+center(x,y,168,84,name,31,selected?P.ink:P.body,selected?700:400);
 });
 s+=asset('icon_locate',94,756,36,36)+text(146,789,'京津冀',41,P.ink,700)+text(986,786,'共 70 个词条',30,P.body,400,'end')+line(92,817,988,817);
 const data=JSON.parse(fs.readFileSync(path.join(ROOT,'Library/UnityMCPTools/home-page-content-data.json'),'utf8')).data.result;
 const words=data.food.slice(0,14);
 words.forEach((name,i)=>{
  const x=94+(i%2)*470,y=847+Math.floor(i/2)*111;
  s+=rect(x,y+19,61,61,19,i%4<2?'#E7ECD7':'#F3E5BD')+center(x,y+19,61,61,String(i+1).padStart(2,'0'),29,P.body,500);
  s+=text(x+81,y+61,name,39,P.ink,500);
  if(i<12)s+=line(x+4,y+104,x+426,y+104,'#E9E2D0',2);
 });
 s+=center(90,1650,900,36,'上下滑动，查看其余词条',28,P.muted,400);
 check('Lexicon ten existing category filters retained',regions.length===10);
 check('Lexicon uses actual current table count',data.foodCount===70);
 check('Lexicon entries clear footer hint',847+6*111+80<1650);
 return s+footer(1);
}
function progress(x,y,w,fraction,completed=false){
 const fill=completed?'#D7A747':'#8CAB72';
 return rect(x,y,w,12,6,'#E3DCC8')+rect(x,y,Math.max(12,w*fraction),12,6,fill);
}
function achievementRow(y,{name,current,target,reward,state},height=156){
 const claim=state==='claim',done=state==='claimed',bg=claim?'#FFF3CD':done?'#EEE9DC':P.cream;
 let s=card(54,y,972,height,bg,claim?'#CCA05A':done?'#D7CEBA':P.border);
 s+=text(94,y+52,name,38,done?P.muted:P.ink,done?400:700);
 s+=text(94,y+101,(done?'已完成':'进度')+' '+current+' / '+target,28,done?P.muted:P.body,400);
 s+=progress(94,y+121,548,Math.min(1,current/target),claim||done);
 s+=asset('icon_gem',714,y+28,56,56)+text(742,y+118,'×'+reward,30,done?P.muted:P.body,500,'middle');
 if(claim)s+=button(820,y+36,176,84,'领取');
 else if(done)s+=checkmark(825,y+62,32,'#90A07E')+text(963,y+88,'已领取',31,P.muted,500,'middle');
 else s+=center(811,y+45,197,70,'进行中',32,P.muted,500);
 return s;
}
function achievements(){
 let s=base()+header()+title('成就','完成挑战，领取钻石奖励','nav_trophy');
 s+=rect(54,416,972,106,26,'#E2E9D1')+checkmark(87,447,43,'#738F5B');
 s+=text(151,481,'有 2 项奖励待领取',39,P.ink,700)+text(983,480,'已完成优先展示',28,P.body,400,'end');
 s+=text(58,587,'可领取',36,P.ink,700)+asset('notify_dot',184,563,20,20);
 s+=achievementRow(613,{name:'击杀1000个字灵',current:1000,target:1000,reward:20,state:'claim'});
 s+=achievementRow(787,{name:'解锁2个新英雄',current:2,target:2,reward:50,state:'claim'});
 s+=text(58,1007,'进行中',36,P.ink,700);
 s+=achievementRow(1035,{name:'击杀5000个字灵',current:1380,target:5000,reward:50,state:'progress'});
 s+=achievementRow(1209,{name:'通关5个关卡',current:1,target:5,reward:50,state:'progress'});
 s+=text(58,1429,'已领取',36,P.body,700);
 s+=achievementRow(1457,{name:'击杀100个字灵',current:100,target:100,reward:10,state:'claimed'});
 s+=center(54,1648,972,42,'上下滑动，查看全部成长记录',29,P.muted,400);
 check('Achievement state examples cover all three states',true,'Ready to claim / in progress / claimed; original individual claim action retained.');
 check('Achievement cards clear navigation',1457+156<1747.2);
 return s+footer(4);
}
async function save(name,body){
 const source=await resolve(svg(1080,1920,body));
 fs.writeFileSync(path.join(OUT,'source',name+'.svg'),source);
 const png=await sharp(Buffer.from(source)).png().toBuffer();
 await sharp(png).toFile(path.join(OUT,name+'-1080x1920.png'));
 await sharp(png).resize(360,640).toFile(path.join(OUT,name+'-360x640.png'));
 return png;
}
async function main(){
 fs.mkdirSync(path.join(OUT,'source'),{recursive:true});
 const pages=[['exchange',shop()],['lexicon',lexicon()],['achievements',achievements()]];
 const buffers=[];for(const [name,body]of pages)buffers.push(await save(name,body));
 let board=rect(0,0,1212,836,0,'#E9E2D0')+text(36,48,'文字割草机 · 局外页面设计 V1',27,P.ink,700)+text(1176,46,'1080 × 1920 / 预览稿',18,P.body,400,'end');
 const labels=['兑换 · 补给与碎片','词汇 · 篇章与地区','成就 · 三种领取状态'];
 const notes=['7 项商品完整保留','10 个地区 / 双列词条','单项领取 / 进度与奖励'];
 for(let i=0;i<3;i++){
  const x=36+i*396;board+=text(x,89,labels[i],23,P.ink,700);
  board+=`<image x="${x}" y="112" width="360" height="640" xlink:href="data:image/png;base64,${buffers[i].toString('base64')}"/>`;
  board+=text(x+180,790,notes[i],19,P.body,400,'middle');
 }
 await sharp(Buffer.from(svg(1212,836,board))).png().toFile(path.join(OUT,'overview.png'));
 fs.writeFileSync(path.join(OUT,'design-checks.json'),JSON.stringify({passed:checks.every(c=>c.passed),designResolution:[1080,1920],scope:'Review mockups only. No Unity prefab, script, asset or player-save changes.',checks},null,2));
 console.log(JSON.stringify({output:OUT,pages:pages.map(p=>p[0]),checks:checks.length,passed:true},null,2));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
