const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const source = path.join(root, 'ArtOutput/UI/PlayerGunnerAimV1/After');
const out = path.join(root, 'ArtOutput/VFX/PlayerGunnerAimV1');
const labels = ['E', 'NE', 'N', 'NW', 'W', 'SW', 'S', 'SE'];

(async () => {
  const layers = [];
  for (let i = 0; i < 8; i++) {
    const file = path.join(source, `direction-${i}.png`);
    const m = await sharp(file).metadata();
    const image = await sharp(file).extract({ left: Math.round(m.width / 2 - 120), top: Math.round(m.height / 2 - 100), width: 240, height: 240 }).png().toBuffer();
    const x = i % 4 * 248 + 8, y = Math.floor(i / 4) * 280 + 36;
    layers.push({ input: image, left: x, top: y });
    layers.push({ input: Buffer.from(`<svg width="240" height="28"><text x="120" y="21" fill="white" text-anchor="middle" font-size="18" font-family="Arial">${labels[i]}</text></svg>`), left: x, top: y - 28 });
  }
  await sharp({ create: { width: 1000, height: 568, channels: 3, background: '#303643' } }).composite(layers).png().toFile(path.join(out, 'unity-eight-directions.png'));
  const folder = path.join(source, 'aim-motion');
  const times = JSON.parse(fs.readFileSync(path.join(folder, 'frame-times.json'), 'utf8'));
  if (times.length < 30) throw Error('Motion capture is incomplete');
  let concat = '';
  for (let i = 0; i < times.length; i++) {
    const frame = path.join(folder, 'frames', `frame-${String(i).padStart(4, '0')}.png`).replace(/\\/g, '/');
    if (!fs.existsSync(frame)) throw Error('Frame missing: ' + frame);
    concat += `file '${frame}'\nduration ${i + 1 < times.length ? times[i + 1] - times[i] : .08}\n`;
  }
  const input = path.join(out, 'motion-concat.txt'); fs.writeFileSync(input, concat);
  const video = path.join(out, 'unity-aim-validation.mp4');
  const run = spawnSync('D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe', ['-hide_banner', '-loglevel', 'error', '-y', '-safe', '0', '-f', 'concat', '-i', input, '-vf', 'fps=30,format=yuv420p', '-c:v', 'libx264', '-crf', '22', '-preset', 'fast', '-movflags', '+faststart', video], { encoding: 'utf8' });
  if (run.status !== 0) throw Error(run.stderr);
  const proof = { type: 'Unity Game-scene capture, controlled target', frames: times.length, capturedSeconds: times.at(-1) - times[0], retimed: false, videoBytes: fs.statSync(video).size, note: 'Camera enlarged for inspection. Five whole-body standing aim keys mirrored to eight directions; no walk-cycle animation. Existing projectile spawn origin and skill numbers retained.' };
  fs.writeFileSync(path.join(out, 'proof.json'), JSON.stringify(proof, null, 2));
  process.stdout.write(JSON.stringify(proof));
})().catch(e => { process.stderr.write(e.stack); process.exitCode = 1; });
