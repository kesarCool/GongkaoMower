// Design preview only: preserves Unity source assets, scenes and login flow.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const ROOT = path.resolve(__dirname, '../..');
const OUT = path.join(ROOT, 'ArtOutput/UI/LoginOriginalPosterV1');
const W=1080,H=1920, FONT='Microsoft YaHei, sans-serif';
const P={ink:'#493627',muted:'#76634C',cream:'#FFF4DA',gold:'#F6B94E'};
const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const uri=file=>'data:image/png;base64,'+fs.readFileSync(file).toString('base64');
const image=(file,x,y,w,h)=>`<image x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="none" xlink:href="${uri(file)}"/>`;
const svg=(w,h,body)=>`<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
const text=(x,y,value,size=30,color=P.ink,weight=400,anchor='middle')=>`<text x="${x}" y="${y}" font-family="${FONT}" font-size="${size}" fill="${color}" font-weight="${weight}" text-anchor="${anchor}">${esc(value)}</text>`;
const common=name=>path.join(ROOT,'Assets/Res/image/common',name+'.png');
function title() {
  const g=`font-family="${FONT}" font-size="148" font-weight="900" text-anchor="middle" stroke-linejoin="round" paint-order="stroke"`;
  return `<defs><linearGradient id="title-gold" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFF4BC"/><stop offset=".42" stop-color="#FFD67A"/><stop offset="1" stop-color="#EDA73A"/></linearGradient></defs><g transform="translate(520 184) rotate(-3) skewX(-4)"><text x="0" y="16" ${g} stroke="#7A4A23" stroke-width="30" fill="#7A4A23">文字割草机</text><text x="0" y="0" ${g} stroke="#FFF2C7" stroke-width="35" fill="#FFF2C7">文字割草机</text><text x="0" y="0" ${g} stroke="${P.ink}" stroke-width="23" fill="url(#title-gold)">文字割草机</text><text x="0" y="-1" ${g} stroke="#FFF1BB" stroke-width="2" fill="url(#title-gold)">文字割草机</text></g>`;
}
async function nineSlice(file,w,h,border) {
  const m=await sharp(file).metadata(),[l,b,r,t]=border;
  const sx=[0,l,m.width-r],sy=[0,t,m.height-b],sw=[l,m.width-l-r,r],sh=[t,m.height-t-b,b];
  const dx=[0,l,w-r],dy=[0,t,h-b],dw=[l,w-l-r,r],dh=[t,h-t-b,b],parts=[];
  for(let j=0;j<3;j++)for(let i=0;i<3;i++)if(sw[i]>0&&sh[j]>0&&dw[i]>0&&dh[j]>0)
    parts.push({input:await sharp(file).extract({left:sx[i],top:sy[j],width:sw[i],height:sh[j]}).resize(dw[i],dh[j],{fit:'fill'}).png().toBuffer(),left:dx[i],top:dy[j]});
  return sharp({create:{width:w,height:h,channels:4,background:'#0000'}}).composite(parts).png().toBuffer();
}
function capsule() {
  return `<g opacity=".72"><rect x="804" y="72" width="236" height="82" rx="41" fill="#49362735"/><g fill="${P.cream}"><circle cx="844" cy="113" r="5"/><circle cx="863" cy="113" r="5"/><circle cx="882" cy="113" r="5"/><circle cx="984" cy="113" r="20" fill="none" stroke="${P.cream}" stroke-width="5"/><circle cx="984" cy="113" r="8"/></g></g>`;
}
function compose(withCapsule=true) {
  const footer=['健康游戏忠告','抵制不良游戏，拒绝盗版游戏。','注意自我保护，谨防受骗上当。','适度游戏益脑，沉迷游戏伤身。','合理安排时间，享受健康生活。'];
  let body=`<defs><linearGradient id="bottomVeil" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="${P.cream}" stop-opacity="0"/><stop offset=".56" stop-color="${P.cream}" stop-opacity=".24"/><stop offset="1" stop-color="${P.cream}" stop-opacity=".6"/></linearGradient></defs>`;
  body+=image(path.join(OUT,'backgrounds/login_background_1080x1920.png'),0,0,W,H);
  body+=text(48,116,'v1.1.18',27,P.muted,500,'start');
  if(withCapsule)body+=capsule();
  body+=image(path.join(OUT,'elements/game_title_1040x250.png'),20,212,1040,250);
  body+=`<rect x="0" y="1530" width="1080" height="390" fill="url(#bottomVeil)"/>`;
  body+=image(path.join(OUT,'elements/start_button_preview.png'),200,1580,680,136);
  body+=image(common('icon_swords'),256,1612,72,72);
  body+=text(568,1669,'开始游戏',56,P.ink,700);
  footer.forEach((line,i)=>body+=text(540,1764+i*29,line,i===0?25:24,P.muted,i===0?700:400));
  return svg(W,H,body);
}
async function main() {
  fs.mkdirSync(path.join(OUT,'elements'),{recursive:true});
  const source=path.join(OUT,'sources/login_background_generated.png');
  await sharp(source).resize(W,H,{fit:'fill'}).png({compressionLevel:9}).toFile(path.join(OUT,'backgrounds/login_background_1080x1920.png'));
  await sharp(source).resize(W,H,{fit:'fill'}).jpeg({quality:86,mozjpeg:true}).toFile(path.join(OUT,'backgrounds/login_background_1080x1920.jpg'));
  const logo=svg(1040,250,title());fs.writeFileSync(path.join(OUT,'elements/game_title_1040x250.svg'),logo);
  await sharp(Buffer.from(logo)).png({compressionLevel:9}).toFile(path.join(OUT,'elements/game_title_1040x250.png'));
  const manifest=JSON.parse(fs.readFileSync(path.join(ROOT,'Tools/UI/home-assets.json'),'utf8'));
  const button=manifest.assets.find(a=>a.path.endsWith('/button_primary_normal_9slice.png'));
  await fs.promises.writeFile(path.join(OUT,'elements/start_button_preview.png'),await nineSlice(path.join(ROOT,button.path),680,136,button.border));
  for(const withCapsule of [true,false]) {
    const stem=withCapsule?'login_original_promo':'login_without_platform_capsule';
    const doc=compose(withCapsule);
    fs.writeFileSync(path.join(OUT,stem+'_1080x1920.svg'),doc);
    await sharp(Buffer.from(doc)).png({compressionLevel:9}).toFile(path.join(OUT,stem+'_1080x1920.png'));
    if(withCapsule)await sharp(path.join(OUT,stem+'_1080x1920.png')).resize(360,640).png().toFile(path.join(OUT,stem+'_360x640.png'));
  }
  const backgroundBytes=fs.statSync(path.join(OUT,'backgrounds/login_background_1080x1920.jpg')).size;
  const titleBytes=fs.statSync(path.join(OUT,'elements/game_title_1040x250.png')).size;
  const layout={design:[W,H],title:'文字割草机',mode:'design preview only; no Unity source, scene or build changes',imageGeneration:'built-in image_gen',originalPromotionalImage:'sources/original_promo.png',background:{path:'backgrounds/login_background_1080x1920.jpg',bytes:backgroundBytes,mode:'full-bleed; preserve aspect ratio; do not stretch the hero'},gameTitle:{path:'elements/game_title_1040x250.png',rect:[20,212,1040,250],note:'baked transparent title; no additional runtime font library'},version:{rect:[48,86,240,44],fontSize:27,color:P.muted,value:'runtime Application.version; v1.1.18 shown in preview'},startButton:{rect:[200,1580,680,136],reuse:button.path,icon:'Assets/Res/image/common/icon_swords.png',text:'开始游戏',fontSize:56,textColor:P.ink},healthNotice:{firstBaseline:1764,lineHeight:29,fontSize:24,color:P.muted},platformCapsule:{rect:[804,72,236,82],previewOnly:true},futureIntegration:{font:'reuse existing game TMP font for normal text; title is baked art',flow:'retain LoginSceneController first-player / returning-player navigation',safeArea:'top version and bottom controls need SafeArea on actual device; tall screens expand background crop, not UI scale'},encodingEstimate:{jpgBytes:backgroundBytes,titlePngBytes:titleBytes,totalCandidateBytes:backgroundBytes+titleBytes,note:'Disk encoding only, not predicted Brotli package increment. Existing button and icon can be reused.'}};
  fs.writeFileSync(path.join(OUT,'layout.json'),JSON.stringify(layout,null,2));
  console.log(JSON.stringify({output:OUT,preview:'login_original_promo_1080x1920.png',backgroundJpgBytes:backgroundBytes,titlePngBytes:titleBytes}));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
