// Mechanical contact sheets of actual Unity camera renders; no generated art.
const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const bamboo = process.argv[2] === 'bamboo';
const chapter = bamboo ? 2 : Number(process.argv[2] || 1);
if (![1, 2].includes(chapter)) throw new Error('Supported chapters: 1, 2');
const folder = path.resolve(__dirname, bamboo ? '../../ArtOutput/World/BambooAcademyLibrary' : chapter === 1 ? '../../ArtOutput/World/ChapterOneMaps' : '../../ArtOutput/World/ChapterTwoMaps');
const names = chapter === 1 ? ['田园食集', '十字集市', '双院连廊', '果园环路', '回折长街', '麦田双道', '双环食坊', '宴集广场']
  : ['竹径迎门', '斜阶问学', '竹影回庭', '分径寻章', '九曲书径', '双庭对语', '书山长阶', '文昌论道'];
const title = chapter === 1 ? '第一章 · 美食篇 / 风格 1' : '竹林书院 · 可复用地图库';
const esc = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;');
async function sheet(phone) {
  const imageWidth = phone ? 250 : 440;
  const imageHeight = phone ? 444 : 440;
  const gap = 18, labelHeight = 60, top = 94;
  const cellWidth = imageWidth + gap;
  const cellHeight = imageHeight + labelHeight + gap;
  const width = cellWidth * 4 + gap, height = top + cellHeight * 2 + 18;
  let svg = `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg"><rect width="100%" height="100%" fill="#f3efdf"/><g font-family="Microsoft YaHei, sans-serif" fill="#4d3a2a"><text x="20" y="35" font-weight="700" font-size="25">${title}</text><text x="20" y="64" font-size="16">${phone ? '手机视野：正交相机 size 10，9:16，中心 (15,15)' : '实际 Prefab 渲染：30×30 格 / 共用现有瓦片与摆件'}</text>`;
  const layers = [];
  for (let i = 0; i < 8; i++) {
    const x = gap + (i % 4) * cellWidth, y = top + Math.floor(i / 4) * cellHeight;
    const renamed = bamboo ? `ZlMap${i + 1}.prefab` : `TileMap${chapter}_${i + 1}.prefab`;
    const label = fs.existsSync(path.resolve(__dirname, '../../Assets/Resources/Map', renamed)) ? renamed : `TileMap${chapter}-${i + 1}.prefab`;
    svg += `<rect x="${x}" y="${y - 5}" width="${imageWidth}" height="${imageHeight + labelHeight + 5}" fill="#fffaf0"/><text x="${x + 10}" y="${y + imageHeight + 24}" font-size="18" font-weight="700">${chapter}-${i + 1} · ${esc(names[i])}</text><text x="${x + 10}" y="${y + imageHeight + 47}" font-size="14" fill="#7a7160">${label}</text>`;
    const file = path.join(folder, `${bamboo ? 'ZlMap' : 'TileMap' + chapter + (chapter === 1 ? '-' : '_')}${i + 1}${phone ? '-phone' : ''}.png`);
    layers.push({ input: await sharp(file).resize(imageWidth, imageHeight).toBuffer(), left: x, top: y });
  }
  svg += '</g></svg>';
  await sharp(Buffer.from(svg)).composite(layers).png().toFile(path.join(folder, phone ? 'phone-overview.png' : 'overview.png'));
}
Promise.all([sheet(false), sheet(true)]).then(() => console.log('Created map and phone contact sheets.')).catch(e => { console.error(e); process.exitCode = 1; });
