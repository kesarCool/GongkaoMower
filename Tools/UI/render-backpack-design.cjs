// Review-only design; no Assets or prefab mutations.
const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtOutput/UI/BackpackDesignV2');
const P={ink:'#493627',muted:'#76634C',cream:'#FFF4DB',border:'#D7C8A6',gold:'#F6B94E'};
const esc=s=>String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;');
const rect=(x,y,w,h,r,c,stroke='none',sw=0)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${c}" stroke="${stroke}" stroke-width="${sw}"/>`;
const text=(x,y,s,size=32,color=P.ink,bold=false,anchor='start')=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei, sans-serif" font-size="${size}" fill="${color}" font-weight="${bold?700:400}" text-anchor="${anchor}">${esc(s)}</text>`;
const img=(file,x,y,w,h)=>`<image x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="xMidYMid meet" href="data:image/png;base64,${fs.readFileSync(path.join(root,file)).toString('base64')}"/>`;
const icon=(file,x,y,w,h)=>img('Assets/Res/image/common/'+file+'.png',x,y,w,h);
const wrap=s=>`<svg xmlns="http://www.w3.org/2000/svg" width="1080" height="1920" viewBox="0 0 1080 1920">${s}</svg>`;
function screen(mode){
 let s=img('ArtOutput/UI/HomeRoadmap/home-current-102-1080x1920.png',0,0,1080,1920)+rect(0,0,1080,1920,0,'#1B1A1560');
 const x=80,y=440,w=920,h=1040;
 s+=rect(x,y+10,w,h,42,'#49362728')+rect(x,y,w,h,42,P.cream,P.ink,10);
 s+=icon('icon_backpack',x+42,y+32,60,60)+text(x+122,y+79,'背包',48,P.ink,true);
 s+=rect(x+w-110,y+24,72,72,23,'#EDE6CF')+icon('icon_close',x+w-91,y+43,34,34);
 s+=`<path d="M${x+42} ${y+116}H${x+w-42}" stroke="#E5D8B7" stroke-width="2"/>`;
 const active=mode==='currency'?'currency':['materials','tooltip','empty'].includes(mode)?'materials':'all';
 const categories=[['all','全部','icon_backpack'],['currency','货币','icon_coins'],['materials','材料','icon_fragment_piece']];
 categories.forEach(([key,label,asset],i)=>{
  const tx=x+42+i*254,ty=y+148,selected=key===active;
  if(selected)s+=rect(tx,ty+4,230,80,23,'#49362712')+rect(tx,ty,230,80,23,'#F8CD77','#B78B42',3);
  s+=icon(asset,tx+42,ty+20,40,40)+text(tx+100,ty+54,label,36,selected?P.ink:P.muted,selected);
 });
 s+=`<path d="M${x+42} ${y+254}H${x+w-42}" stroke="#E5D8B7" stroke-width="2"/>`;
 const allItems=[
  {id:102,type:'materials',label:'飞刀娃碎片',icon:'player02.png',count:20},
  {id:101,type:'materials',label:'弹仔碎片',icon:'player01.png',count:8},
  {id:103,type:'materials',label:'爆爆碎片',icon:'player04.png',count:3},
  {id:104,type:'materials',label:'符箓娃碎片',icon:'player05.png',count:5},
  {id:105,type:'materials',label:'追风碎片',icon:'player03.png',count:2},
  {id:1,type:'currency',label:'金币',icon:'icon_coins',count:832},
  {id:2,type:'currency',label:'钻石',icon:'icon_gem',count:325}
 ];
 const items=mode==='empty'?[]:allItems.filter(item=>active==='all'||item.type===active);
 const heading=active==='all'?'全部物品':active==='currency'?'货币':'材料 · 角色碎片';
 s+=text(x+42,y+309,heading,34,P.ink,true)+text(x+w-42,y+307,`共 ${items.length} 种`,29,P.muted,false,'end');
 if(items.length)items.forEach(({label,icon:key,count,type},i)=>{
  const cx=x+42+i%4*214,cy=y+346+Math.floor(i/4)*272,selected=mode==='tooltip'&&i===0,fragment=type==='materials';
  s+=rect(cx,cy+5,194,250,22,'#4936270B')+rect(cx,cy,194,250,22,selected?'#FFF1CC':'#FFFAEC',selected?'#B4863B':P.border,selected?5:2.5);
  s+=rect(cx+18,cy+18,158,138,18,fragment?'#EBE4F2':key==='icon_coins'?'#F8E9BE':'#DBEEF0');
  if(fragment){s+=img('Assets/Res/image/Game/role/'+key,cx+47,cy+25,104,125)+icon('icon_fragment_piece',cx+25,cy+111,38,38);}
  else s+=icon(key,cx+52,cy+40,90,90);
  s+=text(cx+97,cy+196,label,28,P.ink,true,'middle');
  s+=text(cx+97,cy+232,'× '+count,29,P.muted,false,'middle');
 });
 else{
  s+=rect(x+314,y+405,292,232,116,'#EFEAD8')+icon('icon_backpack',x+380,y+435,160,160);
  s+=text(x+w/2,y+715,'暂时没有材料',40,P.ink,true,'middle')+text(x+w/2,y+778,'闯关与兑换获得的材料会收在这里',29,P.muted,false,'middle');
 }
 s+=text(x+w/2,y+h-44,'点击物品查看说明',29,P.muted,false,'middle');
 if(mode==='tooltip'){
  const tx=128,ty=556,tw=650,th=220;
  s+=rect(tx,ty+6,tw,th,32,'#49362725')+rect(tx,ty,tw,th,32,P.cream,P.ink,8);
  s+=text(tx+40,ty+69,'飞刀娃碎片',40,P.ink,true)+text(tx+40,ty+135,'收集可解锁 / 进阶飞刀娃',34,P.muted);
  s+=text(tx+40,ty+185,'当前拥有：20',29,P.muted);
 }
 return wrap(s);
}
(async()=>{
 fs.mkdirSync(out,{recursive:true});
 for(const mode of ['inventory','currency','materials','tooltip','empty'])await sharp(Buffer.from(screen(mode))).png().toFile(path.join(out,`backpack-${mode}-1080x1920.png`));
 let board=`<svg xmlns="http://www.w3.org/2000/svg" width="1224" height="824">${rect(0,0,1224,824,0,'#E9E2D0')}${text(36,48,'背包设计 · 分类页签',29,P.ink,true)}`;
 const labels=['全部 · 7 种物品','货币 · 金币与钻石','材料 · 角色碎片'];
 labels.forEach((t,i)=>board+=text(36+i*396,93,t,23,P.ink,true));
 board+=text(1188,798,'设计稿 · 数量为示例 · 点击物品查看说明 · 分类增多时横向滑动',18,P.muted,false,'end')+'</svg>';
 const parts=[];for(const [i,mode]of ['inventory','currency','materials'].entries())parts.push({input:await sharp(path.join(out,`backpack-${mode}-1080x1920.png`)).resize(360,640).toBuffer(),left:36+i*396,top:116});
 await sharp(Buffer.from(board)).composite(parts).png().toFile(path.join(out,'overview.png'));
 fs.writeFileSync(path.join(out,'design-notes.md'),'# Backpack design V2\n\nReview-only; BackPackPanel prefab is unchanged. V1 is preserved in BackpackDesignV1.\n\n- 1080 x 1920 reference, centered 920 x 1040 modal.\n- Categories: All, Currency, Materials. Current character fragments belong to Materials; Gold and Diamond belong to Currency.\n- Only the selected tab has a gold face and border; other tabs retain icon and text.\n- Category strip reserves horizontal scrolling when additional item types exceed the available width. Do not shrink all tab labels to fit.\n- Categories should be driven by item-type metadata/configuration when implemented; do not infer types from Chinese display names. Material is a broad category, with fragments as a subtype so additional materials can be added later.\n- Four columns; 194 x 250 item cards, 20px gap horizontally and 22px vertically.\n- Show positive-count real items only; no fixed six rows of blank cells.\n- The count is the number of item kinds in the selected category, not the sum of quantities. Switching categories resets the grid to the top and dismisses ItemTooltip.\n- Preserve item icons, counts, grade accents and existing ItemTooltip interaction.\n- Hide tooltip by tapping outside. Long descriptions scroll.\n- Show category-specific empty state and keep the tab strip visible.\n- Rows scroll for a larger inventory and short screens; no capacity/purchase/use/combine feature added.\n- Sample data uses all seven existing ItemTable items. Quantities shown are examples, not a player save.\n');
 console.log(path.join(out,'overview.png'));
})();
