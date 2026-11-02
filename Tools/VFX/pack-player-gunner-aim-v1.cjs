const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'ArtOutput/VFX/PlayerGunnerAimV1');
const source = path.join(out, 'Sources/aim-sheet-v2.png');
const assetDir = path.join(root, 'Assets/Res/image/Game/role/GunnerAimV1');
const names = ['right', 'up_right', 'up', 'down_right', 'down'];

(async () => {
  fs.mkdirSync(assetDir, { recursive: true });
  fs.mkdirSync(path.join(out, 'Frames'), { recursive: true });
  const meta = await sharp(source).metadata();
  if (meta.width !== 1536 || meta.height !== 1024 || !meta.hasAlpha) throw Error('Expected a transparent 1536x1024 sheet');
  const atlasLayers = [], previews = [], inventory = [];
  for (let i = 0; i < names.length; i++) {
    const cell = await sharp(source).extract({ left: i % 3 * 512, top: Math.floor(i / 3) * 512, width: 512, height: 512 }).ensureAlpha().raw().toBuffer();
    let footY = -1, headY = 512;
    for (let y = 0; y < 512; y++) for (let x = 0; x < 512; x++) {
      if (cell[(y * 512 + x) * 4 + 3] < 192) continue;
      if (y > 350 && x >= 128 && x <= 220) footY = Math.max(footY, y);
      if (y < 240 && x >= 140 && x <= 315) headY = Math.min(headY, y);
    }
    if (footY < 430 || headY > 100) throw Error('Body anchors not found: ' + names[i]);
    const scale = 106 / (footY - headY);
    const size = Math.round(512 * scale);
    const left = Math.round(64 - 256 * size / 512);
    const top = Math.round(118 - footY * size / 512);
    if (left < 0 || top < 0 || left + size > 128 || top + size > 128) throw Error('Cell would be cropped: ' + names[i]);
    const reduced = await sharp(cell, { raw: { width: 512, height: 512, channels: 4 } }).resize(size, size, { kernel: 'nearest' }).png().toBuffer();
    const sprite = await sharp({ create: { width: 128, height: 128, channels: 4, background: '#00000000' } }).composite([{ input: reduced, left, top }]).png().toBuffer();
    fs.writeFileSync(path.join(out, 'Frames', names[i] + '.png'), sprite);
    atlasLayers.push({ input: sprite, left: i % 4 * 128, top: Math.floor(i / 4) * 128 });
    previews.push({ input: await sharp(sprite).resize(256, 256, { kernel: 'nearest' }).png().toBuffer(), left: i * 264 + 8, top: 40 });
    previews.push({ input: Buffer.from(`<svg width="256" height="28"><text x="128" y="21" fill="#20242d" text-anchor="middle" font-size="17" font-family="Arial">${names[i]}</text></svg>`), left: i * 264 + 8, top: 8 });
    inventory.push({ name: names[i], sourceCell: i, headY, footY, scaledCell: size, left, top, bodyHeightPixels: 106, footBaselinePixels: 118, atlasLeft: i % 4 * 128, atlasTop: Math.floor(i / 4) * 128 });
  }
  const atlasPath = path.join(assetDir, 'gunner-aim-v1.png');
  await sharp({ create: { width: 512, height: 256, channels: 4, background: '#00000000' } }).composite(atlasLayers).png({ palette: true, colours: 128, dither: 0 }).toFile(atlasPath);
  await sharp({ create: { width: 1328, height: 304, channels: 3, background: '#e9dfc9' } }).composite(previews).png().toFile(path.join(out, 'five-poses.png'));
  const report = { source: path.relative(root, source), sourceSha256: crypto.createHash('sha256').update(fs.readFileSync(source)).digest('hex'), atlas: path.relative(root, atlasPath), width: 512, height: 256, spriteCount: 5, atlasBytes: fs.statSync(atlasPath).size, runtimePngFiles: 1, rgba32PixelBytes: 512 * 256 * 4, etc2Rgba8BlockBytesIfUsed: 512 * 256, frames: inventory };
  fs.writeFileSync(path.join(out, 'packing-report.json'), JSON.stringify(report, null, 2));
  process.stdout.write(JSON.stringify(report));
})().catch(e => { process.stderr.write(e.stack); process.exitCode = 1; });
