const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const sharp=require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(__dirname,'../..'),folder=path.join(root,'ArtOutput/VFX/SwordPresentationV1');
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/\r\n/g,'\n');
const json=p=>JSON.parse(read(p)),sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const bytes=p=>fs.readFileSync(path.join(root,p));
async function auditImage(p){
  const input=bytes(p),{data,info}=await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let transparent=0,partial=0,opaque=0;
  for(let i=3;i<data.length;i+=4){const a=data[i];if(a===0)transparent++;else if(a===255)opaque++;else partial++;}
  return {path:p,sha256:sha(input),sourceBytes:input.length,width:info.width,height:info.height,transparentPixels:transparent,partialAlphaPixels:partial,opaquePixels:opaque,hasRealTransparency:transparent>0&&opaque>0};
}
function blocks(text){return new Map([...text.matchAll(/^--- !u!\d+ &(-?\d+)\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)].map(m=>[m[1],m[2].trim()]));}
async function main(){
  const sources=await Promise.all(['ArtOutput/VFX/SwordPresentationV1/Sources/blades.png','ArtOutput/VFX/SwordPresentationV1/Sources/hit.png'].map(auditImage));
  fs.writeFileSync(path.join(folder,'Audit/sources.json'),JSON.stringify({passed:sources.every(x=>x.hasRealTransparency),tool:'built-in image_gen',sources},null,2));
  if(process.argv.includes('--sources-only')) {console.log(JSON.stringify(sources));return;}
  const r=json('Tools/VFX/Library/Projectiles/sword_presentation_v1.json');
  const atlas=await auditImage(r.texturePath),configuration=json('ArtOutput/VFX/SwordPresentationV1/Audit/configuration.json');
  const currentReport=r.validationReport||'ArtOutput/UI/SwordPresentationV1/After/validation-report.json';
  const before=json('ArtOutput/UI/SwordPresentationV1/Before/validation-report.json'),after=json(currentReport);
  const reportsComplete=read('ArtOutput/UI/SwordPresentationV1/Before/status.txt').trim()==='passed'&&read(currentReport.replace(/validation-report\.json$/,'status.txt')).trim()==='passed';
  const corePaths=['Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectileSword.asset','Assets/Script/Game/Combat/Skills/Implementations/SkillAutoProjectileSword.cs','Assets/Script/Game/Combat/Skills/Implementations/SkillAutoProjectile.cs','Assets/Script/Game/Combat/Projectiles/PlayerBullet.cs','Assets/Script/Game/Combat/Projectiles/PlayerStraightBullet.cs','Assets/Script/Game/Combat/Projectiles/AutoProjectileBurstBullet.cs','Assets/Script/Game/Combat/Projectiles/CritSplitOnHit.cs'];
  const core=corePaths.map(p=>({path:p,unchanged:sha(bytes(p))===sha(fs.readFileSync(path.join(folder,'Baseline',p))),sha256:sha(bytes(p))}));
  const prefabs=['BulletStraightSword','BulletStraightSwordMax','BurstBulletSword'].map(n=>{
    const p='Assets/Prefab/Combat/playerBullet/'+n+'.prefab',old=blocks(read('ArtOutput/VFX/SwordPresentationV1/Baseline/'+p)),now=blocks(read(p));
    const gameplay=[...old].filter(([,v])=>/^(CircleCollider2D|Rigidbody2D|MonoBehaviour):/.test(v));
    return {path:p,gameplayComponents:gameplay.map(([id,v])=>({id,type:v.split(':')[0],unchanged:now.get(id)===v})),guidUnchanged:sha(bytes(p+'.meta'))===sha(fs.readFileSync(path.join(folder,'Baseline',p+'.meta')))};
  });
  const {data,info}=await sharp(bytes(r.texturePath)).ensureAlpha().raw().toBuffer({resolveWithObject:true});let padded=true;
  for(let f=0;f<8;f++)for(let y=0;y<64;y++)for(let x=0;x<64;x++)if(x<3||x>60||y<3||y>60)padded&&=data[(y*info.width+f*64+x)*4+3]===0;
  const report={passed:reportsComplete&&configuration.passed&&before.passed&&after.passed&&sources.every(s=>s.hasRealTransparency)&&atlas.hasRealTransparency&&atlas.sourceBytes<=r.pngBudgetKiB*1024&&core.every(c=>c.unchanged)&&prefabs.every(p=>p.guidUnchanged&&p.gameplayComponents.every(c=>c.unchanged))&&padded,reportsComplete,core,prefabs,atlas,hitCellsHaveTransparentGutters:padded,baselineChecks:before.checks.length,currentChecks:after.checks.length,note:'Original skill logic, definition, projectile gameplay components and prefab GUIDs compared with the pre-task snapshot. Not a WeChat upload or device measurement.'};
  fs.writeFileSync(path.join(folder,'Audit/integrity.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));if(!report.passed)process.exitCode=1;
}
main().catch(e=>{console.error(e);process.exitCode=1;});
