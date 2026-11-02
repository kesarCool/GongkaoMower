const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
let sharp;
try { sharp = require('sharp'); }
catch { sharp = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp'); }

// Read-only asset audit. This does not launch Unity, execute gameplay checks or modify imported assets.
const root = path.resolve(__dirname, '../..');
const output = 'ArtOutput/VFX/BossBladeBurstV1/Audit/integrity.json';
const baseline = 'ArtOutput/VFX/BossBladeBurstV1/Baseline/';
const bytes = relative => fs.readFileSync(path.join(root, relative));
const read = relative => bytes(relative).toString('utf8').replace(/\r\n/g, '\n');
const json = relative => JSON.parse(read(relative));
const sha = buffer => crypto.createHash('sha256').update(buffer).digest('hex');
const normalized = value => value.replace(/\s+/g, '');
function sameFile(relative) {
  const old = bytes(baseline + relative), current = bytes(relative);
  return { path: relative, unchanged: old.equals(current), baselineSha256: sha(old), currentSha256: sha(current), bytes: current.length };
}
function blocks(text) {
  return new Map([...text.matchAll(/^--- !u!(\d+) &(-?\d+)\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)]
    .map(match => [match[2], { unityType: Number(match[1]), text: match[3].trim() }]));
}
async function imageAudit(relative) {
  const input = bytes(relative);
  const { data, info } = await sharp(input).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  let transparent = 0, partial = 0, opaque = 0;
  for (let index = 3; index < data.length; index += 4) {
    if (data[index] === 0) transparent++;
    else if (data[index] === 255) opaque++;
    else partial++;
  }
  return { path: relative, sha256: sha(input), bytes: input.length, width: info.width, height: info.height,
    transparentPixels: transparent, partialAlphaPixels: partial, opaquePixels: opaque,
    hasRealTransparency: transparent > 0 && partial + opaque > 0 };
}
function recordedReport(relative) {
  const report = json(relative);
  const status = read(relative.replace(/validation-report\.json$/, 'status.txt')).trim();
  return { path: relative, sha256: sha(bytes(relative)), status, passed: status === 'passed' && report.passed === true &&
    report.checks.length > 0 && report.checks.every(check => check.passed === true), checks: report.checks.length,
    includesReleasePhaseReuse: report.checks.some(check => check.name === 'Release-phase interruption and same-instance reuse clear busy and old geometry' && check.passed === true),
    failedChecks: report.checks.filter(check => !check.passed).map(check => check.name) };
}
async function main() {
  const recipe = json('Tools/VFX/Library/Boss/boss_blade_burst_v1.json');
  const sources = await Promise.all([recipe.bladeSource, recipe.hitSource].map(imageAudit));
  const atlas = await imageAudit(recipe.texturePath);
  const { data, info } = await sharp(bytes(recipe.texturePath)).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const cells = [];
  for (let frame = 0; frame < 8; frame++) {
    let transparentBorder = true, visiblePixels = 0;
    // PNG rows 0..63 are Unity's y=192..255, i.e. the eight authored dissolve cells.
    for (let y = 0; y < 64; y++) for (let x = 0; x < 64; x++) {
      const alpha = data[(y * info.width + frame * 64 + x) * 4 + 3];
      if (x < 4 || x >= 60 || y < 4 || y >= 60) transparentBorder = transparentBorder && alpha === 0;
      else if (alpha > 0) visiblePixels++;
    }
    cells.push({ frame, gutterPixels: 4, transparentBorder, visiblePixels });
  }
  const unchanged = [
    'Assets/Script/Game/Enemies/Boss/BossBrain.cs',
    'Assets/Script/Game/Enemies/Boss/BossBullet.cs',
    'Assets/Resources/Data/table_fb/Monster.bytes',
    'Assets/Resources/Data/table_fb/LevelWave.bytes',
    'Assets/Prefab/Enemy/Boss/bossBullet/HomingBullet.prefab',
    'Assets/Prefab/Enemy/Boss/bossBullet/StraightBullet.prefab',
    'Assets/Prefab/Enemy/Boss/bossBullet/StraightBullet.prefab.meta'
  ].map(sameFile);
  const brainGuid = /guid: (\w+)/.exec(read('Assets/Script/Game/Enemies/Boss/BossBrain.cs.meta'))[1];
  const bossPrefabs = ['boss_common', 'boss_30001', 'boss_30002', 'boss_30003'].map(name => {
    const relative = 'Assets/Prefab/Enemy/Boss/' + name + '.prefab';
    const old = blocks(read(baseline + relative)), current = blocks(read(relative));
    const gameplay = [...old].filter(([, block]) => [50, 58, 61, 114].includes(block.unityType)).map(([id, block]) => {
      const now = current.get(id); let currentText = now && now.text;
      const brain = block.text.includes('guid: ' + brainGuid);
      if (brain && currentText) currentText = currentText.replace(/^  - key: BladeBurstBullet\n    prefab: \{[^\n]+\}\n/gm, '');
      return { id, component: block.text.split(':')[0], registryAdditionAllowed: brain,
        unchangedExceptDedicatedRegistryEntry: currentText === block.text };
    });
    const guid = sameFile(relative + '.meta');
    return { path: relative, guidPreserved: guid.unchanged, guidSha256: guid.currentSha256, gameplay };
  });
  const originalProjectile = blocks(read('Assets/Prefab/Enemy/Boss/bossBullet/StraightBullet.prefab'));
  const dedicatedProjectile = blocks(read(recipe.projectilePath));
  const dedicatedGameplay = [...originalProjectile].filter(([, block]) => [50, 58, 114].includes(block.unityType))
    .map(([id, block]) => ({ id, component: block.text.split(':')[0], unchanged: dedicatedProjectile.get(id)?.text === block.text }));
  const originalModule = read(baseline + 'Assets/Script/Game/Enemies/Boss/Skills/BladeBurstModule.cs');
  const currentModule = read('Assets/Script/Game/Enemies/Boss/Skills/BladeBurstModule.cs');
  const originalCombatStatements = [
    'private int _bladeCount = 8;', 'private float _bladeSpeed = 7f;', 'private float _bladeRange = 6f;',
    'private float _damage = 25f;', 'private float _chargeTime = 0.5f;',
    'firstDelayMul = 0.4f;', 'cooldown = interval * firstDelayMul;',
    'interval = p[0] > 0f ? p[0] : 8f;', '_bladeCount = Mathf.Max(2, (int)(p[1] > 0f ? p[1] : 8f));',
    '_damage = p[2] > 0f ? p[2] : 25f;', '_bladeSpeed = p[3] > 0f ? p[3] : 7f;',
    'yield return new WaitForSeconds(_chargeTime);', 'float dmg = ResolveDamage(_damage, 25f);',
    'float lifetm = _bladeRange / Mathf.Max(0.1f, _bladeSpeed);', 'float angleStep = 360f / _bladeCount;',
    'SpawnBlade(_rb.position + dir * 0.6f, dir, dmg, lifetm);', 'yield return new WaitForSeconds(0.15f);'
  ].map(statement => ({ statement, baselineContains: normalized(originalModule).includes(normalized(statement)),
    currentContains: normalized(currentModule).includes(normalized(statement)) }));
  const configuration = json('ArtOutput/VFX/BossBladeBurstV1/Audit/configuration.json');
  const staticPassed = sources.every(source => source.hasRealTransparency) && atlas.hasRealTransparency &&
    atlas.width === 512 && atlas.height === 256 && atlas.bytes <= recipe.pngBudgetKiB * 1024 &&
    cells.every(cell => cell.transparentBorder && cell.visiblePixels > 0) && unchanged.every(file => file.unchanged) &&
    bossPrefabs.every(boss => boss.guidPreserved && boss.gameplay.every(component => component.unchangedExceptDedicatedRegistryEntry)) &&
    dedicatedGameplay.every(component => component.unchanged) && originalCombatStatements.every(statement => statement.baselineContains && statement.currentContains) &&
    configuration.passed === true && Object.values(configuration.checks).every(value => value === true);
  const before = recordedReport('ArtOutput/UI/BossBladeBurstV1/Before/validation-report.json');
  const after = recordedReport(recipe.validationReport || 'ArtOutput/UI/BossBladeBurstV1/After/validation-report.json');
  const report = { passed: staticPassed && before.passed && after.passed && after.includesReleasePhaseReuse, staticEvidence: { passed: staticPassed,
    sources, atlas, dissolveCells: cells, unchangedFiles: unchanged, bossPrefabs, dedicatedGameplay,
    originalCombatStatements, configuration: { path: 'ArtOutput/VFX/BossBladeBurstV1/Audit/configuration.json',
      passed: configuration.passed, checks: Object.keys(configuration.checks).length } },
    existingUnityEvidence: { readOnly: true, requiredLatestCase: 'Release-phase interruption and same-instance reuse clear busy and old geometry', before, after },
    review: { blockingIssuesFound: false, inspected: [
      'BladeBurstModule.cs', 'BossBladeBurstPresentation.cs', 'BossBladeProjectilePresentation.cs', 'StraightBullet.cs'
    ], observations: [
      'New registry key is isolated from the existing HomingBullet and StraightBullet prefab references.',
      'The charge token, active-object and enemy-health guards prevent delayed release after cancellation.',
      'OnDisable clears IsBusy during both charge and release, and runtime-created meshes are destroyed with their owner.',
      'Optional dissolve and orientation hooks in StraightBullet delegate real damage and expiry to the unchanged BossBullet.',
      'SpawnLimiter affects cosmetic dissolve only; warning meshes and damaging enemy bullets remain independent.',
      'No combat RNG, runtime texture generation or per-frame Instantiate calls appear in the new presenters.'
    ] },
    note: 'Static file/pixel evidence and previously recorded Unity runs are separate. This script performs no gameplay execution. It does not measure WeChat device FPS, upload size or final build memory.' };
  fs.mkdirSync(path.dirname(path.join(root, output)), { recursive: true });
  fs.writeFileSync(path.join(root, output), JSON.stringify(report, null, 2));
  console.log(JSON.stringify({ passed: report.passed, staticPassed, pngBytes: atlas.bytes,
    baselineChecks: before.checks, afterChecks: after.checks, includesReleasePhaseReuse: after.includesReleasePhaseReuse,
    output, failedStaticFiles: unchanged.filter(file => !file.unchanged).map(file => file.path),
    failedBossComponents: bossPrefabs.flatMap(boss => boss.gameplay.filter(component => !component.unchangedExceptDedicatedRegistryEntry).map(component => boss.path + ':' + component.id)) }));
  if (!report.passed) process.exitCode = 1;
}
main().catch(error => { console.error(error); process.exitCode = 1; });
