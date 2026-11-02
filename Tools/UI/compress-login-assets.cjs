// Deterministic encoding only. The approved high-resolution art remains in ArtOutput.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root = path.resolve(__dirname, '../..');
const source = path.join(root, 'ArtOutput/UI/LoginOriginalPosterV1');
const destination = path.join(root, 'Assets/Res/image/Home/login');
async function main() {
  await sharp(path.join(source, 'backgrounds/login_background_1080x1920.png'))
    .resize(720, 1280).jpeg({ quality: 80, mozjpeg: true })
    .toFile(path.join(destination, 'login_background.jpg'));
  // DXT requires both dimensions to be divisible by four. Keep the title pixels intact.
  await sharp(path.join(source, 'elements/game_title_1040x250.png'))
    .extend({ top: 1, bottom: 1, left: 0, right: 0, background: '#00000000' })
    .png({ compressionLevel: 9 })
    .toFile(path.join(destination, 'game_title.png'));
  const files = [];
  for (const name of ['login_background.jpg', 'game_title.png']) {
    const file = path.join(destination, name), metadata = await sharp(file).metadata();
    files.push({ name, width: metadata.width, height: metadata.height, bytes: fs.statSync(file).size });
  }
  console.log(JSON.stringify(files, null, 2));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
