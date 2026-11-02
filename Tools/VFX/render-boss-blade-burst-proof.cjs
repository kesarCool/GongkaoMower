const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const dir = path.join(root, 'ArtOutput/UI/BossBladeBurstV1');
const ffmpeg = 'D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
const ffprobe = 'D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffprobe.exe';
const relative = p => path.relative(root, p).replace(/\\/g, '/');
const quoted = p => p.replace(/\\/g, '/').replace(/'/g, "'\\''");
function run(exe, args) {
  const p = spawnSync(exe, args, { encoding: 'utf8' });
  if (p.status !== 0) throw Error(p.stderr || p.error || `${exe} failed`);
  return p.stdout;
}
const encode = args => run(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', ...args]);
function combine(clips, name) {
  const concat = path.join(dir, name + '.concat');
  fs.writeFileSync(concat, clips.map(p => `file '${quoted(p)}'`).join('\n'));
  const out = path.join(dir, name + '.mp4');
  encode(['-f', 'concat', '-safe', '0', '-i', concat, '-c', 'copy', '-movflags', '+faststart', out]);
  return out;
}
async function main() {
  const reports = ['Before', 'After'].map(s => {
    const folder = path.join(dir, s);
    const r = JSON.parse(fs.readFileSync(path.join(folder, 'validation-report.json'), 'utf8'));
    if (!r.passed || !r.checks.every(c => c.passed) || fs.readFileSync(path.join(folder, 'status.txt'), 'utf8').trim() !== 'passed') throw Error(s + ' real skill validation must pass first');
    return { suite: s, checks: r.checks.length };
  });
  const captures = [], groups = {};
  for (const suite of ['Before', 'After']) {
    groups[suite] = [];
    const names = ['eight-motion', 'twelve-motion', 'sixteen-motion'];
    if (suite === 'After') names.push('two-clones-motion', 'four-clones-motion');
    for (const name of names) {
      const folder = path.join(dir, suite, name);
      const times = JSON.parse(fs.readFileSync(path.join(folder, 'frame-times.json'), 'utf8'));
      const motion = JSON.parse(fs.readFileSync(path.join(folder, 'motion.json'), 'utf8'));
      if (motion.retimed || motion.timeScale !== 1 || motion.frames !== times.length || times.length < 2) throw Error('Not a valid real-time motion capture: ' + folder);
      let list = '';
      const last = path.join(folder, 'frames', `frame-${String(times.length - 1).padStart(4, '0')}.png`);
      for (let i = 0; i < times.length; i++) {
        if (!Number.isFinite(times[i]) || (i && times[i] <= times[i - 1])) throw Error('Invalid capture timing');
        const file = path.join(folder, 'frames', `frame-${String(i).padStart(4, '0')}.png`);
        if (!fs.existsSync(file)) throw Error('Missing ' + file);
        list += `file '${quoted(file)}'\nduration ${i + 1 < times.length ? times[i + 1] - times[i] : 1 / 30}\n`;
      }
      // Repeat the final image so concat honors its last duration. fps only duplicates/drops frames.
      list += `file '${quoted(last)}'\n`;
      const concat = path.join(folder, 'capture.concat');
      fs.writeFileSync(concat, list);
      const out = path.join(dir, suite.toLowerCase() + '-' + name + '.mp4');
      encode(['-f', 'concat', '-safe', '0', '-i', concat, '-vf', 'fps=30', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', out]);
      groups[suite].push(out);
      const duration = Number(run(ffprobe, ['-v', 'error', '-show_entries', 'format=duration', '-of', 'default=noprint_wrappers=1:nokey=1', out]).trim());
      const sourceSeconds = times[times.length - 1] - times[0];
      if (Math.abs(duration - sourceSeconds) > .15) throw Error('Timestamp duration drift: ' + out);
      captures.push({ source: relative(folder), video: relative(out), frames: times.length, sourceSeconds, encodedSeconds: duration, ...motion });
    }
  }
  combine(groups.Before, 'before');
  const after = combine(groups.After.slice(0, 3), 'after');
  combine(groups.After.slice(3), 'stress');
  encode(['-i', after, '-vf', 'crop=540:540:0:160,scale=720:720', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', path.join(dir, 'after-closeup.mp4')]);
  const rows = [
    ['eight-charge', '8 向：蓄力与发射方向'],
    ['twelve-flight', '12 向：刀气主体与短拖尾'],
    ['sixteen-grass-flight', '16 向：草地辨识度']
  ];
  const parts = [];
  for (let row = 0; row < rows.length; row++) for (let col = 0; col < 2; col++) {
    const p = path.join(dir, col ? 'After' : 'Before', rows[row][0] + '.png');
    parts.push({ input: await sharp(p).extract({ left: 140, top: 540, width: 860, height: 680 }).resize(430, 340).png().toBuffer(), left: 18 + col * 452, top: 116 + row * 384 });
  }
  const text = (x, y, s, size = 19) => `<text x="${x}" y="${y}" font-family="Microsoft YaHei,sans-serif" font-size="${size}" fill="#493627">${s}</text>`;
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="918" height="1288"><rect width="100%" height="100%" fill="#F4E9D0"/>${text(18,34,'Boss 刀气爆发 · 实战前后对比',27)}${text(18,70,'原效果')}${text(470,70,'新版：朱红刀气与方向预警')}${rows.map((r,i)=>text(18,105+i*384,r[1])).join('')}${text(18,1275,'真实 Game 截图；前后为独立运行，静态截图冻结；视频按原时间戳导出。',16)}</svg>`;
  await sharp(Buffer.from(svg)).composite(parts).png().toFile(path.join(dir, 'comparison.png'));
  fs.writeFileSync(path.join(dir, 'proof.json'), JSON.stringify({ passed: true, reports, retimed: false, captures, note: 'Real Game timestamped captures at timeScale 1. Constant output FPS only duplicates/drops captured frames. Static screenshots freeze the scene separately. Clone pressure clips use manually placed CloneMarker casters, not CloneModule inheritance validation. Not device FPS or upload package measurement.' }, null, 2));
  console.log(JSON.stringify({ dir: relative(dir), reports, videos: captures.length, retimed: false }));
}
main().catch(e => { console.error(e); process.exitCode = 1; });
