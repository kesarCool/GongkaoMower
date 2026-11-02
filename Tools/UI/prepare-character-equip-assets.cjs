// Small code-drawn assets matching the existing rounded Home UI.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const folder = 'Assets/Res/image/Home/character/';
const assets = [];

async function save(name, width, height, body, border) {
  const file = folder + name + '.png';
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">${body}</svg>`;
  await sharp(Buffer.from(svg)).png({ compressionLevel: 9 }).toFile(path.join(root, file));
  assets.push({ path: file, moveFrom: '', border, maxSize: Math.max(width, height) });
}

(async () => {
  for (const [state, fill, stroke] of [
    ['normal', '#94CFBC', '#40695E'],
    ['pressed', '#72B79F', '#34594F'],
    ['disabled', '#D6DDD4', '#96A394'],
  ]) {
    await save(`character_equip_${state}_9slice`, 256, 128,
      `<rect x="2.5" y="2.5" width="251" height="123" rx="28" fill="${fill}" stroke="${stroke}" stroke-width="5"/>`,
      [39, 39, 39, 39]);
  }
  await save('icon_character_swap', 128, 128, `
    <g stroke="#315B51" stroke-linecap="round" stroke-linejoin="round">
      <path d="M19 60C19 34 38 15 62 15H83" fill="none" stroke-width="8"/>
      <path d="M73 5L86 15L73 25" fill="none" stroke-width="8"/>
      <path d="M109 68C109 94 90 113 66 113H45" fill="none" stroke-width="8"/>
      <path d="M55 103L42 113L55 123" fill="none" stroke-width="8"/>
      <circle cx="64" cy="45" r="15" fill="#FFF6DD" stroke-width="6"/>
      <path d="M40 93V83C40 70 50 64 64 64S88 70 88 83V93Z" fill="#FFF6DD" stroke-width="6"/>
    </g>`, [0, 0, 0, 0]);
  const manifestFile = path.join(root, 'Tools/UI/character-assets.json');
  const manifest = JSON.parse(fs.readFileSync(manifestFile, 'utf8').replace(/^\uFEFF/, ''));
  for (const asset of assets) {
    const index = manifest.assets.findIndex(entry => entry.path === asset.path);
    if (index < 0) manifest.assets.push(asset); else manifest.assets[index] = asset;
  }
  fs.writeFileSync(manifestFile, JSON.stringify(manifest, null, 2) + '\n');
  console.log(JSON.stringify({ assets: assets.length, sourceBytes: assets.reduce((total, asset) => total + fs.statSync(path.join(root, asset.path)).size, 0) }));
})().catch(error => { console.error(error); process.exitCode = 1; });
