// Assemble actual Unity screenshots only. Never redraw the generated atlas or gameplay.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const output = path.join(root, 'ArtOutput/UI/GrenadeExplosionV1');
const ffmpeg = 'D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
async function main() {
  const report = JSON.parse(fs.readFileSync(path.join(output, 'validation-report.json'), 'utf8'));
  if (!report.passed || !report.checks.some(c => c.passed && c.name === 'Motion captures a real damaging cast and visible explosion'))
    throw new Error('Real damaging-cast validation must pass before exporting proof.');
  const times = JSON.parse(fs.readFileSync(path.join(output, 'frame-times.json'), 'utf8'));
  if (!times.length || times.some((v, i) => !Number.isFinite(v) || (i > 0 && v < times[i - 1]))) throw new Error('Invalid capture timing.');
  let concat = '';
  for (let i = 0; i < times.length; i++) {
    const source = path.join(output, 'frames', `frame-${String(i).padStart(4, '0')}.png`);
    if (!fs.existsSync(source)) throw new Error('Missing captured frame: ' + source);
    concat += `file '${source.replace(/\\/g, '/')}'\nduration ${i + 1 < times.length ? Math.max(.01, times[i + 1] - times[i]) : .25}\n`;
  }
  concat += `file '${path.join(output, 'frames', `frame-${String(times.length - 1).padStart(4, '0')}.png`).replace(/\\/g, '/')}'\n`;
  const list = path.join(output, 'recording.concat');
  fs.writeFileSync(list, concat);
  const run = args => {
    const result = spawnSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', list, ...args], { encoding: 'utf8' });
    if (result.status !== 0) throw new Error(result.stderr);
  };
  run(['-vf', 'fps=30', '-c:v', 'libx264', '-crf', '20', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', path.join(output, 'runtime-cast.mp4')]);
  run(['-filter_complex', 'fps=20,scale=360:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=160:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle', '-loop', '0', path.join(output, 'runtime-cast.gif')]);
  const text = (x, y, s, size = 22) => `<text x="${x}" y="${y}" font-family="Microsoft YaHei, sans-serif" fill="#493627" font-size="${size}">${s}</text>`;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="916" height="930"><rect width="100%" height="100%" fill="#F6EBD2"/>${text(24, 36, '石破天惊 · Unity 实际落地效果', 28)}${text(24, 67, '同一站位与命中结果；静态图采样于特效 0.075 秒，便于比较', 19)}${text(24, 106, '旧版 · 共用火焰')}${text(472, 106, '新版 · 卡通爆炸序列帧')}${text(24, 911, 'Editor 验证通过 · 未做微信真机与发布包体测量', 19)}</svg>`;
  const before = await sharp(path.join(output, 'legacy-impact-1080x1920.png')).resize(420, 747).png().toBuffer();
  const after = await sharp(path.join(output, 'impact-1080x1920.png')).resize(420, 747).png().toBuffer();
  await sharp(Buffer.from(svg)).composite([{ input: before, left: 24, top: 124 }, { input: after, left: 472, top: 124 }]).png().toFile(path.join(output, 'comparison.png'));
  console.log(JSON.stringify({ output, checks: report.checks.length, capturedFrames: times.length, gif: 'runtime-cast.gif', video: 'runtime-cast.mp4' }));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
