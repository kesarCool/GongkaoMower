'use strict';

// Executes scheduling functions extracted from the actual exported framework.
// It validates the JS mapping and replay behavior, not live Unity/device FPS.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../..');
const args = process.argv.slice(2);
if (args.includes('--help')) {
  console.log('node Tools/Performance/validate-frame-scheduling.cjs [--framework <exported framework.js>] [--expect-fixed] [--output <report.json>]');
  process.exit(0);
}
let framework = path.join(root, 'Build/minigame/minigame/webgl.wasm.framework.unityweb.js');
let expectFixed = false;
let outputPath;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--expect-fixed') { expectFixed = true; continue; }
  if (args[i] === '--output' && args[i + 1]) { outputPath = path.resolve(args[++i]); continue; }
  if (args[i] !== '--framework' || !args[i + 1]) throw new Error(`Unsupported argument: ${args[i]}`);
  framework = path.resolve(args[++i]);
}
const source = fs.readFileSync(framework, 'utf8');
const fixed = source.includes('/* WX logical frame timing */');
if (expectFixed) assert(fixed, 'The exported scheduler must contain the logical timing repair');

function endOfFunction(open) {
  let depth = 0;
  let quote = null;
  let comment = null;
  for (let i = open; i < source.length; i++) {
    const c = source[i], next = source[i + 1];
    if (comment === 'line') { if (c === '\n') comment = null; continue; }
    if (comment === 'block') { if (c === '*' && next === '/') { comment = null; i++; } continue; }
    if (quote) {
      if (c === '\\') i++;
      else if (c === quote) quote = null;
      continue;
    }
    if (c === '/' && next === '/') { comment = 'line'; i++; continue; }
    if (c === '/' && next === '*') { comment = 'block'; i++; continue; }
    if (c === '"' || c === "'" || c === '`') { quote = c; continue; }
    if (c === '{') depth++;
    else if (c === '}' && --depth === 0) return i + 1;
  }
  throw new Error('Unterminated scheduling function in framework.');
}

function declaration(name) {
  const match = new RegExp(`function\\s+${name}\\s*\\([^)]*\\)\\s*\\{`).exec(source);
  if (!match) throw new Error(`Named function ${name} is unavailable; use an unminified exported framework.`);
  return source.slice(match.index, endOfFunction(source.indexOf('{', match.index)));
}

function mainLoopMember(name) {
  const browser = /(?:var|let|const)\s+Browser\s*=/.exec(source);
  if (!browser) throw new Error('Browser main-loop declaration is unavailable.');
  const tail = source.slice(browser.index);
  const match = new RegExp(`\\b${name}\\s*:\\s*function\\s*\\([^)]*\\)\\s*\\{`).exec(tail);
  if (!match) throw new Error(`Browser.mainLoop.${name} is unavailable.`);
  const start = browser.index + match.index + match[0].indexOf('function');
  return source.slice(start, endOfFunction(source.indexOf('{', start)));
}

const setter = declaration('_emscripten_set_main_loop_timing');
const setup = declaration('setMainLoop');
const pause = mainLoopMember('pause');
const resume = mainLoopMember('resume');

function harness() {
  const writes = [];
  const schedules = [];
  const mainLoop = {
    func: null, arg: 0, running: false, currentlyRunningMainloop: 0,
    currentFrameNumber: 0, queue: [], remainingBlockers: 0, updateStatus() {},
    runIter(callback) { callback(); }
  };
  const context = vm.createContext({
    console: { ...console, info() {} }, Date, Math,
    Browser: { mainLoop, requestAnimationFrame: callback => schedules.push(callback) },
    wx: { setPreferredFramesPerSecond: fps => writes.push(fps) },
    GameGlobal: { unityNamespace: { isLoopRunnerEnable: true }, manager: { isVisible: true } },
    Module: {}, ABORT: false, ENVIRONMENT_IS_WORKER: false,
    assert: (condition, message) => assert.ok(condition, message),
    err: message => { throw new Error(message); }, warnOnce() {}, maybeExit() {}, checkStackCookie() {},
    _emscripten_get_now: () => 0, performance: { now: () => 0 },
    GL: { newRenderingFrameStarted() {} },
    setTimeout: callback => schedules.push(callback), setImmediate: callback => schedules.push(callback)
  });
  vm.runInContext(`${setter}\n${setup}\nBrowser.mainLoop.pause = ${pause};\nBrowser.mainLoop.resume = ${resume};`, context);
  context.setMainLoop(() => {}, 0, false);
  assert.deepEqual(writes, [60], 'actual initial RAF loop requests 60 FPS');
  return { context, writes, schedules, mainLoop };
}

