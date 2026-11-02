const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname,'../..');
const backup = path.join(root,'Library/VFXBackups/TalismanPresentation-20261025');
const paths = ['Assets/Prefab/Combat/playerBullet/BulletStraightTalisman.prefab','Assets/Prefab/Combat/playerBullet/BulletStraightTalismanMax.prefab','Assets/Prefab/Combat/playerBullet/BurstBulletTailman.prefab','Assets/Resources/Traits/TalismanOrbit.prefab'];
const read = p=>fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n');
const blocks = s=>new Map([...s.matchAll(/^--- !u!(\d+) &(\d+)\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)].map(m=>[m[2],{type:+m[1],text:m[3]}]));
const checks=[];
for(const p of paths) {
  const before=blocks(read(path.join(backup,p))), after=blocks(read(path.join(root,p)));
  const protectedBlocks=[...before].filter(([,v])=>[50,58,114].includes(v.type));
  checks.push({path:p,check:'Every pre-existing rigidbody, collider and gameplay component remains byte-equivalent after newline normalization',passed:protectedBlocks.length>0 && protectedBlocks.every(([id,v])=>after.get(id)?.text===v.text),components:protectedBlocks.length});
}
const definition='Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectileTalisman.asset';
checks.push({check:'Skill definition unchanged',passed:fs.readFileSync(path.join(root,definition)).equals(fs.readFileSync(path.join(backup,definition)))});
const files=['Assets/Res/Textures/VFX/sheet_talisman_presentation_v1.png','Assets/Res/Materials/VFX/mat_talisman_presentation_v1.mat','Assets/Res/Materials/VFX/mat_talisman_trail_v1.mat','Assets/Res/Materials/VFX/TalismanTrailSoft.shader','Assets/Res/Prefabs/Effects/Impact/fx_talisman_hit_v1.prefab'];
const resources=files.map(p=>{const data=fs.readFileSync(path.join(root,p));return {path:p,sourceBytes:data.length,sha256:crypto.createHash('sha256').update(data).digest('hex')};});
const oldReferences=[];
function walk(dir) {for(const e of fs.readdirSync(dir,{withFileTypes:true})) {const p=path.join(dir,e.name);if(e.isDirectory())walk(p);else if(/\.(prefab|asset|unity|mat|cs)$/.test(e.name)&&fs.readFileSync(p,'utf8').includes('3aaae64950349d24f82cb7606eae9b13'))oldReferences.push(path.relative(root,p));}}
walk(path.join(root,'Assets'));
checks.push({check:'Old pale fulu image has no serialized or code GUID references under Assets',passed:oldReferences.length===0,remaining:oldReferences});
const report={passed:checks.every(c=>c.passed),checks,resources,sourceBytesExcludingMeta:resources.reduce((a,v)=>a+v.sourceBytes,0),note:'Source assets only, not final compressed WeChat package. Original 946-byte fulu.jpg retained for rollback; generated high-resolution sources remain outside Assets.'};
fs.writeFileSync(path.join(root,'ArtOutput/VFX/TalismanPresentationV1/Audit/asset-integrity.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify(report,null,2));
if(!report.passed)process.exitCode=1;
