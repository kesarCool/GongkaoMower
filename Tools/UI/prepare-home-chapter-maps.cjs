// Encode approved seasonal maps for runtime. High-resolution designs stay outside Assets.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const designs = path.join(root, 'ArtOutput/UI/HomeChaptersV3');
const destination = path.join(root, 'Assets/Res/image/Home/roadmap');
const out = path.join(root, 'ArtOutput/UI/HomeChapterMapsUnity');
async function main() {
  fs.mkdirSync(destination, { recursive: true }); fs.mkdirSync(out, { recursive: true });
  const layout = JSON.parse(fs.readFileSync(path.join(designs, 'layout.json'), 'utf8'));
  const assets = [];
  for (const chapter of layout.chapters) {
    const filename = chapter.id === 2 ? 'map_autumn_academy_720x1280.jpg' : 'map_winter_town_720x1280.jpg';
    const source = path.join(designs, 'backgrounds', `chapter_0${chapter.id}_${chapter.slug}_1080x1920.png`);
    const target = path.join(destination, filename);
    await sharp(source).resize(720, 1280).jpeg({ quality: 80, mozjpeg: true }).toFile(target);
    assets.push({ chapterId: chapter.id, file: path.relative(root, target).replaceAll('\\', '/'),
      width: 720, height: 1280, sourceBytes: fs.statSync(target).size,
      illustrationCoordinates: [1080, 1920], points: chapter.points });
  }
  fs.writeFileSync(path.join(out, 'source-assets.json'), JSON.stringify(assets, null, 2));
  console.log(JSON.stringify(assets.map(({ points, ...asset }) => asset), null, 2));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
