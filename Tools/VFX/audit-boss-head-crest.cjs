const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'ArtOutput/VFX/BossHeadCrestV1/Audit');
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,'');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex').toUpperCase();
const checks={};
for(const entry of JSON.parse(read('ArtOutput/VFX/BossHeadCrestV1/before-integrity.json'))) checks['unchanged:'+entry.path]=hash(entry.path)===entry.sha256;
function blocks(text){const map=new Map();for(const block of text.replace(/\r/g,'').split(/\n(?=--- !u!)/)){const m=block.match(/^--- !u!(\d+) &(\d+)/);if(m)map.set(m[2],{type:m[1],text:block.replace(/[ \t]+$/gm,'').trimEnd()});}return map;}
for(const name of ['boss_common','boss_30001','boss_30002','boss_30003']){
 const p=`Assets/Prefab/Enemy/Boss/${name}.prefab`,before=blocks(read('ArtOutput/VFX/BossHeadCrestV1/Baseline/'+p)),after=blocks(read(p));
 const allowedRemoved=new Set();
 for(const [id,b] of before)if(b.type==='1'&&/^  m_Name: (LeftHorn|RightHorn)$/m.test(b.text)){
  allowedRemoved.add(id);for(const m of b.text.matchAll(/component: \{fileID: (\d+)\}/g))allowedRemoved.add(m[1]);
 }
 const removed=[...before.keys()].filter(id=>!after.has(id)),added=[...after.keys()].filter(id=>!before.has(id));
 checks[name+':onlyOldDecorationObjectsRemoved']=allowedRemoved.size===6&&removed.length===6&&removed.every(id=>allowedRemoved.has(id));
 checks[name+':onlyCrestObjectsAdded']=added.length===5&&added.every(id=>after.get(id).type==='1'?after.get(id).text.includes('m_Name: BossHeadCrestVisual'):true);
 const strip=text=>text.replace(/  m_Children:(?: \[\])?\n(?:  - \{fileID: \d+\}\n)*/g,'').replace(/  m_RootOrder: \d+\n/g,'').trimEnd();
 checks[name+':allExistingComponentFieldsPreserved']=[...before].filter(([id])=>after.has(id)).every(([id,b])=>b.type==='4'||b.type==='224'?strip(b.text)===strip(after.get(id).text):b.text===after.get(id).text);
 checks[name+':spawnPointPreserved']=read(p).includes('m_Name: KnifeSpawnPoint');
}
const report=JSON.parse(read('ArtOutput/UI/BossHeadCrestV1/After/validation-report.json'));
checks.realCombatAndAnchorChecks=report.passed&&report.checks.length===100&&report.checks.every(c=>c.passed);
const docs=['Tools/VFX/boss-head-crest-v1.md','Tools/VFX/README.md','Tools/VFX/Library/README.md','Tools/VFX/Library/Boss/README.md','Tools/VFX/toolkit-guide.md','Tools/VFX/visual-spec.md'];
const broken=[];let links=0;
for(const p of docs)for(const m of read(p).matchAll(/!?\[[^\]]*\]\(([^)]+)\)/g)){
 const ref=m[1].split('#')[0];if(!ref||/^[a-z]+:/i.test(ref))continue;links++;
 if(!fs.existsSync(path.resolve(path.dirname(path.join(root,p)),ref)))broken.push({document:p,target:ref});
}
checks.documentLinks=broken.length===0;
fs.mkdirSync(out,{recursive:true});
const result={passed:Object.values(checks).every(Boolean),checks,documents:docs.length,links,broken,checkedUtc:new Date().toISOString()};
fs.writeFileSync(path.join(out,'integrity.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify(result));if(!result.passed)process.exitCode=1;
