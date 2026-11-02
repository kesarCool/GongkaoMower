const fs = require('node:fs'), path = require('node:path'), { spawnSync } = require('node:child_process');
const starsOnly = process.argv.includes('--stars');
const folder = path.resolve(__dirname, '../../ArtOutput/UI/SkillSelectionV5/' + (starsOnly ? 'LayeredStars' : 'After'));
const report = JSON.parse(fs.readFileSync(path.join(folder, 'validation-report.json'), 'utf8'));
if (!report.passed || !report.checks.every(c => c.passed)) throw Error('Unity V5 validation failed.');
const capture = path.join(folder, starsOnly ? 'star-motion' : 'selection-motion');
const times = JSON.parse(fs.readFileSync(path.join(capture, 'frame-times.json'), 'utf8'));
const meta = JSON.parse(fs.readFileSync(path.join(capture, 'motion.json'), 'utf8'));
if (meta.retimed || times.length < 10) throw Error('Invalid capture timestamps.');
let concat = '';
for (let i = 0; i < times.length; i++) {
    const frame = path.join(capture, 'frames', `frame-${String(i).padStart(4, '0')}.png`);
    if (!fs.existsSync(frame) || !Number.isFinite(times[i]) || (i > 0 && times[i] <= times[i - 1])) throw Error('Missing/invalid frame.');
    concat += `file '${frame.replace(/\\/g, '/')}'\nduration ${i + 1 < times.length ? times[i + 1] - times[i] : 1 / 30}\n`;
}
concat += `file '${path.join(capture, 'frames', `frame-${String(times.length - 1).padStart(4, '0')}.png`).replace(/\\/g, '/')}'\n`;
const list = path.join(capture, 'capture.concat'); fs.writeFileSync(list, concat);
const video = path.join(folder, starsOnly ? 'star-layer-blink.mp4' : 'skill-selection-press.mp4');
const result = spawnSync('D:/Tools/ffmpeg/ffmpeg-8.1.1-full_build/bin/ffmpeg.exe',
    ['-hide_banner', '-loglevel', 'error', '-y', '-f', 'concat', '-safe', '0', '-i', list,
        '-vf', 'fps=30', '-c:v', 'libx264', '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', video], { encoding: 'utf8' });
if (result.status !== 0) throw Error(result.stderr || result.error);
console.log(JSON.stringify({ passed: true, checks: report.checks.length, frames: times.length, retimed: false, video }));
