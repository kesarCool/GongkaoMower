const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/VFX/HitFeedbackAlignmentV1');
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/\r\n/g,'\n');
const json=p=>JSON.parse(read(p)),hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const checks=[];function check(name,passed,detail=''){checks.push({name,passed,detail});}
function main(){
 const reports={};
 for(const name of ['Sword','Talisman','Lightning','Explosions','Beam']){
  const p=`ArtOutput/UI/HitFeedbackAlignmentV1/${name}/validation-report.json`,r=json(p);
  check(`${name}: completed real skill report`,r.passed&&r.checks.every(c=>c.passed)&&read(path.posix.join(path.posix.dirname(p),'status.txt')).trim()==='passed');reports[name]={path:p,checks:r.checks.length};
 }
 const glyph=json('ArtOutput/VFX/HitFeedbackAlignmentV1/anchor-checks.json');check('Visible glyph placement checks pass',glyph.passed&&glyph.checks.every(c=>c.passed));
 const pre=p=>read('ArtOutput/VFX/HitFeedbackAlignmentV1/Before/'+p);
 const lightning='Assets/Script/Game/Combat/Skills/Implementations/SkillLightningStrike.cs';
 check('Lightning AOE calculation is unchanged',pre(lightning).split('    private void ApplyStrikeDamage')[1]===read(lightning).split('    private void ApplyStrikeDamage')[1]);
 const missile='Assets/Script/Game/Combat/Projectiles/HomingMissileBullet.cs';
 check('Missile AOE damage and release calculation are unchanged',pre(missile).split('        // AOE 伤害')[1]===read(missile).split('        // AOE 伤害')[1]);
 const beam='Assets/Script/Game/Combat/Skills/Implementations/SkillLineBeam2D.cs';
 const oldBeam=pre(beam),newBeam=read(beam).replace('_presentation.AddImpact(CombatHitPlacement.Contact(col, _sharedRayHits[h].point))','_presentation.AddImpact(_sharedRayHits[h].point)');
 check('Ray gameplay file differs only at the visual impact call',oldBeam===newBeam);
 const blocks=t=>new Map([...t.matchAll(/^--- !u!\d+ &(-?\d+)\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)].map(m=>[m[1],m[2].trim()]));
 for(const name of ['BulletStraightSword','BulletStraightSwordMax','BurstBulletSword']){
  const p='Assets/Prefab/Combat/playerBullet/'+name+'.prefab',old=blocks(pre(p)),now=blocks(read(p));
  check(`${name}: original physics and gameplay/feedback settings unchanged`,[...old].filter(([,v])=>/^(CircleCollider2D|Rigidbody2D|MonoBehaviour):/.test(v)).every(([id,v])=>now.get(id)===v));
  check(`${name}: prefab GUID unchanged`,fs.readFileSync(path.join(root,p+'.meta'),'utf8')===fs.readFileSync(path.join(root,'ArtOutput/VFX/SwordPresentationV1/Baseline',p+'.meta'),'utf8'));
 }
 const image=fs.readFileSync(path.join(root,'Assets/Res/Textures/VFX/sheet_sword_presentation_v1.png'));
 check('Runtime art bytes are identical to the approved atlas',image.length===37493&&hash(image)==='645d27a7f42610bedea36c8fcc87fd1991f30e0e0ffcb794ee28ca14904855c2');
 const recipe=json('Tools/VFX/Library/Projectiles/sword_presentation_v1.json');check('Length is reduced 20% without reducing thickness',recipe.bladeLengthMultiplier===.8&&recipe.normalScale===.88&&recipe.maxScale===1&&recipe.normalBladeAspectY===1.35&&recipe.maxBladeAspectY===1.45);
 const toolkit=json('ArtOutput/VFX/HitFeedbackAlignmentV1/toolkit-report.json');check('Import and preview regression passes and cleans temporary assets',toolkit.passed&&toolkit.fixtureAssetsRemoved);
 const report={passed:checks.every(c=>c.passed),checks,reports,glyphChecks:glyph.checks.length,toolkitChecks:toolkit.checks.length,realSkillChecks:Object.values(reports).reduce((s,r)=>s+r.checks,0),sourcePngBytes:image.length,note:'Presentation-only coordinate changes. Real physics/damage and byte-level guards are checked separately; no WeChat device/build-size claim.'};
 fs.writeFileSync(path.join(dir,'integrity.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));if(!report.passed)process.exitCode=1;
}
try{main();}catch(e){console.error(e);process.exitCode=1;}
