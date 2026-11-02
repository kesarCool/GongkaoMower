const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/VFX/LightningBreakthroughV2/Audit');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex');
const def='Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_LightningStrike.asset';
const strip=s=>s.replace(/\r\n/g,'\n').replace(/^  maxLevelPrefab:[^\r\n]*\n?/gm,'');
const resources=JSON.parse(fs.readFileSync(path.join(dir,'resources.json'),'utf8'));
const files=resources.dependencies.filter(p=>!p.endsWith('.cs')).map(p=>({path:p,sourceBytes:fs.statSync(path.join(root,p)).size,sha256:sha(p)}));
const retirement=JSON.parse(fs.readFileSync(path.join(dir,'retirement.json'),'utf8').replace(/^\uFEFF/,''));
const checks=[
 {name:'Only the breakthrough VFX binding changes in the definition',passed:strip(read(def))===strip(read('Library/VFXBackups/LightningRedesignV2-20261025/'+def))},
 {name:'Ordinary blue atlas remains unchanged',passed:sha('Assets/Res/Textures/VFX/sheet_lightning_v1.png')==='c5d4c55aa77f5652f81f1f4668e65e54f5907f953bfcc31ac6372ba8f33e8ce7'},
 {name:'Dedicated atlas and two single-particle layers fit the source budget',passed:resources.width===1024&&resources.height===512&&resources.sourcePngBytes<=256*1024&&resources.particleSystems===2&&resources.materials===1},
 {name:'Retired source files retain their hashes and original Unity GUIDs',passed:retirement.every(r=>crypto.createHash('sha256').update(fs.readFileSync(r.archived)).digest('hex').toUpperCase()===r.sourceSha256&&fs.readFileSync(r.archived+'.meta','utf8').includes('guid: '+r.metaGuid))}
];
const report={passed:checks.every(c=>c.passed),checks,files,runtimeSourceBytesExcludingMeta:files.reduce((s,f)=>s+f.sourceBytes,0),note:'Source bytes are not final WeChat package size. Retirement metadata was reconstructed from standard importers with original GUIDs recovered from the Unity editor cache; original source-file hashes are preserved.'};
fs.writeFileSync(path.join(dir,'asset-integrity.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));if(!report.passed)process.exitCode=1;
