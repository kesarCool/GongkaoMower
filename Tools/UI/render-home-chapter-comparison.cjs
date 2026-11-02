// Comparison of actual Unity captures; output stays outside Assets.
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const out = path.resolve(__dirname, '../../ArtOutput/UI/HomeChapterMapsUnity');
async function main() {
  const background = Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="1080" height="1010"><rect width="1080" height="1010" fill="#EEE7D6"/><g fill="#493627" font-family="Microsoft YaHei, sans-serif" text-anchor="middle"><text x="540" y="40" font-size="26" font-weight="bold">Home 章节地图 · Unity 实际画面</text><text x="285" y="89" font-size="22">第二章 · 秋日书院</text><text x="813" y="89" font-size="22">第三章 · 冬雪水镇</text></g></svg>');
  const layers = [];
  for (const [i, name] of ['chapter-02-autumn-1080x1920.png', 'chapter-03-winter-1080x1920.png'].entries())
    layers.push({ input: await sharp(path.join(out, name)).resize(486, 864).png().toBuffer(), left: 42 + i * 528, top: 112 });
  const target = path.join(out, 'home-seasonal-comparison.png');
  await sharp(background).composite(layers).png({ compressionLevel: 9 }).toFile(target);
  console.log(target);
}
main().catch(error => { console.error(error); process.exitCode = 1; });
