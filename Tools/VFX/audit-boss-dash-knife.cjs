const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const dir = 'ArtOutput/VFX/BossDashKnifeV1/Audit';
const baseline = 'ArtOutput/VFX/BossDashKnifeV1/Baseline/';
const bytes = p => fs.readFileSync(path.join(root, p));
const read = p => bytes(p).toString('utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
const json = p => JSON.parse(read(p));
const sha = b => crypto.createHash('sha256').update(b).digest('hex');
const compact = s => s.replace(/\s+/g, '');
function same(p) { const a = bytes(baseline + p), b = bytes(p); return {path:p, unchanged:a.equals(b), before:sha(a), after:sha(b)}; }
function blocks(s) { return new Map([...s.matchAll(/^--- !u!(\d+) &(-?\d+)\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)].map(m => [m[2], {type:Number(m[1]), text:m[3].trim()}])); }
function method(source, marker) {
  const i = source.indexOf(marker); if (i < 0) return '';
  const start = source.indexOf('{', i); let nesting = 1, end = start + 1;
  for (; end < source.length && nesting; end++) { if(source[end] === '{') nesting++; else if(source[end] === '}') nesting--; }
  return compact(source.slice(start, end));
}
function unityReport(p) { const r=json(p), status=read(p.replace('validation-report.json','status.txt')).trim(); return {path:p, checks:r.checks.length, status, passed:status==='passed' && r.passed===true && r.checks.length>0 && r.checks.every(c=>c.passed===true), failed:r.checks.filter(c=>!c.passed).map(c=>c.name), sha256:sha(bytes(p))}; }
function main() {
  const unchanged = [
    'Assets/Script/Game/Enemies/Boss/BossBrain.cs', 'Assets/Script/Game/Enemies/Boss/BossBullet.cs',
    'Assets/Script/Game/Enemies/Boss/Skills/BossSkillModule.cs',
    'Assets/Resources/Data/table_fb/Monster.bytes','Assets/Resources/Data/table_fb/LevelWave.bytes',
    'Assets/Prefab/Enemy/Boss/bossBullet/HomingBullet.prefab','Assets/Prefab/Enemy/Boss/bossBullet/HomingBullet.prefab.meta',
    'Assets/Script/Game/Enemies/Boss/BossBladeBurstPresentation.cs','Assets/Script/Game/Enemies/Boss/BossBladeProjectilePresentation.cs',
    'Assets/Prefab/Enemy/Boss/bossBullet/BladeBurstBulletV1.prefab',
    'Assets/Res/Prefabs/Effects/Impact/fx_boss_blade_dissolve_v1.prefab',
    'Assets/Res/Textures/VFX/sheet_boss_blade_burst_v1.png','Assets/Res/Textures/VFX/sheet_boss_blade_burst_v1.png.meta',
    'Assets/Res/Materials/VFX/mat_boss_blade_burst_v1.mat','Assets/Res/Materials/VFX/mat_boss_blade_solid_v1.mat','Assets/Res/Materials/VFX/mat_boss_blade_trail_v1.mat'
  ].map(same);
  const textureManifest = json(baseline+'texture-manifest.json');
  const textureDirectory = 'Assets/Res/Textures/VFX/';
  const currentTextureNames = fs.readdirSync(path.join(root,textureDirectory)).filter(n=>fs.statSync(path.join(root,textureDirectory,n)).isFile()).map(n=>textureDirectory+n).sort();
  const textureFiles = textureManifest.map(f=>({path:f.path, unchanged:fs.existsSync(path.join(root,f.path)) && sha(bytes(f.path))===f.sha256}));
  const newTexturePaths = currentTextureNames.filter(p=>!textureManifest.some(f=>f.path===p));
  const brainGuid = /guid: (\w+)/.exec(read('Assets/Script/Game/Enemies/Boss/BossBrain.cs.meta'))[1];
  const bosses = ['boss_common','boss_30001','boss_30002','boss_30003'].map(name=>{
    const p='Assets/Prefab/Enemy/Boss/'+name+'.prefab', before=blocks(read(baseline+p)), after=blocks(read(p));
    const components=[...before].filter(([,b])=>[50,58,61,114].includes(b.type)).map(([id,b])=>{
      let now=after.get(id)?.text;
      if(b.text.includes('guid: '+brainGuid) && now) now=now.replace(/^  - key: KnifeBullet\n    prefab: \{[^\n]+\}\n/gm,'');
      return {id, unchangedExceptKnifeRegistry:now===b.text};
    });
    return {path:p, guidPreserved:same(p+'.meta').unchanged, components};
  });
  const dashBefore=read(baseline+'Assets/Script/Game/Enemies/Boss/Skills/DashModule.cs'), dashAfter=read('Assets/Script/Game/Enemies/Boss/Skills/DashModule.cs');
  const knifeBefore=read(baseline+'Assets/Script/Game/Enemies/Boss/Skills/HomingKnifeModule.cs'), knifeAfter=read('Assets/Script/Game/Enemies/Boss/Skills/HomingKnifeModule.cs');
  const statements=[
    ['dash', 'private float _dashSpeed = 15f;'], ['dash','private float _dashDistance = 8f;'], ['dash','private float _damage = 30f;'],
    ['dash','private float _chargeTime = 0.3f;'], ['dash','private float _recoveryTime = 0.2f;'],
    ['dash','interval = p[0] > 0f ? p[0] : 6f;'], ['dash','_dashSpeed = p[1] > 0f ? p[1] : 15f;'],
    ['dash','_dashDistance = p[2] > 0f ? p[2] : 8f;'], ['dash','_damage = p[3] > 0f ? p[3] : 30f;'],
    ['dash','cooldown = interval * firstDelayMul;'], ['dash','yield return new WaitForSeconds(_chargeTime);'],
    ['dash','float step = Mathf.Min(_dashSpeed * Time.fixedDeltaTime, remaining);'], ['dash','_rb.MovePosition(nextPos);'],
    ['dash','remaining -= step;'], ['dash','yield return new WaitForSeconds(_recoveryTime);'],
    ['knife','interval = p[0] > 0f ? p[0] : 3f;'], ['knife','_lifetime = p[2] > 0f ? p[2] : 5f;'],
    ['knife','_damage = p[3] > 0f ? p[3] : 20f;'], ['knife','_knifeSpeed = p[4] > 0f ? p[4] : 4f;'],
    ['knife','cooldown = interval * firstDelayMul;'], ['knife','SpawnBullet(_knifePrefab, boss.position, Quaternion.identity);'], ['knife','hb.Launch(dir, _knifeSpeed, dmg, _lifetime);']
  ].map(([module,statement])=>{const a=module==='dash'?dashBefore:knifeBefore,b=module==='dash'?dashAfter:knifeAfter;return{module,statement,unchanged:compact(a).includes(compact(statement))&&compact(b).includes(compact(statement))};});
  const dashHitUnchanged=method(dashBefore,'private void CheckDashHit()')===method(dashAfter,'private void CheckDashHit()');
  const configurationFiles=fs.readdirSync(path.join(root,dir)).filter(n=>n.endsWith('-configuration.json'));
  const configurations=configurationFiles.map(n=>{const p=dir+'/'+n,r=json(p);return{path:p,passed:r.passed===true};});
  const before=unityReport('ArtOutput/UI/BossDashKnifeV1/Before/validation-report.json');
  const after=unityReport('ArtOutput/UI/BossDashKnifeV1/After/validation-report.json');
  const rebuild=json(dir+'/rebuild.json');
  const staticPassed=unchanged.every(f=>f.unchanged)&&textureFiles.every(f=>f.unchanged)&&newTexturePaths.length===0&&bosses.every(b=>b.guidPreserved&&b.components.every(c=>c.unchangedExceptKnifeRegistry))&&statements.every(s=>s.unchanged)&&dashHitUnchanged&&configurations.length===2&&configurations.every(c=>c.passed)&&rebuild.passed===true;
  const result={passed:staticPassed&&before.passed&&after.passed,staticEvidence:{passed:staticPassed,unchanged,textureFiles,newTexturePaths,addedTextureBytes:0,bosses,statements,dashHitUnchanged,configurations,rebuildPassed:rebuild.passed},recordedUnityEvidence:{before,after},note:'Read-only integrity audit; gameplay evidence comes from separately recorded Game runs. Visual-only baseline uses pre-change Boss prefab and the original HomingBullet with current cancellation guards. Dash single-result OverlapCircle, current direct-flight knife, and contact behavior remain unchanged; this is not a tracking, collision balance, CloneModule inheritance, WeChat FPS or final upload-size test.'};
  fs.writeFileSync(path.join(root,dir,'integrity.json'),JSON.stringify(result,null,2));
  console.log(JSON.stringify({passed:result.passed,staticPassed,before:before.checks,after:after.checks,addedTextures:newTexturePaths,changed:unchanged.filter(f=>!f.unchanged).map(f=>f.path),changedStatements:statements.filter(s=>!s.unchanged)}));
  if(!result.passed)process.exitCode=1;
}
try {main();} catch(e) {console.error(e); process.exitCode=1;}
