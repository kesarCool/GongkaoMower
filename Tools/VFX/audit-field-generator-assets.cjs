const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),folder=path.join(root,'ArtOutput/VFX/FieldGeneratorV1');
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/\r\n/g,'\n');
const oldSource=read('ArtOutput/VFX/FieldGeneratorV1/Baseline/SkillFieldGenerator.cs');
const source=read('Assets/Script/Game/Combat/Skills/Implementations/SkillFieldGenerator.cs');
const oldDamage=oldSource.match(/    private void ApplyDamageInRadius\(float damageAmount\)[\s\S]*?(?=    \/\/ ── 贴图缓存)/)?.[0].trim();
const damage=source.match(/    private void ApplyDamageInRadius\(float damageAmount\)[\s\S]*?(?=\n}\s*$)/)?.[0].replace(/^            if \(_presentation != null\) _presentation.NotifyDamageTick\(\);\n/m,'').trim();
const strip=s=>s.replace(/^  (fieldVisualPrefab|visualBaseDiameter|sortingOrder):[^\n]*\n?/gm,'');
const definitionUnchanged=strip(read('Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_FieldGenerator.asset'))===strip(read('ArtOutput/VFX/FieldGeneratorV1/Baseline/SkillDef_FieldGenerator.asset'));
const recipe=JSON.parse(read('Tools/VFX/Library/Persistent/field_generator_v1.json'));
const configuration=JSON.parse(fs.readFileSync(path.join(folder,'Audit/configuration.json'),'utf8'));
const resources=JSON.parse(fs.readFileSync(path.join(folder,'Audit/resources.json'),'utf8'));
const after=JSON.parse(read('ArtOutput/UI/FieldGeneratorV1/After/validation-report.json'));
const png=fs.readFileSync(path.join(root,recipe.texturePath));
const report={passed:!!oldDamage&&oldDamage===damage&&definitionUnchanged&&configuration.passed&&after.passed&&png.length<=recipe.pngBudgetKiB*1024,
 damageImplementationPreserved:!!oldDamage&&oldDamage===damage,definitionGameplayFieldsPreserved:definitionUnchanged,
 sourcePngBytes:png.length,sourcePngSha256:crypto.createHash('sha256').update(png).digest('hex'),resources,checks:after.checks.length,
 sources:[recipe.normalSource,recipe.breakthroughSource].map(p=>({path:p,sha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex')}))};
fs.writeFileSync(path.join(folder,'Audit/integrity.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({passed:report.passed,damageImplementationPreserved:report.damageImplementationPreserved,definitionGameplayFieldsPreserved:definitionUnchanged,sourcePngBytes:png.length}));
if(!report.passed)process.exitCode=1;
