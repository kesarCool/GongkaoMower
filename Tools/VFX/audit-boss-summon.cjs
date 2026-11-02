const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtOutput/VFX/BossSummonV1/Audit');
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,'').replace(/\r/g,'');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex').toUpperCase();
const checks={};
for(const p of JSON.parse(read('ArtOutput/VFX/BossSummonV1/before-integrity.json')))checks['unchanged:'+p.path]=hash(p.path)===p.sha256;
function blocks(text){const result=new Map();for(const b of text.split(/\n(?=--- !u!)/)){const m=b.match(/^--- !u!(\d+) &(\d+)/);if(m)result.set(m[2],{type:m[1],text:b.replace(/[ \t]+$/gm,'').trimEnd()});}return result;}
for(const name of ['boss_common','boss_30001','boss_30002','boss_30003']){
 const p=`Assets/Prefab/Enemy/Boss/${name}.prefab`,before=blocks(read('ArtOutput/VFX/BossSummonV1/Baseline/'+p)),after=blocks(read(p));
 const strip=t=>t.replace(/  m_Children:(?: \[\])?\n(?:  - \{fileID: \d+\}\n)*/g,'').trimEnd();
 checks[name+':allOriginalObjectsPreserved']=[...before.keys()].every(id=>after.has(id));
 checks[name+':allExistingComponentFieldsPreserved']=[...before].every(([id,b])=>after.has(id)&&(b.type==='4'||b.type==='224'?strip(b.text)===strip(after.get(id).text):b.text===after.get(id).text));
 const added=[...after.keys()].filter(id=>!before.has(id));
 checks[name+':onlySummonPresentationAdded']=added.length===5&&added.filter(id=>after.get(id).type==='1').length===1&&added.filter(id=>after.get(id).type==='1').every(id=>after.get(id).text.includes('m_Name: BossSummonVisual'));
}
const spawner=read('Assets/Script/Game/Spawning/SpawnerWaves.cs'),old=read('ArtOutput/VFX/BossSummonV1/Baseline/Assets/Script/Game/Spawning/SpawnerWaves.cs');
checks.originalPointAndSpawnMethodsPreserved=spawner.slice(spawner.indexOf('    private GameObject SpawnEnemy('))===old.slice(old.indexOf('    private GameObject SpawnEnemy('));
function groupBody(text,name){const from=text.indexOf('    private IEnumerator '+name+'('),to=text.indexOf('    /// <summary>按 wave 字段分组',from);const section=text.slice(from,to);return section.slice(section.indexOf('{')).replace(/\n\s*if \(spawnedGo != null && onSpawned != null\) onSpawned\(spawnedGo\);\n/,'').replace(/\n\s*\n/g,'\n').trim();}
checks.originalGroupSchedulingPreserved=groupBody(spawner,'TableWaveGroupWithFeedbackRoutine')===groupBody(old,'TableWaveGroupRoutine');
const afterReport=JSON.parse(read('ArtOutput/UI/BossSummonV1/After/validation-report.json'));
checks.actualSummonValidationPassed=afterReport.passed&&afterReport.checks.length===70&&afterReport.checks.every(c=>c.passed);
const docs=['Tools/VFX/boss-summon-v1.md','Tools/VFX/README.md','Tools/VFX/Library/README.md','Tools/VFX/Library/Boss/README.md','Tools/VFX/toolkit-guide.md','Tools/VFX/visual-spec.md','Tools/VFX/boss-skills-optimization-plan-v1.md','ArtOutput/VFX/BossSummonV1/brief.md'];
const broken=[];let links=0;for(const p of docs)for(const m of read(p).matchAll(/!?\[[^\]]*\]\(([^)]+)\)/g)){const ref=m[1].split('#')[0];if(!ref||/^[a-z]+:/i.test(ref))continue;links++;if(!fs.existsSync(path.resolve(path.dirname(path.join(root,p)),ref)))broken.push({document:p,target:ref});}
checks.documentLinks=broken.length===0;
fs.mkdirSync(out,{recursive:true});const result={passed:Object.values(checks).every(Boolean),checks,documents:docs.length,links,broken,checkedUtc:new Date().toISOString()};fs.writeFileSync(path.join(out,'integrity.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));if(!result.passed)process.exitCode=1;
