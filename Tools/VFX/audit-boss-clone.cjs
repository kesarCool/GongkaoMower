const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),dir='ArtOutput/VFX/BossCloneV1/Audit/',baseline='ArtOutput/VFX/BossCloneV1/Baseline/';
const bytes=p=>fs.readFileSync(path.join(root,p)),read=p=>bytes(p).toString('utf8').replace(/^\uFEFF/,'').replace(/\r\n/g,'\n'),json=p=>JSON.parse(read(p));
const sha=b=>crypto.createHash('sha256').update(b).digest('hex'),compact=s=>s.replace(/\s+/g,'');
function same(p){const a=bytes(baseline+p),b=bytes(p);return{path:p,unchanged:a.equals(b),before:sha(a),after:sha(b)};}
function blocks(s){return new Map([...s.matchAll(/^--- !u!(\d+) &(-?\d+)\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)].map(m=>[m[2],{type:Number(m[1]),text:m[3].trim()}]));}
function report(p){const r=json(p),status=read(p.replace('validation-report.json','status.txt')).trim();return{path:p,passed:r.passed===true&&status==='passed'&&r.checks.length>0&&r.checks.every(c=>c.passed),checks:r.checks.length,sha256:sha(bytes(p)),failed:r.checks.filter(c=>!c.passed).map(c=>c.name)};}
function main(){
 const unchanged=[
 'Assets/Resources/Data/table_fb/Monster.bytes','Assets/Resources/Data/table_fb/LevelWave.bytes',
 'Assets/Script/Game/Enemies/Boss/Skills/SummonModule.cs','Assets/Script/Game/Enemies/Boss/Skills/ReviveModule.cs',
 'Assets/Script/Game/Enemies/Boss/Skills/BladeBurstModule.cs','Assets/Script/Game/Enemies/Boss/Skills/DashModule.cs','Assets/Script/Game/Enemies/Boss/Skills/HomingKnifeModule.cs',
 'Assets/Script/Game/Enemies/Components/EnemyWordLabel.cs',
 'Assets/Res/Textures/VFX/sheet_boss_blade_burst_v1.png','Assets/Res/Textures/VFX/sheet_boss_blade_burst_v1.png.meta',
 'Assets/Res/Materials/VFX/mat_boss_blade_solid_v1.mat','Assets/Res/Prefabs/Effects/Impact/fx_boss_blade_dissolve_v1.prefab'
 ].map(same);
 const bosses=['boss_common','boss_30001','boss_30002','boss_30003'].map(name=>{
  const p='Assets/Prefab/Enemy/Boss/'+name+'.prefab',old=blocks(read(baseline+p)),now=blocks(read(p));
  return{path:p,guidPreserved:same(p+'.meta').unchanged,oldGameplayComponents:[...old].filter(([,b])=>[50,58,61,114].includes(b.type)).map(([id,b])=>({id,unchanged:now.get(id)?.text===b.text}))};
 });
 const old=compact(read(baseline+'Assets/Script/Game/Enemies/Boss/Skills/CloneModule.cs')),now=compact(read('Assets/Script/Game/Enemies/Boss/Skills/CloneModule.cs'));
 const statements=[
 'private float _hpPercent = 0.4f;','private int _cloneCount = 1;','private float _lifetime = 12f;','private float _chargeTime = 0.35f;',
 'firstDelayMul = 0.6f;','interval = p[0] > 0f ? p[0] : 8f;','_hpPercent = Mathf.Clamp01(p[1] > 0f ? p[1] : 0.4f);',
 '_cloneCount = Mathf.Max(1, (int)(p[2] > 0f ? p[2] : 1f));','_lifetime = p[3] > 0f ? p[3] : 12f;','cooldown = interval * firstDelayMul;',
 'yield return new WaitForSeconds(_chargeTime);','originEb.MaxHp * _hpPercent','float baseAngle = Random.Range(0f, 360f);',
 'float angle = (baseAngle + i * angleStep + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;','float radius = Random.Range(2.5f, 4.5f);',
 'eb.ApplyTableStats(0, Mathf.CeilToInt(cloneMaxHp), 0);'
 ].map(statement=>({statement,unchanged:old.includes(compact(statement))&&now.includes(compact(statement))}));
 const basePath='Assets/Script/Game/Enemies/Components/EnemyBase.cs';
 const baseDelta=read(basePath).replace('int killReward = GetComponent<CloneMarker>() != null ? 0 : Mathf.Max(1, rewardKillCount);','int killReward = Mathf.Max(1, rewardKillCount);')===read(baseline+basePath);
 const presentation=read('Assets/Script/Game/Enemies/Boss/BossClonePresentation.cs');
 const noNewBitmapsOrMaterials=!/new\s+(Texture2D|Material|Sprite)\s*\(/.test(presentation);
 const before=report('ArtOutput/UI/BossCloneV1/Before/validation-report.json'),after=report('ArtOutput/UI/BossCloneV1/After/validation-report.json');
 const snapshots=json('ArtOutput/UI/BossCloneV1/After/inheritance-snapshots.json');
 const afterChecks=json('ArtOutput/UI/BossCloneV1/After/validation-report.json').checks;
 const mixed=['mixed-dash','mixed-blade'].every(name=>[': whitelist survives real Start',': whitelist survives a later data-ready rebuild'].every(suffix=>afterChecks.some(c=>c.name===name+suffix&&c.passed===true)));
 const policies=snapshots.every(s=>!s.poolContamination&&s.actual.every(a=>a.length===1&&a[0]===s.expected));
 const config=json(dir+'configuration.json'),rebuild=json(dir+'rebuild.json'),toolkit=json(dir+'toolkit-report.json');
 const staticPassed=unchanged.every(x=>x.unchanged)&&bosses.every(b=>b.guidPreserved&&b.oldGameplayComponents.every(c=>c.unchanged))&&statements.every(x=>x.unchanged)&&baseDelta&&noNewBitmapsOrMaterials&&config.passed===true&&rebuild.passed===true;
 const passed=staticPassed&&before.passed&&after.passed&&policies&&mixed&&toolkit.passed===true;
 const result={passed,staticEvidence:{passed:staticPassed,unchanged,bosses,statements,enemyBaseChangeLimitedToCloneKillReward:baseDelta,noNewBitmapsOrMaterials,configurationChecks:Object.keys(config.checks).length,rebuildPassed:rebuild.passed},recordedUnityEvidence:{before,after,allRecordedPoliciesPass:policies,mixedSkillBossesIncluded:mixed,toolkitPassed:toolkit.passed,toolkitChecks:toolkit.checks.length},scope:{newPngBytes:0,newMaterials:0,cloneLifecycleRepaired:true,summonVisualsChanged:false,reviveVisualsChanged:false,nextSkill:'summon',gate:'clone visual acceptance before next skill'},note:'Read-only file audit. Actual gameplay and live Start checks were recorded separately. Before is real pre-change runtime, not a manual CloneMarker simulation. No WeChat FPS/upload-size claim.'};
 fs.writeFileSync(path.join(root,dir,'integrity.json'),JSON.stringify(result,null,2));console.log(JSON.stringify({passed,staticPassed,before:before.checks,after:after.checks,mixed,policies,changed:unchanged.filter(x=>!x.unchanged).map(x=>x.path),changedStatements:statements.filter(x=>!x.unchanged)}));if(!passed)process.exitCode=1;
}
try{main();}catch(e){console.error(e);process.exitCode=1;}
