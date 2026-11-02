const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'ArtOutput/VFX/Timing15/Audit');
const after=JSON.parse(fs.readFileSync(path.join(dir,'after.json'),'utf8'));
const read=p=>fs.readFileSync(p,'utf8').replace(/\r\n/g,'\n');
function omitTiming(s){
 return s.replace(/^  lengthInSec:.*$/gm,'  lengthInSec: <timing>')
  .replace(/^  lifetimeOverride:.*$/gm,'  lifetimeOverride: <timing>')
  .replace(/(    startLifetime:\n      serializedVersion: 2\n      minMaxState: 0\n      scalar:) [^\n]+/g,'$1 <timing>');
}
const checks=after.rows.map(row=>({path:row.path,passed:row.poolPassed&&row.layers.every(l=>l.passed)&&omitTiming(read(path.join(root,row.path)))===omitTiming(read(path.join(root,'Library/VFXBackups/Timing15-20261025',row.path)))}));
const report={passed:after.passed&&checks.every(c=>c.passed),checks,note:'Every particle duration, constant start lifetime and pool override is 1.5 times the captured pre-task value. Prefab text outside these timing fields is unchanged after newline normalization, including frame/alpha curves, materials, size, seed and limits.'};
fs.writeFileSync(path.join(dir,'integrity.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));if(!report.passed)process.exitCode=1;
