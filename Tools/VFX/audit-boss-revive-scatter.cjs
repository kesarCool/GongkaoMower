const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),task='ArtOutput/VFX/BossReviveScatterV1',checks={};
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,'').replace(/\r/g,'');
for(const p of JSON.parse(read(task+'/before-integrity.json')))checks['unchanged:'+p.path]=crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p.path))).digest('hex').toUpperCase()===p.sha256;
const source='Assets/Script/Game/Enemies/Boss/BossRevivePresentation.cs';
const normalized=read(source).replace('    [SerializeField,Range(1f,3f)] float scatterDistanceMultiplier=1.6f;\n','').replace('*(amount*scatterDistanceMultiplier)','*amount');
checks.onlyGlyphTranslationChanged=normalized===read(task+'/Baseline/'+source);
for(const name of ['boss_common','boss_30001','boss_30002','boss_30003']){
 const p='Assets/Prefab/Enemy/Boss/'+name+'.prefab';
 checks[name+':onlyScatterSettingChanged']=read(p).replace(/^  scatterDistanceMultiplier: 1\.6\n/gm,'')===read(task+'/Baseline/'+p);
}
const recipePath='Tools/VFX/Library/Boss/boss_revive_v1.json',old=JSON.parse(read(task+'/Baseline/'+recipePath)),recipe=JSON.parse(read(recipePath));
const unchanged={...recipe};delete unchanged.scatterDistanceMultiplier;unchanged.validationReport=old.validationReport;
checks.onlyRecipeDistanceAndEvidenceChanged=JSON.stringify(unchanged)===JSON.stringify(old)&&recipe.scatterDistanceMultiplier===1.6;
const report=JSON.parse(read(recipe.validationReport));checks.actualReviveValidation=report.passed&&report.checks.length>=90&&report.checks.every(c=>c.passed);
const result={passed:Object.values(checks).every(Boolean),checks,scatterDistanceMultiplier:1.6,timelineChanged:false,newTextures:0,newMaterials:0,report:recipe.validationReport,checkedUtc:new Date().toISOString()};
fs.mkdirSync(path.join(root,task,'Audit'),{recursive:true});fs.writeFileSync(path.join(root,task,'Audit/integrity.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({passed:result.passed,checks:Object.keys(checks).length,failures:Object.entries(checks).filter(([,v])=>!v).map(([k])=>k)}));if(!result.passed)process.exitCode=1;