const old = harness();
old.context.wx.setPreferredFramesPerSecond(30);
old.context._emscripten_set_main_loop_timing(1, 1);
assert.deepEqual(old.writes, [60, 30, 60], 'Unity scheduling can overwrite an independent wx 30 request');

function mapping(mode, value, expected = 30) {
  const h = harness();
  h.writes.length = 0;
  h.context._emscripten_set_main_loop_timing(mode, value);
  assert.deepEqual(h.writes, [expected], `actual framework maps (${mode},${value}) to wx ${expected}`);
  assert.equal(h.mainLoop.method, 'rAF');
  assert.equal(h.mainLoop.timingValue, 1, 'WeChat controls FPS; JS must not skip a second time');
  return { requested: { mode, value }, wxWrites: h.writes.slice(),
    stored: { mode: h.mainLoop.timingMode, value: h.mainLoop.timingValue, method: h.mainLoop.method } };
}
const raf30 = mapping(1, 2);
const timeout33 = mapping(0, 33);

const restored = harness();
restored.writes.length = 0;
restored.context._emscripten_set_main_loop_timing(1, 2);
restored.mainLoop.pause();
restored.mainLoop.resume();
assert.deepEqual(restored.writes, fixed ? [30, 30] : [30, 60], 'resume must replay the logical rate after repair');
assert.equal(restored.mainLoop.method, 'rAF');
const additionalMappings = [];
if (fixed) {
  for (const [mode, value, fps] of [[1,1,60],[1,3,20],[1,4,15],[0,16,60],[0,66,20]])
    additionalMappings.push(mapping(mode, value, fps));
  for (const [mode,value] of [[1,2],[0,33]]) {
    const loop = harness(); loop.context._emscripten_set_main_loop_timing(mode, value);
    loop.writes.length = 0;
    for (let i=0;i<5;i++) { loop.mainLoop.pause(); loop.mainLoop.resume(); }
    assert.deepEqual(loop.writes, [30,30,30,30,30]);
    assert.equal(loop.mainLoop.timingValue, 1);
  }
}

const report = {
  status: 'passed',
  fixed,
  framework: path.relative(root, framework).replace(/\\/g, '/'),
  sha256: crypto.createHash('sha256').update(source).digest('hex'),
  scope: 'Actual exported JavaScript scheduler mapping with stubbed host APIs; no Unity engine or device was run.',
  independentWxRequestOverwrite: old.writes,
  raf30, timeout33,
  additionalMappings,
  pauseResumeReplay: { wxWrites: restored.writes, storedValue: restored.mainLoop.timingValue },
  limitations: [
    'Does not prove which timing calls Unity makes, or their real startup/onShow order.',
    'Does not prove the final Unity Update rate, native RAF frequency, cloud FPS, CPU cost, or device performance.',
    'Confirm the native target-frame-rate setter is included in the new player, then record runtime scheduling calls and wall-clock frame counts.'
  ]
};
const directory = outputPath ? path.dirname(outputPath) : path.join(root, 'Library/IosPerformance20261028');
fs.mkdirSync(directory, { recursive: true });
const output = outputPath || path.join(directory, fixed ? 'frame-scheduling-validation-fixed.json' : 'frame-scheduling-validation.json');
fs.writeFileSync(output, JSON.stringify(report, null, 2));
console.log(JSON.stringify({ status: report.status, output: path.relative(root, output),
  overwrite: old.writes, mapping30: [raf30.wxWrites, timeout33.wxWrites], resume: restored.writes,
  scope: 'JS mapping only; live Unity/device FPS is unverified.' }));
