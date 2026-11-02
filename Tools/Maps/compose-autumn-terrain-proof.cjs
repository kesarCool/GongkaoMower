const fs = require('node:fs'), path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..'), folder = path.join(root, 'ArtOutput/World/AutumnAcademyTerrain');
(async () => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1124" height="1048"><rect width="100%" height="100%" fill="#EEE9DC"/><g font-family="Microsoft YaHei,sans-serif" fill="#493C32"><text x="16" y="32" font-size="25" font-weight="bold">第二章 · 秋日书院 / 地表更新对比</text><text x="16" y="66" font-size="19">原：青砖＋草地</text><text x="570" y="66" font-size="19">新：暖石板＋红褐秋土</text></g></svg>`;
  await sharp(Buffer.from(svg)).composite([
    { input: fs.readFileSync(path.join(root, 'ArtOutput/World/AutumnAcademy/TileMap2_3-phone.png')), left: 16, top: 80 },
    { input: fs.readFileSync(path.join(folder, 'TileMap2_3-phone.png')), left: 570, top: 80 },
  ]).png().toFile(path.join(folder, 'before-after.png'));
  const textureFolder = path.join(root, require('./map-paths.cjs').autumnAcademy, 'Terrain/Textures');
  const files = fs.readdirSync(textureFolder).filter(file => file.endsWith('.png')).sort();
  const columns = 8, cell = 112, rows = Math.ceil(files.length / columns), width = columns * cell, height = 54 + rows * cell;
  let labels = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="100%" height="100%" fill="#EEE9DC"/><g font-family="Microsoft YaHei,sans-serif" fill="#493C32"><text x="12" y="32" font-size="22" font-weight="bold">秋日独立瓦片 / 64px / ${files.length} 张</text>`;
  const layers = [];
  for (let i = 0; i < files.length; i++) {
    const left = i % columns * cell + 8, top = 54 + Math.floor(i / columns) * cell;
    layers.push({ input: await sharp(path.join(textureFolder, files[i])).resize(96, 96).toBuffer(), left, top });
    labels += `<text x="${left}" y="${top + 108}" font-size="8">${files[i].replace('autumn_stone_on_earth_', '').replace('autumn_', '').replace('.png', '')}</text>`;
  }
  labels += '</g></svg>';
  await sharp(Buffer.from(labels)).composite(layers).png().toFile(path.join(folder, 'tile-palette.png'));
  console.log('Created before/after proof and terrain palette sheet.');
})().catch(error => { console.error(error); process.exitCode = 1; });
