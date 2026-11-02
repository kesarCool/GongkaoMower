// Check byte preservation outside Tilemap data and the pre-task shared-resource hashes.
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const backup = path.join(root, 'Library/MapBackups/AutumnTerrain-20261027');
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex').toUpperCase();
// Historical baselines retain their original paths. Compare their content after the
// documented move, allowing only the renamed prefab root and importer path strings.
const paths = require('./map-paths.cjs');
function preservedHash(originalPath) {
  let current = originalPath.replace(/\\/g, '/');
  current = current.replace('Assets/ArtPreview/CartoonMaps_32_db482fa081', paths.sharedCartoon);
  const bamboo = current.match(/Assets\/Res\/Map\/Library\/BambooAcademy\/Prefabs\/TileMap2_(\d)\.prefab(\.meta)?$/);
  if (bamboo) current = path.join(root, paths.bambooMap(Number(bamboo[1]))) + (bamboo[2] || '');
  if (!fs.existsSync(current)) return null;
  let data = fs.readFileSync(current);
  if (bamboo && !bamboo[2]) data = Buffer.from(data.toString('utf8').replace(new RegExp(`^  m_Name: ZlMap${bamboo[1]}$`, 'gm'), `  m_Name: TileMap2_${bamboo[1]}`));
  if (current.endsWith('/SharedCartoon/BuildReport.json')) {
    const text = data.toString('utf8').replace(/^\uFEFF/, '').replaceAll(paths.sharedCartoon, 'Assets/ArtPreview/CartoonMaps_32_db482fa081');
    // The original imported report used UTF-8 BOM; the migrated JSON uses plain UTF-8.
    data = Buffer.from('\uFEFF' + text);
  }
  return crypto.createHash('sha256').update(data).digest('hex').toUpperCase();
}
const json = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const docMap = file => {
  const text = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
  return new Map(text.split(/(?=^--- !u!)/m).slice(1).map(doc => [doc.split('\n')[0], doc]));
};
const report = { maps: [], protectedResources: {} };
for (let stage = 1; stage <= 8; stage++) {
  const name = `TileMap2_${stage}.prefab`, original = docMap(path.join(backup, name)), updated = docMap(path.join(root, 'Assets/Resources/Map', name));
  const changed = [];
  for (const [header, doc] of original) if (updated.get(header) !== doc) changed.push(header);
  report.maps.push({ stage, originalDocuments: original.size, updatedDocuments: updated.size, changedDocuments: changed,
    onlyTilemapChanged: original.size === updated.size && changed.length === 1 && changed.every(header => header.startsWith('--- !u!1839735485 ')),
    prefabMetaUnchanged: hash(path.join(backup, name + '.meta')) === hash(path.join(root, 'Assets/Resources/Map', name + '.meta')) });
}
for (const category of ['shared-art', 'bamboo-map', 'chapter-one']) {
  const original = json(path.join(backup, category + '-hashes.json'));
  const changed = original.filter(record => preservedHash(record.Path) !== record.Hash.toUpperCase()).map(record => record.Path);
  report.protectedResources[category] = { count: original.length, changed, passed: changed.length === 0 };
}
report.passed = report.maps.every(map => map.onlyTilemapChanged && map.prefabMetaUnchanged) && Object.values(report.protectedResources).every(item => item.passed);
fs.writeFileSync(path.join(root, 'ArtOutput/World/AutumnAcademyTerrain/preservation-check.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report));
if (!report.passed) process.exitCode = 1;
