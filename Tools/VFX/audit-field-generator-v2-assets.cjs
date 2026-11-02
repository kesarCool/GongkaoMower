const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),folder=path.join(root,'ArtOutput/VFX/FieldGeneratorV2');
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/\r\n/g,'\n');
const json=p=>JSON.parse(read(p));
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const oldSource=read('ArtOutput/VFX/FieldGeneratorV1/Baseline/SkillFieldGenerator.cs');
const source=read('Assets/Script/Game/Combat/Skills/Implementations/SkillFieldGenerator.cs');
const oldDamage=oldSource.match(/    private void ApplyDamageInRadius\(float damageAmount\)[\s\S]*?(?=    \/\/ ── 贴图缓存)/)?.[0].trim();
const damage=source.match(/    private void ApplyDamageInRadius\(float damageAmount\)[\s\S]*?(?=\n}\s*$)/)?.[0].replace(/^            if \(_presentation != null\) _presentation.NotifyDamageTick\(\);\n/m,'').trim();
const strip=s=>s.replace(/^  (fieldVisualPrefab|visualBaseDiameter|sortingOrder):[^\n]*\n?/gm,'');
const gameplaySame=strip(read('Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_FieldGenerator.asset'))===strip(read('ArtOutput/VFX/FieldGeneratorV1/Baseline/SkillDef_FieldGenerator.asset'));
const recipe=json('Tools/VFX/Library/Persistent/field_generator_v2.json');
const configuration=json('ArtOutput/VFX/FieldGeneratorV2/Audit/configuration.json');
const resources=json('ArtOutput/VFX/FieldGeneratorV2/Audit/resources.json');
const after=json(recipe.validationReport),preview=json('ArtOutput/VFX/FieldGeneratorV2/Audit/editor-preview-checks.json');
const toolkit=json('ArtOutput/VFX/FieldGeneratorV2/Audit/toolkit-report.json'),rebuild=json('ArtOutput/VFX/FieldGeneratorV2/Audit/rebuild.json');
const currentDefinition=read('Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_FieldGenerator.asset');
const prefabGuid=read(recipe.prefabPath+'.meta').match(/^guid: (\w+)/m)?.[1];
const bound=!!prefabGuid&&currentDefinition.includes('guid: '+prefabGuid);
const assetHashesCurrent=rebuild.assets.every(a=>a.guidPreserved&&a.bytesIdentical&&sha(fs.readFileSync(path.join(root,a.path)))===a.sha256.toLowerCase());
const reference=fs.readFileSync(path.join(root,recipe.referencePath));
const inputPath='C:/Users/Admin/Desktop/QQ20261026-225222.mp4';
const inputVerified=fs.existsSync(inputPath)?sha(fs.readFileSync(inputPath))===sha(reference):null;
const report={
  passed:!!oldDamage&&oldDamage===damage&&gameplaySame&&bound&&configuration.passed&&after.passed&&preview.passed&&toolkit.passed&&rebuild.passed&&assetHashesCurrent&&resources.dedicatedTextures===0&&resources.sharedMaterials===1&&resources.renderers===3&&inputVerified!==false,
  damageImplementationPreserved:!!oldDamage&&oldDamage===damage,definitionGameplayFieldsPreserved:gameplaySame,currentPrefabBound:bound,
  rebuildHashesMatchCurrentAssets:assetHashesCurrent,resourceSummary:resources,
  runtimeChecks:after.checks.length,editorPreviewChecks:preview.checks.length,toolkitChecks:toolkit.checks.length,
  reference:{path:recipe.referencePath,bytes:reference.length,sha256:sha(reference),matchesUserInputWhenAvailable:inputVerified},
  sources:['Assets/Script/Game/Combat/Visuals/FieldGeneratorPresentation.cs','Assets/Script/Game/Combat/Visuals/FieldGeneratorPresentation.Surface.cs','Assets/Script/Game/Combat/Skills/Implementations/SkillFieldGenerator.cs'].map(p=>({path:p,sha256:sha(fs.readFileSync(path.join(root,p)))})),
  note:'V1 damage notification is excluded from comparison against the pre-V1 damage method. V2 changes presentation only. No WeChat package or device FPS measurement.'
};
fs.writeFileSync(path.join(folder,'Audit/integrity.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({passed:report.passed,damageImplementationPreserved:report.damageImplementationPreserved,definitionGameplayFieldsPreserved:gameplaySame,currentPrefabBound:bound,runtimeChecks:report.runtimeChecks,editorPreviewChecks:report.editorPreviewChecks,toolkitChecks:report.toolkitChecks,sourceAssetBytes:resources.sourceAssetBytes}));
if(!report.passed)process.exitCode=1;
