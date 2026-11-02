// Review-only chapter designs: reuses the current Home UI assets and writes outside Assets.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const ROOT = path.resolve(__dirname, '../..');
const revised = process.argv[2] === 'seasonal-revised';
const seasonal = process.argv[2] === 'seasonal' || revised;
const OUT = path.join(ROOT, revised ? 'ArtOutput/UI/HomeChaptersV3' : seasonal ? 'ArtOutput/UI/HomeChaptersV2' : 'ArtOutput/UI/HomeChaptersV1');
const P = { ink: '#493627', muted: '#76634C', cream: '#FFF4DA' };
const W = 1080, H = 1920, viewport = { x: 0, y: 221, width: 1080, height: 1526 };
const chapters = [
  { id: 2, name: '成语篇', theme: '竹林书院', slug: 'bamboo_academy',
    points: [[241,1450],[680,1181],[534,970],[637,768],[832,647],[691,528],[821,390],[875,278]] },
  { id: 3, name: '滨水古镇', tentativeName: true, theme: '滨水古镇', slug: 'riverside_town',
    points: [[265,1318],[702,1056],[464,883],[670,739],[691,605],[734,432],[778,315],[940,230]] }
];
if (seasonal) {
  chapters[0].theme = '秋日书院'; chapters[0].season = 'autumn'; chapters[0].slug = 'bamboo_academy_autumn';
  chapters[1].theme = '冬雪水镇'; chapters[1].season = 'winter'; chapters[1].slug = 'riverside_town_winter';
}
if (revised) {
  chapters[0].slug = 'maple_academy_autumn';
}
const assets = new Map(), requests = new Map(), nine = new Map(), uris = new Map();
for (const a of JSON.parse(fs.readFileSync(path.join(ROOT, 'Tools/UI/home-assets.json'), 'utf8')).assets)
  assets.set(path.basename(a.path, path.extname(a.path)), a);
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;');
function uri(file) {
  if (!uris.has(file)) uris.set(file, 'data:image/png;base64,' + fs.readFileSync(file).toString('base64'));
  return uris.get(file);
}
function image(file, x, y, w, h, aspect = 'none') {
  return `<image x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="${aspect}" xlink:href="${uri(file)}"/>`;
}
function svg(w, h, body) { return `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`; }
function text(x, y, value, size = 38, color = P.ink, weight = 400, anchor = 'middle') {
  return `<text x="${x}" y="${y}" font-family="Microsoft YaHei, sans-serif" font-size="${size}" font-weight="${weight}" fill="${color}" text-anchor="${anchor}">${esc(value)}</text>`;
}
function centered(x, y, w, h, value, size = 38, color = P.ink, weight = 400) {
  return text(x + w/2, y + h/2 + size * .35, value, size, color, weight);
}
function asset(key, x, y, w, h) {
  const a = assets.get(key);
  if (!a) throw Error('Missing existing UI asset: ' + key);
  if (a.border.every(n => n === 0)) return image(path.join(ROOT, a.path), x, y, w, h);
  const token = `__NINE_${key}_${Math.round(w)}_${Math.round(h)}__`;
  requests.set(token, { a, w: Math.round(w), h: Math.round(h) });
  return `<image x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="none" xlink:href="${token}"/>`;
}
async function resolve(source) {
  for (const [token, { a, w, h }] of requests) {
    if (!source.includes(token)) continue;
    if (!nine.has(token)) {
      const file = path.join(ROOT, a.path), meta = await sharp(file).metadata();
      let [l,b,r,t] = a.border;
      const sx = [0,l,meta.width-r], sy = [0,t,meta.height-b], sw = [l,meta.width-l-r,r], sh = [t,meta.height-t-b,b];
      const scale = Math.min(1, w/(l+r), h/(t+b));
      const dl = Math.floor(l*scale), dr = Math.floor(r*scale), dt = Math.floor(t*scale), db = Math.floor(b*scale);
      const dx = [0,dl,w-dr], dy = [0,dt,h-db], dw = [dl,w-dl-dr,dr], dh = [dt,h-dt-db,db], parts = [];
      for (let j=0;j<3;j++) for (let i=0;i<3;i++) if (sw[i]>0 && sh[j]>0 && dw[i]>0 && dh[j]>0)
        parts.push({ input: await sharp(file).extract({left:sx[i],top:sy[j],width:sw[i],height:sh[j]}).resize(dw[i],dh[j],{fit:'fill'}).png().toBuffer(), left:dx[i], top:dy[j] });
      const buffer = await sharp({create:{width:w,height:h,channels:4,background:'#0000'}}).composite(parts).png().toBuffer();
      nine.set(token, 'data:image/png;base64,' + buffer.toString('base64'));
    }
    source = source.split(token).join(nine.get(token));
  }
  return source;
}
function node(ch, n, x, y, selected = 2) {
  const state = n < selected ? 'cleared' : n === selected ? 'available' : 'locked';
  const w = state === 'available' ? 181 : 165, h = state === 'available' ? 92 : 80;
  let body = asset('node_' + state + '_9slice', x-w/2,y-h/2,w,h);
  body += asset(state==='cleared'?'icon_check':state==='available'?'icon_flag':'icon_lock',x-w/2+18,y-h/2+20,42,42);
  body += centered(x-w/2+58,y-h/2,100,80,`${ch.id}-${n}`,state==='available'?43:38,P.ink,700);
  if (state==='cleared') for(let i=0;i<3;i++) body += asset('star_filled',x-57+i*36,y-84,36,36);
  if (n===selected) body += asset('node_selection_ring_9slice',x-103,y-53,206,106);
  return body;
}
function utility(icon, label, x, y, w, h) {
  return asset('surface_square_cream_9slice',x,y,w,h)+asset(icon,x+(w-68)/2,y+12,68,68)+centered(x,y+80,w,h-80,label,38,P.ink,700);
}
function chrome(ch) {
  let body = image(path.join(OUT,'shared_header.png'),0,0,1080,221);
  body += asset('surface_pill_cream_9slice',270,234.44,540,74.88);
  body += centered(286,234.44,380,75,`第${ch.id}章 · ${ch.name}`,ch.id===3?34:38,P.ink,700);
  body += asset('inset_progress_9slice',650,250.44,100,42)+centered(650,250.44,100,42,'1 / 8',29,P.muted);
  body += asset('surface_locate_9slice',40,336.2,241,113.14)+asset('icon_locate',56,370.2,44,44)+centered(105,336.2,162,113,'当前关卡',32);
  body += utility('icon_backpack','背包',924.4,826.6,115.6,133.9)+utility('icon_map','关卡',924.4,982.1,115.6,133.9);
  const tip = ch.id===3?'第3章 · 关卡3-2':`${ch.name} · 关卡${ch.id}-2`;
  body += asset('info_pill_cream_9slice',386,1532.24,308,56.2)+centered(386,1532.24,308,56.2,tip,30,P.muted);
  body += asset('button_primary_normal_9slice',234.9,1599.24,610.2,126.36)+asset('icon_swords',278,1628,72,72)+centered(330,1599.24,460,126.36,'开始游戏',54,P.ink,700);
  body += image(path.join(OUT,'shared_navigation.png'),0,1747,1080,173);
  return body;
}
function compose(ch, overview=false) {
  const offset = Math.max(0,Math.min(1920-viewport.height, ch.points[1][1]-viewport.height*.68));
  let world = image(path.join(OUT,'backgrounds',`chapter_0${ch.id}_${ch.slug}_1080x1920.png`),0,0,W,H);
  const [hx,hy] = ch.points[1];
  world += asset('hero_shadow',hx-83,hy-67,166,35);
  world += image(path.join(ROOT,'Assets/Res/image/Game/role/player02.png'),hx-83,hy-223,166,173,'xMidYMid meet');
  ch.points.forEach(([x,y],i)=>world+=node(ch,i+1,x,y));
  if (overview) return svg(W,H,world);
  return svg(W,H,`<defs><clipPath id="map-clip"><rect x="0" y="221" width="1080" height="1526"/></clipPath></defs><rect width="1080" height="1920" fill="${P.cream}"/><g clip-path="url(#map-clip)"><g transform="translate(0 ${viewport.y-offset})">${world}</g></g>${chrome(ch)}`);
}
async function main() {
  fs.mkdirSync(OUT,{recursive:true});
  const existing = path.join(ROOT,'ArtOutput/UI/HomeRoadmap/home-final-1080x1920.png');
  await sharp(existing).extract({left:0,top:0,width:1080,height:221}).png().toFile(path.join(OUT,'shared_header.png'));
  await sharp(existing).extract({left:0,top:1747,width:1080,height:173}).png().toFile(path.join(OUT,'shared_navigation.png'));
  const report = { purpose:'Design mockups only; no Unity Assets, prefabs, scenes, data or player saves modified.', canvas:[W,H], viewport, imageGeneration:'built-in image_gen', font:'Existing project UI uses msyh SDF; these review PNG labels are rasterized with Microsoft YaHei and introduce no game font asset.', chapters:[] };
  for (const ch of chapters) {
    const name = `chapter_0${ch.id}_${ch.slug}`;
    const source = path.join(OUT,'backgrounds',name+'_source.png');
    await sharp(source).resize(W,H,{fit:'fill'}).png({compressionLevel:9}).toFile(path.join(OUT,'backgrounds',name+'_1080x1920.png'));
    await sharp(source).resize(W,H,{fit:'fill'}).jpeg({quality:88,mozjpeg:true}).toFile(path.join(OUT,'backgrounds',name+'_1080x1920.jpg'));
    for (const overview of [false,true]) {
      const doc=await resolve(compose(ch,overview));
      const stem=path.join(OUT,name+(overview?'_route_overview':'_home'));
      fs.writeFileSync(stem+'.svg',doc);
      await sharp(Buffer.from(doc)).png({compressionLevel:9}).toFile(stem+'_1080x1920.png');
      if (!overview) await sharp(stem+'_1080x1920.png').resize(360,640).png().toFile(stem+'_360x640.png');
    }
    const scrollOffset=Math.max(0,Math.min(H-viewport.height,ch.points[1][1]-viewport.height*.68));
    report.chapters.push({...ch,selectedStage:2,clearedStages:[1],lockedStages:[3,4,5,6,7,8],scrollOffset,worldOriginTop:viewport.y-scrollOffset,nodeCoordinates:'1080x1920 illustration pixels, origin top-left; intended per-chapter coordinates, not yet wired to current shared Points array.',jpgBytes:fs.statSync(path.join(OUT,'backgrounds',name+'_1080x1920.jpg')).size});
  }
  const panels=[];
  for (let i=0;i<chapters.length;i++) {
    const ch=chapters[i];
    panels.push({input:await sharp(path.join(OUT,`chapter_0${ch.id}_${ch.slug}_home_1080x1920.png`)).resize(486,864).png().toBuffer(),left:42+i*528,top:112});
  }
  const heading=svg(1080,1020,`<rect width="1080" height="1020" fill="#EEE7D6"/>${text(42,45,revised?'章节主界面 · 秋冬修订版':seasonal?'章节主界面 · 秋冬版':'章节主界面 · 第一版',26,P.ink,700,'start')}${text(285,91,seasonal?'第二章 · 成语篇 / 秋日书院':'第二章 · 成语篇 / 竹林书院',20,P.muted)}${text(813,91,seasonal?'第三章 · 滨水古镇 / 冬雪水镇':'第三章 · 滨水古镇（暂定主题）',20,P.muted)}`);
  await sharp(Buffer.from(heading)).composite(panels).png({compressionLevel:9}).toFile(path.join(OUT,'chapters_02_03_comparison.png'));
  fs.writeFileSync(path.join(OUT,'layout.json'),JSON.stringify(report,null,2));
  console.log(JSON.stringify({output:OUT,chapters:report.chapters.map(ch=>({id:ch.id,nodes:ch.points.length,jpgBytes:ch.jpgBytes,scrollOffset:ch.scrollOffset}))}));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
