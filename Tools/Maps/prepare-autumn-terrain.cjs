// Production preparation: crop/resample AI materials into periodic 2x2 terrain macros.
const fs = require('node:fs'), path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const source = path.join(root, 'ArtOutput/World/AutumnAcademyTerrain/source');
const target = path.join(root, require('./map-paths.cjs').autumnAcademy, 'Terrain/Source');
(async () => {
  fs.mkdirSync(target, { recursive: true });
  const assets = [];
  for (const material of ['stone', 'earth']) {
    const input = path.join(source, `autumn_${material}_material.png`);
    const meta = await sharp(input).metadata();
    const side = Math.floor(Math.min(meta.width, meta.height) * .5);
    const { data, info } = await sharp(input).extract({ left: Math.floor((meta.width - side) / 2), top: Math.floor((meta.height - side) / 2), width: side, height: side })
      .resize(128, 128).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    // Match opposite outer pixels; interior quadrant boundaries stay exact source neighbors.
    const size = 128, band = 8, copy = Buffer.from(data);
    for (let y = 0; y < size; y++) for (let k = 0; k < band; k++) for (let channel = 0; channel < 3; channel++) {
      const a = (y * size + k) * 3 + channel, b = (y * size + size - 1 - k) * 3 + channel;
      const weight = (1 - k / band) ** 2, mean = (copy[a] + copy[b]) / 2;
      data[a] = Math.round(copy[a] * (1 - weight) + mean * weight); data[b] = Math.round(copy[b] * (1 - weight) + mean * weight);
    }
    const horizontal = Buffer.from(data);
    for (let x = 0; x < size; x++) for (let k = 0; k < band; k++) for (let channel = 0; channel < 3; channel++) {
      const a = (k * size + x) * 3 + channel, b = ((size - 1 - k) * size + x) * 3 + channel;
      const weight = (1 - k / band) ** 2, mean = (horizontal[a] + horizontal[b]) / 2;
      data[a] = Math.round(horizontal[a] * (1 - weight) + mean * weight); data[b] = Math.round(horizontal[b] * (1 - weight) + mean * weight);
    }
    const output = path.join(target, `autumn_${material}_macro.png`);
    await sharp(data, { raw: info }).png({ compressionLevel: 9 }).toFile(output);
    assets.push({ material, path: path.relative(root, output).replace(/\\/g, '/'), bytes: fs.statSync(output).size });
  }
  fs.writeFileSync(path.join(root, 'ArtOutput/World/AutumnAcademyTerrain/material-preparation.json'), JSON.stringify({ assets, tilePixels: 64, macroPixels: 128, note: 'Editor source macros are not runtime map dependencies; generated ground sprites use 64 pixels per world unit.' }, null, 2));
  console.log(JSON.stringify(assets));
})().catch(error => { console.error(error); process.exitCode = 1; });
