const fs = require('node:fs');
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const folder = process.argv[2] ? path.resolve(process.argv[2]) : path.resolve(__dirname, '../../ArtOutput/World/AutumnAcademy');
const names = ['枫庭迎门', '银杏斜阶', '秋影回庭', '丹枫寻章', '落叶书径', '双庭听秋', '书山秋阶', '文昌秋试'];
async function render(phone) {
  const w = phone ? 250 : 420, h = phone ? 444 : 420, gap = 18, row = h + 68;
  const width = 4 * (w + gap) + gap, height = 96 + row * 2;
  let body = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="100%" height="100%" fill="#EEE9DC"/><g font-family="Microsoft YaHei,sans-serif" fill="#493C32"><text x="18" y="36" font-size="27" font-weight="bold">第二章 · 秋日书院 / 八关瓦片地图</text><text x="18" y="67" font-size="17">${phone ? '实际 Prefab 手机视野 / 9:16 / 红枫、银杏与落叶，中央留白用于战斗' : '实际 Unity Prefab 渲染 / 30×30 格 / 原竹林书院八图完整保留'}</text>`;
  const layers = [];
  for (let i = 0; i < 8; i++) {
    const left = gap + i % 4 * (w + gap), top = 96 + Math.floor(i / 4) * row;
    body += `<rect x="${left}" y="${top}" width="${w}" height="${h + 54}" fill="#FFF9ED"/><text x="${left + 9}" y="${top + h + 25}" font-size="19" font-weight="bold">2-${i + 1} · ${names[i]}</text><text x="${left + 9}" y="${top + h + 46}" font-size="13">TileMap2_${i + 1}.prefab</text>`;
    layers.push({ input: await sharp(path.join(folder, `TileMap2_${i + 1}${phone ? '-phone' : ''}.png`)).resize(w, h).toBuffer(), left, top });
  }
  body += '</g></svg>';
  await sharp(Buffer.from(body)).composite(layers).png().toFile(path.join(folder, phone ? 'phone-overview.png' : 'overview.png'));
}
Promise.all([render(false), render(true)]).then(() => console.log('Created autumn map contact sheets.')).catch(error => { console.error(error); process.exitCode = 1; });
