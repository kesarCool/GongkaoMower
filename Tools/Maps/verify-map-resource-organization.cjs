// Read-only preservation audit against the one-time resource migration receipt.
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const layout = require('./map-paths.cjs');
const output = path.join(root, 'ArtOutput/World/MapResourceOrganization');
const receipt = JSON.parse(fs.readFileSync(path.join(output, 'move-receipt.json'), 'utf8'));
const sha = data => crypto.createHash('sha256').update(data).digest('hex').toUpperCase();
const issues = [];
let textureBytes = 0, textures = 0, guids = 0;
for (const record of receipt.files) {
  const before = path.join(root, receipt.backup, record.from), after = path.join(root, record.to);
  if (!fs.existsSync(after)) { issues.push(`Missing: ${record.to}`); continue; }
  const original = fs.readFileSync(before);
  let current = fs.readFileSync(after);
  if (record.to === `${layout.sharedCartoon}/BuildReport.json`) {
    const normalize = text => JSON.parse(text.replace(/^\uFEFF/, '').replaceAll(layout.sharedCartoon, 'Assets/ArtPreview/CartoonMaps_32_db482fa081'));
    if (JSON.stringify(normalize(current.toString('utf8'))) !== JSON.stringify(normalize(original.toString('utf8')))) issues.push(`Unexpected report edit: ${record.to}`);
  } else {
    if (/\/Library\/BambooAcademy\/.*\.prefab$/.test(record.from)) {
      const oldName = path.basename(record.from, '.prefab'), newName = path.basename(record.to, '.prefab');
      current = Buffer.from(current.toString('utf8').replace(new RegExp(`^  m_Name: ${newName}(?=\r?$)`, 'gm'), `  m_Name: ${oldName}`));
    }
    if (sha(current) !== sha(original)) issues.push(`Unexpected content edit: ${record.to}`);
  }
  if (record.guid) {
    const meta = fs.readFileSync(after + '.meta', 'utf8');
    if (!meta.includes(`guid: ${record.guid}`)) issues.push(`GUID changed: ${record.to}`);
    guids++;
  }
  if (record.to.endsWith('.png')) { textures++; textureBytes += fs.statSync(after).size; }
}
for (const move of receipt.moves) if (fs.existsSync(path.join(root, move.from))) issues.push(`Old location remains: ${move.from}`);
const source = fs.readFileSync(path.join(root, 'Assets/Editor/MapResourcePaths.cs'), 'utf8');
if (!source.includes(`Styles = "${layout.styles}"`)) issues.push('C# and Node style roots differ');
const report = { passed: issues.length === 0, checkedFiles: receipt.files.length, checkedGuids: guids, textures, textureSourceBytes: textureBytes, issues };
fs.writeFileSync(path.join(output, 'preservation-check.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report));
if (!report.passed) process.exitCode = 1;
