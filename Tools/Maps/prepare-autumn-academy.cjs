// Mechanically crop/downsample the generated transparent atlas; original stays outside Assets.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const source = path.join(root, 'ArtOutput/World/AutumnAcademy/source/autumn_props_atlas.png');
const folder = path.join(root, require('./map-paths.cjs').autumnAcademy, 'Textures');
const cells = [
  ['maple_tree', 0, 0, 540, 550, 256],
  ['ginkgo_tree', 545, 0, 520, 550, 256],
  ['autumn_shrub', 1070, 0, 466, 550, 128],
  ['academy_gate', 0, 560, 605, 464, 256],
  ['study_table', 610, 560, 420, 464, 128],
  ['autumn_leaves', 1040, 560, 496, 464, 128],
];
(async () => {
  fs.mkdirSync(folder, { recursive: true });
  const assets = [];
  for (const [name, left, top, width, height, size] of cells) {
    const { data, info } = await sharp(source).extract({ left, top, width, height }).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    let x0 = width, y0 = height, x1 = -1, y1 = -1;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
      if (data[(y * width + x) * info.channels + 3] > 20) {
        x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y);
      }
    }
    if (x1 < x0) throw new Error('Empty sprite: ' + name);
    const trim = { left: Math.max(0, x0 - 3), top: Math.max(0, y0 - 3), width: Math.min(width - 1, x1 + 3) - Math.max(0, x0 - 3) + 1, height: Math.min(height - 1, y1 + 3) - Math.max(0, y0 - 3) + 1 };
    const target = path.join(folder, name + '.png');
    await sharp(data, { raw: info }).extract(trim).resize(size - 8, size - 8, { fit: 'contain', background: '#00000000' })
      .extend({ top: 4, bottom: 4, left: 4, right: 4, background: '#00000000' }).png({ palette: true, colours: 256, quality: 95, dither: 0 }).toFile(target);
    assets.push({ name, path: path.relative(root, target).replace(/\\/g, '/'), size, bytes: fs.statSync(target).size, sourceRect: { left, top, width, height }, trim });
  }
  const report = { source: path.relative(root, source).replace(/\\/g, '/'), assets, totalSourceBytes: assets.reduce((n, a) => n + a.bytes, 0) };
  fs.writeFileSync(path.join(root, 'ArtOutput/World/AutumnAcademy/asset-preparation.json'), JSON.stringify(report, null, 2) + '\n');
  const sheet = await sharp({ create: { width: 768, height: 512, channels: 4, background: '#EEE8D6' } }).composite(await Promise.all(assets.map(async (a, i) => ({
    input: await sharp(path.join(root, a.path)).resize(240, 240, { fit: 'contain', background: '#00000000' }).toBuffer(), left: i % 3 * 256 + 8, top: Math.floor(i / 3) * 256 + 8,
  })))).png().toBuffer();
  fs.writeFileSync(path.join(root, 'ArtOutput/World/AutumnAcademy/sprite-proof.png'), sheet);
  console.log(JSON.stringify(report));
})().catch(error => { console.error(error); process.exitCode = 1; });
