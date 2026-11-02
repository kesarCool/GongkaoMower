// Encode only the actual Unity screenshots, preserving recorded frame timing.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const output = path.join(root, 'ArtOutput/UI/BossArenaEntry/Transition');
const motion = path.join(output, 'portal-motion');
const ffmpeg = 'D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe';
const report = JSON.parse(fs.readFileSync(path.join(output, 'validation-report.json'), 'utf8'));
if (!report.passed || !report.checks.every(c => c.passed)) throw Error('Unity validation did not pass.');
const times = JSON.parse(fs.readFileSync(path.join(motion, 'frame-times.json'), 'utf8'));
const metadata = JSON.parse(fs.readFileSync(path.join(motion, 'motion.json'), 'utf8'));
if (metadata.retimed || metadata.timeScale !== 1 || times.length < 16) throw Error('Invalid capture.');
let concat = '';
for (let i = 0; i < times.length; i++) {
    if (!Number.isFinite(times[i]) || (i > 0 && times[i] <= times[i - 1])) throw Error('Non-monotonic timestamps.');
    const frame = path.join(motion, 'frames', `frame-${String(i).padStart(4, '0')}.png`);
    if (!fs.existsSync(frame)) throw Error('Missing capture frame.');
    concat += `file '${frame.replace(/\\/g, '/')}'\nduration ${i + 1 < times.length ? times[i + 1] - times[i] : 1 / 30}\n`;
}
concat += `file '${path.join(motion, 'frames', `frame-${String(times.length - 1).padStart(4, '0')}.png`).replace(/\\/g, '/')}'\n`;
const list = path.join(motion, 'capture.concat');
fs.writeFileSync(list, concat);
const video = path.join(output, 'boss-entry-transition.mp4');
const encoded = spawnSync(ffmpeg, ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', list,
    '-vf', 'fps=30', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', video], { encoding: 'utf8' });
if (encoded.status !== 0) throw Error(encoded.stderr || encoded.error);
console.log(JSON.stringify({ passed: true, checks: report.checks.length, frames: times.length,
    capturedSeconds: times.at(-1) - times[0], retimed: false, video }));
