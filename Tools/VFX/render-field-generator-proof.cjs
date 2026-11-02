// Real Game scene samples and continuous damaging aura. Keep captured time intervals.
const fs=require('node:fs'),path=require('node:path'),{spawnSync}=require('node:child_process');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/UI/FieldGeneratorV1');
const ffmpeg='D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
const label=(x,y,s,size=19)=>`<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${s}</text>`;
async function main(){
  const summary=[];
  for(const version of ['Before','After']){
    const report=JSON.parse(fs.readFileSync(path.join(dir,version,'validation-report.json'),'utf8'));
    const motion=JSON.parse(fs.readFileSync(path.join(dir,version,'motion.json'),'utf8'));
    if(!report.passed||!motion.damaged)throw Error('Actual field damage verification must pass.');
    const times=JSON.parse(fs.readFileSync(path.join(dir,version,'frame-times.json'),'utf8'));let list='';
    for(let i=0;i<times.length;i++){
      if(!Number.isFinite(times[i])||(i&&times[i]<times[i-1]))throw Error('Invalid capture timing.');
      const frame=path.join(dir,version,'frames',`frame-${String(i).padStart(4,'0')}.png`).replace(/\\/g,'/');
      list+=`file '${frame}'\nduration ${i+1<times.length?Math.max(.001,times[i+1]-times[i]):.04}\n`;
    }
    const concat=path.join(dir,version,'runtime.concat');fs.writeFileSync(concat,list);
    const result=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',concat,'-vf','fps=30','-c:v','libx264','-crf','20','-pix_fmt','yuv420p','-movflags','+faststart',path.join(dir,version.toLowerCase()+'.mp4')],{encoding:'utf8'});
    if(result.status)throw Error(result.stderr);
    summary.push({version,checks:report.checks.length,...motion});
  }
  const gif=spawnSync(ffmpeg,['-hide_banner','-loglevel','error','-y','-i',path.join(dir,'after.mp4'),'-filter_complex','fps=20,scale=360:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',path.join(dir,'after.gif')],{encoding:'utf8'});
  if(gif.status)throw Error(gif.stderr);
  const parts=[];
  for(let row=0;row<2;row++)for(let col=0;col<2;col++){
    const source=path.join(dir,col?'After':'Before',row?'max.png':'normal.png');
    parts.push({input:await sharp(source).extract({left:230,top:866,width:680,height:512}).resize(425,320).png().toBuffer(),left:18+col*445,top:105+row*362});
  }
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="908" height="835"><rect width="100%" height="100%" fill="#F4E9D0"/>${label(18,32,'罡气护体 · 实际技能改版对照',26)}${label(18,64,'原版：填充底盘 + 三圈淡色脉冲')}${label(463,64,'新版：外缘 + 旋转罡气 + 扩散脉冲')}${label(18,91,'普通状态',18)}${label(18,453,'突破状态',18)}${label(18,818,'同一技能数值 / 真实 Game 场景 / 512×256 共用图集 / 无微信真机性能结论',17)}</svg>`;
  await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir,'comparison.png'));
  fs.writeFileSync(path.join(dir,'proof.json'),JSON.stringify({retimed:false,summary},null,2));
  console.log(JSON.stringify({dir,summary}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
