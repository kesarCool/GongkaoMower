// Static catalog verification only. Never starts Unity, invokes builders or rewrites historical evidence.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const args = process.argv.slice(2);
if (args.includes('--help')) {
  console.log('node Tools/VFX/audit-boss-workflow.cjs [--write-index]\nChecks delivery references and archived reports; optionally regenerates boss-deliverables.md. Writes ArtOutput/VFX/BossWorkflow/Audit/catalog-check.json. Does not run Unity.');
  process.exit(0);
}
if (args.some(a => a !== '--write-index')) throw Error('Unknown argument; use --help.');
const catalogPath = 'Tools/VFX/boss-deliverables.json';
const indexPath = 'Tools/VFX/boss-deliverables.md';
const checks = [], files = new Set(), evidence = new Map();
function check(name, passed, detail = '') { checks.push({ name, passed: Boolean(passed), ...(detail ? { detail } : {}) }); }
function full(p) {
  if (typeof p !== 'string' || !p || path.isAbsolute(p)) throw Error('Expected project-relative path: ' + p);
  const resolved = path.resolve(root, p);
  if (!resolved.startsWith(root + path.sep)) throw Error('Path escapes project: ' + p);
  return resolved;
}
function exists(p) {
  const ok = fs.existsSync(full(p)) && fs.statSync(full(p)).isFile();
  if (!files.has(p)) { files.add(p); check('file:' + p, ok); }
  return ok;
}
function read(p) { return fs.readFileSync(full(p), 'utf8').replace(/^\uFEFF/, ''); }
function json(p) { return JSON.parse(read(p)); }
function report(p) {
  if (evidence.has(p)) return evidence.get(p);
  let result = { passed: false, count: 0 };
  if (exists(p)) {
    const r = json(p), rows = Array.isArray(r.checks) ? r.checks : Object.values(r.checks || {});
    result = { passed: r.passed === true && rows.length > 0 && rows.every(c => typeof c === 'boolean' ? c : c.passed === true), count: rows.length };
    check('archived-report:' + p, result.passed, 'Archived evidence only; not re-executed by this command.');
  }
  evidence.set(p, result); return result;
}
const manifest = json(catalogPath);
check('schemaVersion', manifest.schemaVersion === 1);
for (const p of [manifest.workflowPath, manifest.briefTemplatePath, manifest.promptTemplatePath, ...manifest.bossPrefabs, ...manifest.sharedAssets]) exists(p);
const sessionPath = 'Assets/Editor/SkillVfxToolkit/SkillVfxValidationSession.cs';
const launcherPath = 'Assets/Editor/HomeUIEditorPreview.cs';
exists(sessionPath); exists(launcherPath);
const session = read(sessionPath), launcher = read(launcherPath);
const validationMenus = new Map([...session.matchAll(/\[MenuItem\("([^"]+)"\)\]\s*public static void [^\n]*?=> Begin\("([^"]+)"\);/g)].map(m => [m[1], m[2]]));
const ids = new Set(), entries = [];
for (const e of manifest.entries) {
  check('unique-id:' + e.id, !ids.has(e.id)); ids.add(e.id);
  check('kind:' + e.id, ['skill', 'common-visual'].includes(e.kind));
  check('integration:' + e.id, ['table-connected', 'capability-only', 'common-prefab-connected'].includes(e.integration));
  for (const p of [e.recipePath, e.modulePath, ...e.presentationPaths, e.builderPath, e.runnerPath, e.videoPath, e.comparisonPath, e.proofScript, e.integrityScript].filter(Boolean)) exists(p);
  if (!fs.existsSync(full(e.recipePath))) continue;
  const recipe = json(e.recipePath);
  check('recipe-id:' + e.id, recipe.id === e.id);
  for (const [key, p] of Object.entries(recipe)) if (p && typeof p === 'string' && (key.endsWith('Path') || key.endsWith('Source'))) exists(p);
  const builder = read(e.builderPath);
  check('build-menu:' + e.id, builder.includes('[MenuItem("' + e.buildMenu + '")]'));
  check('audit-menu:' + e.id, builder.includes('[MenuItem("' + e.auditMenu + '")]'));
  check('validation-menu:' + e.id, validationMenus.get(e.validationMenu) === e.validationPhase);
  const runner = path.basename(e.runnerPath, '.cs');
  check('validation-launcher:' + e.id, launcher.includes('if(phase=="' + e.validationPhase + '") name="' + runner + '";'));
  const primary = report(recipe.validationReport), configuration = report(e.configurationReport);
  const regressions = e.regressionReports.map(p => ({ path: p, ...report(p) }));
  entries.push({ ...e, recipe, primary, configuration, regressions });
}
const libraryIds = fs.readdirSync(full('Tools/VFX/Library/Boss')).filter(p => p.endsWith('.json')).map(p => json('Tools/VFX/Library/Boss/' + p).id);
check('every-boss-recipe-indexed', libraryIds.length === ids.size && libraryIds.every(id => ids.has(id)));
for (const b of manifest.backlog) if (b.modulePath) exists(b.modulePath);

const labels = { 'table-connected': '正式表已用', 'capability-only': '能力完成，未配表', 'common-prefab-connected': '公共 Prefab 已接入' };
function link(label, p) { return '[' + label + '](' + path.relative(path.dirname(full(indexPath)), full(p)).replace(/\\/g, '/') + ')'; }
{
  const lines = [
    '# Boss 技能产出总目录', '',
    '更新：' + manifest.updatedDate + '。由 ' + link('产出索引', catalogPath) + ' 和 `node Tools/VFX/audit-boss-workflow.cjs --write-index` 生成。编辑索引后重新生成本页。', '',
    '制作从 ' + link('Boss 工作流', manifest.workflowPath) + ' 开始；复制 ' + link('任务单', manifest.briefTemplatePath) + '，使用 ' + link('提示词 / 执行指令', manifest.promptTemplatePath) + '。', '',
    '本页核对的是归档证据及文件 / 菜单引用，没有重新运行 Unity 战斗。配表状态由制作记录维护；观感确认与微信真机结果分别记录。冲刺 / 小刀、分身 / 头标存在共用报告，检查数不相加为一个总验收数。', '',
    '| 产出 | 接入范围 | 历史主报告 | 配置检查 | 原速预览 |',
    '| --- | --- | --- | --- | --- |'
  ];
  for (const e of entries) lines.push('| ' + link(e.name, e.recipe.briefPath) + ' | ' + labels[e.integration] + ' | ' + link(e.primary.count + ' 项', e.recipe.validationReport) + ' | ' + link(e.configuration.count + ' 项', e.configurationReport) + ' | ' + link('录像', e.videoPath) + ' |');
  const connected = entries.filter(e => e.kind === 'skill' && e.integration === 'table-connected').length;
  const pending = entries.filter(e => e.integration === 'capability-only').map(e => e.name);
  const ordinaryRecipes = fs.readdirSync(full('Tools/VFX/Library/Recipes')).filter(p => p.endsWith('.json')).length;
  lines.push('', '共 ' + entries.filter(e => e.kind === 'skill').length + ' 个技能配置和 ' + entries.filter(e => e.kind === 'common-visual').length + ' 个公共外观配置；其中 ' + connected + ' 类技能已由正式表使用' + (pending.length ? '，' + pending.join('、') + '待配表' : '') + '。普通一次性库现为 ' + ordinaryRecipes + ' 项，Boss 小消散已计在其中。', '', '## 共享正式资源', '');
  for (const p of manifest.sharedAssets) lines.push('- ' + link(path.basename(p), p) + (p.endsWith('.png') ? '：' + fs.statSync(full(p)).size.toLocaleString('en-US') + ' 字节源 PNG，按路径只计一次。' : ''));
  lines.push('', '字体快照使用实际 TMP 共享字体材质；原稿位于 Assets 外。源 PNG、压缩块数据、运行内存和最终微信包体分别计量。', '', '## 各项工程和证据入口', '');
  for (const e of entries) {
    lines.push('### ' + e.name + ' · ' + e.id, '', e.summary, '',
      '- 接入：' + labels[e.integration] + '；配方记录状态：`' + e.recipe.status + '`。',
      '- ' + link('配置', e.recipePath) + ' / ' + link('制作说明', e.recipe.briefPath) + ' / ' + link('构建器', e.builderPath) + (e.modulePath ? ' / ' + link('技能模块', e.modulePath) : '') + ' / ' + e.presentationPaths.map((p, i) => link('表现组件' + (e.presentationPaths.length > 1 ? i + 1 : ''), p)).join(' / '),
      '- 构建：`' + e.buildMenu + '`；检查：`' + e.auditMenu + '`。',
      '- 实战：`' + e.validationMenu + '`；phase：`' + e.validationPhase + '`；' + link('夹具', e.runnerPath) + '。',
      '- ' + link('归档实战 ' + e.primary.count + ' 项', e.recipe.validationReport) + ' / ' + link('配置报告', e.configurationReport) + ' / ' + link('原速录像', e.videoPath) + ' / ' + link('对比图', e.comparisonPath),
      '- 证据工具：' + link('录像导出', e.proofScript) + ' / ' + link('该版本完整性检查', e.integrityScript) + '（使用固定历史目录，运行前核对输出范围）。');
    if (e.regressions.length) lines.push('- 后续归档回归：' + e.regressions.map(r => link(r.count + ' 项', r.path)).join(' / ') + '。');
    for (const limit of e.limits) lines.push('- 边界：' + limit);
    lines.push('');
  }
  lines.push('## 后续项', '');
  for (const b of manifest.backlog) lines.push('- **' + b.name + '**（`' + b.status + '`）：' + b.note + (b.modulePath ? ' ' + link('代码', b.modulePath) : ''));
  lines.push('', '## 当前目录核对', '', link('静态检查报告', 'ArtOutput/VFX/BossWorkflow/Audit/catalog-check.json') + '检查目录完整性和历史报告字段；不替代重新编译、Prefab 构建、实战或真机验证。', '');
  const indexText = lines.join('\n');
  if (args.includes('--write-index')) fs.writeFileSync(full(indexPath), indexText);
  check('generated-index-current', fs.existsSync(full(indexPath)) && read(indexPath) === indexText, 'Regenerate with --write-index after editing the manifest.');
}

// Reserve the current report path before checking its link from the generated index.
const output = 'ArtOutput/VFX/BossWorkflow/Audit/catalog-check.json';
fs.mkdirSync(path.dirname(full(output)), { recursive: true });
if (!fs.existsSync(full(output))) fs.writeFileSync(full(output), '{}\n');
let linkCount = 0;
const documents = [manifest.workflowPath, manifest.briefTemplatePath, manifest.promptTemplatePath, indexPath, 'Tools/VFX/README.md', 'Tools/VFX/Library/Boss/README.md', 'Tools/VFX/toolkit-guide.md', 'Tools/VFX/prompt-templates.md', 'Tools/VFX/boss-skills-optimization-plan-v1.md'];
for (const p of documents) {
  if (!exists(p)) continue;
  for (const m of read(p).matchAll(/!?\[[^\]]*\]\(([^)]+)\)/g)) {
    const ref = m[1].split('#')[0]; if (!ref || /^[a-z]+:/i.test(ref)) continue;
    const target = path.resolve(path.dirname(full(p)), ref); linkCount++;
    check('link:' + p + ' -> ' + ref, target.startsWith(root + path.sep) && fs.existsSync(target));
  }
}
const failures = checks.filter(c => !c.passed);
const result = { passed: failures.length === 0, checkedUtc: new Date().toISOString(), scope: 'Static delivery-catalog and archived-report verification only; no new Unity test or deployment claim.', entries: entries.length, skillConfigurations: entries.filter(e => e.kind === 'skill').length, commonVisuals: entries.filter(e => e.kind === 'common-visual').length, uniqueFiles: files.size, documentLinks: linkCount, uniqueArchivedReports: evidence.size, checks, failures };
fs.writeFileSync(full(output), JSON.stringify(result, null, 2) + '\n');
console.log(JSON.stringify({ passed: result.passed, entries: result.entries, uniqueFiles: files.size, documentLinks: linkCount, uniqueArchivedReports: evidence.size, failures }));
if (!result.passed) process.exitCode = 1;
