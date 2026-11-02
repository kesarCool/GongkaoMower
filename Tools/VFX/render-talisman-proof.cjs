// Proof export from real Unity captures; no reconstructed gameplay or retimed animation.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const output = path.join(root, 'ArtOutput/UI/TalismanPresentationV1');
const ffmpeg = 'D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
const text = (x,y,s,size=21) => `<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${s}</text>`;
async function main() {
  fs.mkdirSync(output,{recursive:true});
  const counts = {};
  for (const version of ['Before','After']) {
    const source = path.join(root,'ArtOutput/UI/Talisman'+version);
    const report = JSON.parse(fs.readFileSync(path.join(source,'validation-report.json'),'utf8'));
    const motion = JSON.parse(fs.readFileSync(path.join(source,'motion.json'),'utf8'));
    const times = JSON.parse(fs.readFileSync(path.join(source,'frame-times.json'),'utf8'));
    if (!report.passed || !motion.damaged || !motion.fired || (version==='After' && !motion.visible)) throw Error('Real collision validation must pass.');
    counts[version]=report.checks.length;
    let list='';
    for (let i=0;i<times.length;i++) {
      if(!Number.isFinite(times[i]) || (i && times[i]<times[i-1])) throw Error('Invalid timing');
      list+=`file '${path.join(source,'frames',`frame-${String(i).padStart(4,'0')}.png`).replace(/\\/g,'/')}'\nduration ${i+1<times.length?Math.max(.01,times[i+1]-times[i]):.25}\n`;
    }
    const concat=path.join(output,version.toLowerCase()+'.concat'); fs.writeFileSync(concat,list);
    const run=args=>{const r=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',concat,...args],{encoding:'utf8'});if(r.status)throw Error(r.stderr);};
    run(['-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(output,version.toLowerCase()+'.mp4')]);
    run(['-filter_complex','fps=20,scale=360:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(output,version.toLowerCase()+'.gif')]);
  }
  let svg=`<svg xmlns="http://www.w3.org/2000/svg" width="920" height="730"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(20,33,'天师符令 · Unity 真实飞行与命中采样',27)}${text(20,65,'旧版',23)}${text(470,65,'新版：朱红符纸 / 柔边双层拖尾 / 符印命中',20)}`;
  const parts=[];
  for (const [row,name,crop] of [[0,'max-flight',{left:420,top:1060,width:240,height:150}],[1,'max-hit',{left:380,top:700,width:320,height:220}]]) {
    const top=row?410:98;
    svg+=text(20,top-10,row?'命中瞬间 · 特效位于怪物文字下层':'飞行中 · 相同技能配置，局部放大',18);
    for (let col=0;col<2;col++) {
      const source=path.join(root,'ArtOutput/UI/Talisman'+(col?'After':'Before'),name+'.png');
      const input=await sharp(source).extract(crop).resize(430,row?296:269).png().toBuffer();
      parts.push({input,left:20+col*450,top});
    }
  }
  svg+=text(20,722,'512×256 共享图集 / PNG 43 KiB / 拖尾无新增贴图；未做微信真机及最终包体测量',17)+'</svg>';
  await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(output,'comparison.png'));
  fs.writeFileSync(path.join(output,'proof.json'),JSON.stringify({source:'real Unity Game scene captures',counts,retimed:false,output},null,2));
  console.log(JSON.stringify({output,counts}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
