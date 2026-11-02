'use strict';
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const cp = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const source = path.resolve(root, process.argv[2] || 'Build/minigame/minigame');
const output = path.resolve(root, process.argv[3] || 'Build/ios-performance-20261028');
const within = (base, value) => value === base || value.startsWith(base + path.sep);
assert(within(root, source) && within(root, output), 'Both paths must stay in this workspace');
assert(!within(source, output) && !within(output, source), 'Source and output must be independent');
const folders = { A: path.join(output, 'A-native-dpr'), B: path.join(output, 'B-dpr2') };
for (const folder of Object.values(folders)) assert(!fs.existsSync(folder), 'Variant already exists: ' + folder);
const original = fs.readFileSync(path.join(source, 'check-version.js'), 'utf8');
const dprBlock = /if\s*\(isIOS\s*&&\s*typeof\s*2\s*===\s*'number'\s*&&\s*2\s*>\s*0\)\s*\{\s*(?:\/\/[^\n]*\n\s*)*window\.devicePixelRatio\s*=\s*2;\s*\}/g;
assert.equal([...original.matchAll(dprBlock)].length, 1, 'Expected one SDK-exported DPR 2 block');
const native = original.replace(dprBlock, block => block.replace(/typeof\s*2/, 'typeof 0').replace(/&&\s*2\s*>/, '&& 0 >').replace(/=\s*2;/, '= 0;'));
const entry = fs.readFileSync(path.join(source, 'game.js'), 'utf8');
assert.equal((entry.match(/variant: 'B-dpr2'/g) || []).length, 1, 'Expected one source variant label');
fs.mkdirSync(output, { recursive: true });
for (const folder of Object.values(folders)) fs.cpSync(source, folder, { recursive: true, force: false, errorOnExist: true });
fs.writeFileSync(path.join(folders.A, 'check-version.js'), native);
fs.writeFileSync(path.join(folders.A, 'game.js'), entry.replace("variant: 'B-dpr2'", "variant: 'A-native-dpr'"));

const extractDpr = text => {
  const match = text.match(/if\s*\(isIOS\s*&&\s*typeof\s*[\d.]+\s*===\s*'number'\s*&&\s*[\d.]+\s*>\s*0\)\s*\{\s*(?:\/\/[^\n]*\n\s*)*window\.devicePixelRatio\s*=\s*[\d.]+;\s*\}/);
  assert(match, 'DPR block is missing'); return match[0];
};
for (const [label, folder] of Object.entries(folders)) {
  const text = fs.readFileSync(path.join(folder, 'check-version.js'), 'utf8');
  const block = extractDpr(text);
  for (const ratio of [2, 3, 3.5]) for (const isIOS of [false, true]) {
    const context = { isIOS, window: { devicePixelRatio: ratio } };
    vm.runInNewContext(block, context);
    assert.equal(context.window.devicePixelRatio, label === 'B' && isIOS ? 2 : ratio);
  }
  const temporary = path.join(root, 'Library/IosPerformanceVariants', path.basename(output), `game-${label}.mjs`);
  fs.mkdirSync(path.dirname(temporary), { recursive: true });
  fs.copyFileSync(path.join(folder, 'game.js'), temporary);
  cp.execFileSync(process.execPath, ['--check', temporary], { stdio: 'pipe' });
}
const walk = (folder, prefix = '') => fs.readdirSync(folder, { withFileTypes: true }).flatMap(item =>
  item.isDirectory() ? walk(path.join(folder, item.name), path.join(prefix, item.name)) : [path.join(prefix, item.name)]);
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const files = walk(folders.A).sort();
assert.deepEqual(files, walk(folders.B).sort());
const changes = files.filter(file => hash(path.join(folders.A, file)) !== hash(path.join(folders.B, file)));
assert.deepEqual(changes.sort(), ['check-version.js', 'game.js']);
const binary = files.filter(file => /\.(?:br|wasm)$/.test(file)).map(file => ({ file, sha256: hash(path.join(folders.A, file)) }));
const revision = entry.match(/revision: '([^']+)'/);
assert(revision, 'Expected one performance revision');
const report = { status: 'PASS', revision: revision[1],
  targetUnityFps: 30, A: folders.A, B: folders.B,
  variants: { A: 'Native iOS DPR', B: 'iOS DPR 2' },
  differingFiles: changes, comparedFiles: files.length, identicalBinaries: binary,
  checks: ['same code and resources', 'only DPR and diagnostic label differ', '12 platform/DPR cases', 'entry JavaScript syntax'],
  devicePerformance: 'pending; this validates package isolation, not iPhone performance' };
fs.writeFileSync(path.join(output, 'comparison-manifest.json'), JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
