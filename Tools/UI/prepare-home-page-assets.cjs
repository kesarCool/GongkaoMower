const fs=require('node:fs'),path=require('node:path');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),records=[];
const wrap=(w,h,body)=>`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
async function save(folder,name,body,w,h,border=[0,0,0,0]){
 const file=`Assets/Res/image/${folder}/${name}.png`,full=path.join(root,file);fs.mkdirSync(path.dirname(full),{recursive:true});
 const buffer=await sharp(Buffer.from(wrap(w,h,body))).png().toBuffer();
 if(fs.existsSync(full)&&!fs.readFileSync(full).equals(buffer))throw Error('Refusing to replace existing asset '+file);
 if(!fs.existsSync(full))fs.writeFileSync(full,buffer);
 records.push({path:file,border,maxSize:256});
}
const frame=(fill,stroke,r=28)=>`<rect x="1.5" y="1.5" width="125" height="125" rx="${r}" fill="${fill}" stroke="${stroke}" stroke-width="3"/>`;
async function main(){
 for(const [name,fill,stroke]of [['home_card_cream_9slice','#FFF9EA','#D7C8A6'],['home_card_claimable_9slice','#FFF3CD','#CCA05A'],['home_card_muted_9slice','#EEE9DC','#D7CEBA']])await save('common',name,frame(fill,stroke),128,128,[30,30,30,30]);
 for(const [name,fill,stroke]of [['bonus_gold_card_9slice','#FFF7E4','#D7C8A6'],['bonus_gem_card_9slice','#F3F6E9','#D7C8A6']])await save('Home/exchange',name,frame(fill,stroke),128,128,[30,30,30,30]);
 for(const [name,fill,stroke]of [['filter_normal_9slice','#F1EDDE','#DED4BD'],['filter_selected_9slice','#F7CC76','#B08644']])await save('Home/lexicon',name,`<rect x="1.5" y="1.5" width="125" height="81" rx="18" fill="${fill}" stroke="${stroke}" stroke-width="3"/>`,128,84,[20,20,20,20]);
 await save('common','icon_ad_video','<g fill="none" stroke="#493627" stroke-width="3" stroke-linejoin="round"><rect x="2" y="5" width="36" height="28" rx="6"/><path d="M16 12L27 19L16 26Z" fill="#493627" stroke="none"/><path d="M12 38H28"/></g>',40,40);
 await save('common','icon_refresh_clock','<g stroke="#76634C" fill="none" stroke-width="3.2" stroke-linecap="round"><circle cx="20" cy="20" r="16"/><path d="M20 10V20L28 24"/></g>',40,40);
 await save('common','icon_fragment_piece','<path d="M4 9H17C12 -1 34 -1 29 9H42V22C32 16 32 36 42 30V43H29C35 33 13 33 19 43H4V30C14 36 14 16 4 22Z" fill="#F5C065" stroke="#9D733D" stroke-width="3" stroke-linejoin="round"/>',48,48);
 for(const [name,fill]of [['home_badge_mint_9slice','#E2E9D1'],['home_badge_sand_9slice','#E9E5D1'],['home_progress_track_9slice','#E3DCC8'],['home_progress_green_9slice','#8CAB72'],['home_progress_gold_9slice','#D7A747'],['home_number_green_9slice','#E7ECD7'],['home_number_gold_9slice','#F3E5BD']])await save('common',name,`<rect width="64" height="64" rx="18" fill="${fill}"/>`,64,64,[20,20,20,20]);
 for(const [name,key]of [['reward_coins','icon_coins'],['reward_gems','icon_gem']]){
  const data=fs.readFileSync(path.join(root,'Assets/Res/image/common',key+'.png')).toString('base64');
  const body=`<ellipse cx="80" cy="125" rx="76" ry="13" fill="${key==='icon_coins'?'#D9B75E35':'#6AB3B72A'}"/><image href="data:image/png;base64,${data}" x="46" y="5" width="98" height="98"/><image href="data:image/png;base64,${data}" x="4" y="34" width="100" height="100"/><image href="data:image/png;base64,${data}" x="80" y="52" width="76" height="76"/>`;
  await save('Home/exchange',name,body,160,150);
 }
 fs.writeFileSync(path.join(root,'Tools/UI/home-pages-assets.json'),JSON.stringify({assets:records},null,2));
 console.log(JSON.stringify({assets:records.length,folders:['Home/exchange','Home/lexicon','common']},null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
