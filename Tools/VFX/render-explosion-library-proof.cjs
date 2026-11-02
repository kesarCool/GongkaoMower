// Export proof from actual Unity captures, preserving the captured frame timings.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const output = path.join(root, 'ArtOutput/UI/ExplosionLibraryV2');
const ffmpeg = 'D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
const labels = { compact: '石破天惊 · 普通爆炸', chain: '连环爆破 · 星芒爆炸', missile: '穿云破雾 · 导弹穿刺', heavy: '追风逐日 · 重型爆炸' };
const text = (x, y, value, size = 21) => `<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${value}</text>`;
async function main() {
  const report = JSON.parse(fs.readFileSync(path.join(output, 'validation-report.json'), 'utf8'));
  if (!report.passed) throw Error('The full runtime validation must pass before proof export.');
  const { clips } = JSON.parse(fs.readFileSync(path.join(output, 'clips.json'), 'utf8'));
  if (clips.length !== 4 || clips.some(c => !c.damaged || c.visibleFrames < 1 || !c.times.length)) throw Error('Each clip must contain a real damaging impact.');
  let combined = '';
  for (const clip of clips) {
    const frames = path.join(output, 'proof-frames', clip.name); fs.mkdirSync(frames, { recursive: true });
    const header = await sharp(Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="540" height="62"><rect width="100%" height="100%" fill="#FFF4DB"/>${text(12, 26, labels[clip.name], 23)}${text(12, 50, '实际施放回放 · 原战斗数值与结算保留', 16)}</svg>`)).png().toBuffer();
    let list = '';
    for (let i = 0; i < clip.times.length; i++) {
      if (!Number.isFinite(clip.times[i]) || (i && clip.times[i] < clip.times[i - 1])) throw Error('Invalid capture timing.');
      const source = path.join(output, clip.name + '-frames', `frame-${String(i).padStart(4, '0')}.png`);
      const target = path.join(frames, `frame-${String(i).padStart(4, '0')}.png`);
      const game = await sharp(source).resize(540, 960).png().toBuffer();
      await sharp({ create: { width: 540, height: 1022, channels: 4, background: '#FFF4DB' } }).composite([{ input: header, left: 0, top: 0 }, { input: game, left: 0, top: 62 }]).png().toFile(target);
      list += `file '${target.replace(/\\/g, '/')}'\nduration ${i + 1 < clip.times.length ? Math.max(.01, clip.times[i + 1] - clip.times[i]) : .25}\n`;
    }
    combined += list;
  }
  const concat = path.join(output, 'recording.concat'); fs.writeFileSync(concat, combined);
  const run = args => { const result = spawnSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', concat, ...args], { encoding: 'utf8' }); if (result.status) throw Error(result.stderr); };
  run(['-vf', 'fps=30', '-c:v', 'libx264', '-crf', '20', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', path.join(output, 'runtime-library.mp4')]);
  run(['-filter_complex', 'fps=20,scale=360:-2:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=192:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle', '-loop', '0', path.join(output, 'runtime-library.gif')]);
  const names = Object.keys(labels), parts = [];
  let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1220" height="800"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(20, 36, '共享爆炸库 · 四款轮廓 / 一张图集', 30)}${text(20, 68, '真实 Unity 粒子采样；上排浅色沙地，下排草地夹具；文字始终在特效上层', 20)}`;
  for (let row = 0; row < 2; row++) for (let col = 0; col < 4; col++) {
    const name = names[col], left = 20 + col * 300, top = row ? 472 : 125;
    svg += text(left, top - 14, labels[name], 20);
    const crop = await sharp(path.join(output, `${name}-${row ? 'grass' : 'sand'}.png`)).extract({ left: 280, top: 675, width: 520, height: 500 }).resize(280, 269).png().toBuffer();
    parts.push({ input: crop, left, top });
  }
  svg += text(20, 778, '1024×512 / 每帧128px / PNG 351KiB / 共用材质 · 未做微信真机与最终包体测量', 20) + '</svg>';
  await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(output, 'gallery.png'));
  console.log(JSON.stringify({ output, checks: report.checks.length, clips: clips.map(c => ({ name: c.name, frames: c.times.length, visible: c.visibleFrames })) }));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
