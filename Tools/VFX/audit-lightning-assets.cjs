const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),folder=path.join(root,'ArtOutput/VFX/LightningPresentationV1/Audit');
const before=JSON.parse(fs.readFileSync(path.join(folder,'before-resources.json'),'utf8'));
const after=JSON.parse(fs.readFileSync(path.join(folder,'after-resources.json'),'utf8'));
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const backup='Library/VFXBackups/LightningPresentation-20261025/';
const code='Assets/Script/Game/Combat/Skills/Implementations/SkillLightningStrike.cs';
const def='Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_LightningStrike.asset';
const normalize=s=>s.replace(/\r\n/g,'\n');
const stripReference=s=>normalize(s).replace(/^  (strikeFxPrefab|maxLevelPrefab):[^\r\n]*\n?/gm,'');
const combat=s=>normalize(s).slice(normalize(s).indexOf('public void ApplyRuntimeStats(')).replace(/CombatVfxSpawner.TryPlayPooled\([^;\n]*\);/g,'');
const checks=[{check:'All skill definition fields except the VFX references are unchanged',passed:stripReference(read(def))===stripReference(read(backup+def))},
{check:'Gameplay stats, cooldown, targeting, stagger and damage code remain unchanged apart from VFX selection',passed:combat(read(backup+code))===combat(read(code))},
{check:'One small atlas and two bounded particle layers',passed:after.sourcePngBytes<=128*1024&&after.particleSystems===2&&after.maximumParticlesPerStrike===2&&after.materials===1}];
const oldPng=before.dependencies.filter(d=>/\.png$/i.test(d.path));
const resources=after.dependencies.filter(p=>!p.endsWith('.cs')).map(p=>{const b=fs.readFileSync(path.join(root,p));return{path:p,sourceBytes:b.length,sha256:crypto.createHash('sha256').update(b).digest('hex')};});
const summary={passed:checks.every(c=>c.passed),checks,oldParticleSystems:before.layers.length,newParticleSystems:after.particleSystems,oldSelectedPngCount:oldPng.length,oldSelectedPngBytes:oldPng.reduce((s,d)=>s+d.bytes,0),newSelectedPngBytes:after.sourcePngBytes,newSourceAssetBytesExcludingMeta:resources.reduce((s,d)=>s+d.sourceBytes,0),resources,note:'Selected dependency totals are not global project or final WeChat build savings. Legacy shared textures/materials remain. High-resolution authored sources are outside Assets.'};
fs.writeFileSync(path.join(folder,'asset-integrity.json'),JSON.stringify(summary,null,2));console.log(JSON.stringify(summary,null,2));if(!summary.passed)process.exitCode=1;
