// Read-only inspection of exported files. Writes reports outside Assets / Build.
const fs = require('node:fs');
const path = require('node:path');
const ROOT = path.resolve(__dirname, '../..');
const BASE = path.join(ROOT, 'Build/minigame/minigame');
const OUT = path.join(ROOT, 'ArtOutput/BuildAudit/20261024');
const MiB = 1024 * 1024;
const stat = file => fs.statSync(file);
const slash = value => value.replaceAll('\\', '/');
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const pretty = bytes => (bytes / MiB).toFixed(2);
function walk(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(dir, entry.name);
    if (entry.isSymbolicLink()) throw Error('Unexpected symlink in export: ' + file);
    return entry.isDirectory() ? walk(file) : [{ path: slash(path.relative(BASE, file)), bytes: stat(file).size, modifiedAt: stat(file).mtime.toISOString() }];
  });
}
const game = readJson(path.join(BASE, 'game.json'));
const config = readJson(path.join(BASE, 'project.config.json'));
const ignore = config.packOptions?.ignore || [];
function ignored(file) {
  return ignore.find(rule => {
    const value = slash(rule.value).replace(/^\/+|\/+$/g, '');
    if (rule.type === 'folder') return file.path === value || file.path.startsWith(value + '/');
    if (rule.type === 'file') return file.path === value;
    if (rule.type === 'suffix') return file.path.endsWith(rule.value);
    if (rule.type === 'prefix') return file.path.startsWith(value);
    if (rule.type === 'regexp') return new RegExp(rule.value).test(file.path);
    throw Error('Unimplemented pack ignore rule: ' + rule.type);
  });
}
const packages = [{ name: 'main', root: '', bytes: 0, fileCount: 0, limitBytes: 4 * MiB }];
for (const sub of game.subpackages || game.subPackages || []) packages.push({ name: sub.name || sub.root, root: slash(sub.root).replace(/\/+$/, '') + '/', bytes: 0, fileCount: 0, limitBytes: null });
const files = walk(BASE), skipped = [], candidates = [];
for (const file of files) {
  const reason = ignored(file);
  if (reason) { skipped.push({ ...file, ignoredBy: reason }); continue; }
  const group = packages.slice(1).find(p => file.path.startsWith(p.root)) || packages[0];
  group.bytes += file.bytes; group.fileCount++;
  candidates.push({ ...file, package: group.name });
}
const totalBytes = packages.reduce((n, p) => n + p.bytes, 0), limitBytes = 30 * MiB;
const manager = fs.readFileSync(path.join(BASE, 'game.js'), 'utf8');
const dataFileBytes = Number(manager.match(/DATA_FILE_SIZE:\s*['"](\d+)['"]/)?.[1] || 0);
const packed = readJson(path.join(OUT, 'unity-packed-assets.json'));
const assets = new Map();
for (const item of packed.entries) {
  const key = item.path || '<built-in / no source path>';
  const a = assets.get(key) || { path: key, bytes: 0, entries: 0, containers: new Set() };
  a.bytes += item.packedBytes; a.entries++; a.containers.add(item.container); assets.set(key, a);
}
function category(p) {
  if (/\.(ttc|ttf|otf)$/i.test(p) || /fonts[\\/]|fonts & materials[\\/]/i.test(p) && /SDF\.asset$/i.test(p)) return '字体与字体图集';
  if (p.startsWith('Assets/Res/Map/Styles/') || p.startsWith('Assets/ArtPreview/CartoonMaps_') || p.startsWith('Assets/Resources/Map/')) return '战斗地图、瓦片与装饰';
  if (p.startsWith('Assets/Res/image/Home/')) return '局外UI与主界面地图';
  if (p.startsWith('Assets/Res/image/Game/battle/') || p.startsWith('Assets/Res/image/Game/bosswheel/') || p.startsWith('Assets/Res/image/Game/select/')) return '局内UI';
  if (p.startsWith('Assets/Res/image/common/')) return '公共UI';
  if (/\.(mp3|ogg|wav|aif|aiff)$/i.test(p)) return '音频';
  if (p.startsWith('Assets/Resources/Data/')) return '配表';
  return '其他场景、角色、技能、特效及内置资源';
}
const categories = {};
const ranked = [...assets.values()].map(a => ({ ...a, containers: [...a.containers], category: category(a.path) })).sort((a,b) => b.bytes-a.bytes);
for (const a of ranked) categories[a.category] = (categories[a.category] || 0) + a.bytes;
const folderBytes = folder => fs.existsSync(folder) ? fs.readdirSync(folder,{withFileTypes:true}).reduce((n,e)=>n+(e.isDirectory()?folderBytes(path.join(folder,e.name)):stat(path.join(folder,e.name)).size),0) : 0;
const report = {
  measuredAt: new Date().toISOString(), exportPath: BASE, measurement: 'sum of on-disk exported file bytes, grouped by game.json; no DevTools compile/minify/upload-size result',
  units: 'MiB = 1048576 bytes; local SDK uses 30*1024*1024 limit', packages, totalBytes, limitBytes, remainingBytes: limitBytes-totalBytes,
  mainRemainingBytes: packages[0].limitBytes-packages[0].bytes, totalPercentUsed: totalBytes/limitBytes*100,
  excludedByPackOptions: skipped, localProjectConfigBytesIncludedConservatively: stat(path.join(BASE,'project.config.json')).size,
  noCdn: /DATA_CDN:\s*['"]['"]/.test(manager), loadDataFromSubpackage: /loadDataPackageFromSubpackage:\s*true/.test(manager), brotliData: /compressDataPackage:\s*true/.test(manager),
  unityDataBeforeBrotliBytes: dataFileBytes, developmentBuild: packed.options.includes('Development'),
  webglSiblingBytesNotAddedToUpload: folderBytes(path.join(ROOT,'Build/minigame/webgl')),
  unityPackedAssetAccountingBytes: ranked.reduce((n,a)=>n+a.bytes,0), categories, largestAssets: ranked,
  caveats: ['Unity PackedAssetInfo bytes are per-asset build accounting, not marginal bytes in the final Brotli stream. They must not be subtracted directly from the upload size.', 'Brotli compression and DevTools transforms are context dependent; actual upload may differ.', 'UnityPlugin is configured as a platform-supplied plugin; its delivered bytes are not represented by these local files.', 'No current source or export settings were modified.'],
  rules: [{title:'WeChat SDK official change log: 30MB resource-subpackage budget',url:'https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk/blob/main/CHANGELOG.md'}, {title:'Cocos official documentation: WeChat main/subpackage limits',url:'https://docs.cocos.com/creator/3.8/manual/zh/editor/publish/subpackage.html#微信小游戏'}]
};
fs.writeFileSync(path.join(OUT,'package-audit.json'),JSON.stringify(report,null,2));
fs.writeFileSync(path.join(OUT,'files.json'),JSON.stringify(candidates.sort((a,b)=>b.bytes-a.bytes),null,2));
fs.writeFileSync(path.join(OUT,'asset-sizes.csv'),'path,bytes,MiB,category\n'+ranked.map(a=>`"${a.path.replaceAll('"','""')}",${a.bytes},${(a.bytes/MiB).toFixed(4)},"${a.category}"`).join('\n'));
let md=`# 微信小游戏包体审计 · 2026-10-24\n\n核对构建：${BASE}。Unity 构建报告时间：${packed.ended}（该报告时间为 UTC）；本次构建为 Development。\n\n**本地导出文件共 ${pretty(totalBytes)} MiB，按 30 MiB 上限剩余 ${pretty(limitBytes-totalBytes)} MiB。主包剩余 ${pretty(report.mainRemainingBytes)} MiB。**\n\n所有 MiB 均按 1024×1024 字节计算。统计范围只含真正微信工程 minigame 子目录，未把旁边的 webgl 副本重复计入。\n\n| 分配 | 文件数 | 字节数 | MiB | 说明 |\n|---|---:|---:|---:|---|\n`;
for(const p of packages)md+=`| ${p.name} | ${p.fileCount} | ${p.bytes.toLocaleString('en-US')} | ${pretty(p.bytes)} | ${p.name==='main'?'参考上限4 MiB，其余分包另计':'与主包共用30 MiB总上限'} |\n`;
md+=`| 合计 | ${candidates.length} | ${totalBytes.toLocaleString('en-US')} | ${pretty(totalBytes)} | 已用 ${(report.totalPercentUsed).toFixed(1)}% |\n\n主包包括 ${pretty(stat(path.join(BASE,'images/login2.png')).size)} MiB 的启动图，以及 framework、适配脚本和 SDK 等文件。扫描保守包含 project.config.json 的 ${report.localProjectConfigBytesIncludedConservatively} 字节；微信工具会编译/压缩 JS 并应用自己的上传规则，因此实际上传体积以开发者工具结果为准。当前目录无文件命中 packOptions.ignore，符号文件没有计入。\n\n## 资源加载与统计口径\n\n- 没有 CDN，资源包放 data-package，WASM 放 wasmcode；两包均配置启动并行预加载。这是文件分包，不是章节资源按需加载。\n- 首资源文件 Brotli 前为 ${pretty(dataFileBytes)} MiB，Brotli 后为 ${pretty(packages.find(p=>p.name==='data-package').bytes)} MiB；剩余上传空间按后者计算。\n- webgl 目录包含同批资源与代码副本，不是第二份待上传内容。\n- 主包和分包的剩余额度不能相加：总包仍只有 ${pretty(limitBytes-totalBytes)} MiB 余量。\n\n## Unity资源来源\n\n以下是 Unity PackedAssetInfo 的分资产构建统计，**不是最终 .br 包内逐项压缩大小**。报告各项合计 ${pretty(report.unityPackedAssetAccountingBytes)} MiB，与最终数据文件及上传包属于不同口径，不能直接用于相减或预测精确节省量。\n\n| 分类 | 构建统计 MiB |\n|---|---:|\n`;
for(const [name,bytes]of Object.entries(categories).sort((a,b)=>b[1]-a[1]))md+=`| ${name} | ${pretty(bytes)} |\n`;
md+='\n最大的单项来源：\n\n| 资源 | 构建统计 MiB |\n|---|---:|\n';
for(const a of ranked.slice(0,15))md+=`| ${a.path} | ${pretty(a.bytes)} |\n`;
md+=`\n## 后续可用预算与优先项\n\n- 当前还没到30 MiB上限。建议预留约2 MiB给 SDK、代码和后续小改动，将近期新增入包预算先控制在 **约4.95 MiB**；这是工程预算建议。每轮重建核对实际增量。\n- 资源/地图进入Unity资源分包，主包有1.89 MiB空位并不代表能额外增加同等总空间。\n- 优先核查同一份字体的裁剪：msyh.ttc 在构建统计中为18.74 MiB，动态 TMP 字库保留源字体是当前最大的来源。不能直接删除，否则动态补字可能失败。可以后续按词库和UI字符裁剪同一源字体，或评估静态字库覆盖率，再构建验证节省量。\n- LiberationSans SDF及源字体也出现在报告中，约1.37 MiB（构建统计）。应先查TMP默认/回退引用，再判断是否可剔除；本次未修改。\n- 主包启动图目前1.19 MiB，是主包最大的单文件；如以后主包吃紧，可单独评估启动图编码压缩。\n- 小瓦片与Prefab复用可继续推进；新增整屏底图、帧动画和音频应按新构建的Brotli增量计，不能按原始PNG大小推算。\n- 保留在 ArtOutput 的原版与秋冬设计图、旧地图归档未出现在本次资源报告中，不占上传包体。\n\n## 限制依据\n\n- [微信官方转换SDK更新说明：首资源包分包总大小30MB](https://github.com/wechat-miniprogram/minigame-tuanjie-transform-sdk/blob/main/CHANGELOG.md)。本工程 WXConvertCore.cs 也按30×1024×1024计。\n- [Cocos官方文档：微信主包4M、总包30M](https://docs.cocos.com/creator/3.8/manual/zh/editor/publish/subpackage.html#微信小游戏)。微信官方分包页面本次访问失败，采用上述公开官方SDK与引擎官方文档交叉核对。\n\n本轮仅分析，没有修改工程资源、导出文件或打包设置，也没有执行上传。\n`;
fs.writeFileSync(path.join(OUT,'report.md'),md);
console.log(JSON.stringify({packages,totalBytes,totalMiB:pretty(totalBytes),remainingMiB:pretty(limitBytes-totalBytes),mainRemainingMiB:pretty(report.mainRemainingBytes),categories,largest:ranked.slice(0,8),report:path.join(OUT,'report.md')},null,2));
