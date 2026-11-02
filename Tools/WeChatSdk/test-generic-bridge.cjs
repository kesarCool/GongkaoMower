'use strict';

// No Unity installation or browser is needed: exercise the native ABI against
// a real UTF-8 heap, then run the installed SDK implementation with fake wx APIs.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '../..');
const sdkRoot = path.join(root, 'Assets/WX-WASM-SDK-V2/Runtime/wechat-default/unity-sdk');
const manifest = require('./bridge-signatures.json');
const bridge = fs.readFileSync(path.join(root,
  'Assets/WX-WASM-SDK-V2/Runtime/Plugins/SDK-WX-Generic.jslib'), 'utf8');

function harness() {
  const heap = Buffer.alloc(1024 * 1024);
  const messages = [];
  const errors = [];
  const warnings = [];
  let next = 64;
  const context = vm.createContext({
    Buffer, Uint8Array, Uint32Array, Int32Array, Float32Array, Float64Array,
    ArrayBuffer, setTimeout, clearTimeout,
    console: { error: (...args) => errors.push(args.join(' ')), log() {},
      warn: (...args) => warnings.push(args.join(' ')) },
    LibraryManager: { library: {} },
    mergeInto: (target, source) => Object.assign(target, source),
    _malloc(size) { const p = next; next += size + 8; return p; },
    UTF8ToString(p) { if (!p) return ''; return heap.toString('utf8', p, heap.indexOf(0, p)); },
    lengthBytesUTF8: str => Buffer.byteLength(str, 'utf8'),
    stringToUTF8(str, p, size) {
      const bytes = Buffer.from(str, 'utf8');
      assert.ok(bytes.length + 1 <= size, 'UTF-8 allocation includes the terminator');
      bytes.copy(heap, p);
      heap[p + bytes.length] = 0;
    },
    GameGlobal: { Module: { SendMessage: (...args) => messages.push(args) } },
    window: {}, wx: {}, MODULE_NAME: 'WXSDKManagerHandler'
  });
  vm.runInContext(bridge, context, { filename: 'SDK-WX-Generic.jslib' });
  context.WXBridge = context.LibraryManager.library.$WXBridge;
  function pointer(value) { return context.WXBridge.pointer(value); }
  function call(symbol, ...args) {
    const abi = manifest.signatures.find(item => item.symbol === symbol);
    assert.ok(abi, `ABI exists for ${symbol}`);
    const result = context.LibraryManager.library[symbol](...args.map((arg, index) =>
      abi.parameters[index] === 'System.String' ? pointer(arg) : arg));
    return abi.returns === 'System.String' ? context.UTF8ToString(result) : result;
  }
  return { context, messages, errors, warnings, heap, call, pointer };
}

function loadLibrary(h, filename) {
  const source = fs.readFileSync(path.join(root, 'Assets/WX-WASM-SDK-V2/Runtime/Plugins', filename), 'utf8');
  vm.runInContext(source, h.context, { filename });
  for (const [key, value] of Object.entries(h.context.LibraryManager.library)) {
    if (key.startsWith('$')) h.context[key.slice(1)] = value;
  }
}

function testEverySignature() {
  const h = harness();
  for (const abi of manifest.signatures) {
    let observed;
    const expected = abi.parameters.map((type, index) => {
      if (type === 'System.String') return `汉字🙂-${abi.symbol}-${index}`;
      if (type === 'System.Boolean') return true;
      if (type === 'System.Double') return -27.125 - index;
      if (type === 'WeChatWASM.WXSDKManagerHandler/OnBLECharacteristicValueChangeCallback') return 27183;
      throw new Error(`Unexpected manifest type ${type}`);
    });
    const answer = abi.returns === 'System.String' ? '返回值🙂 {"left":0}' :
      abi.returns === 'System.Boolean' ? true : -108.25;
    h.context.GameGlobal.WXWASMSDK = { [abi.symbol](...args) { observed = args; return answer; } };
    assert.equal(h.context.LibraryManager.library[abi.symbol].length, abi.parameters.length,
      `${abi.symbol}: native parameter count`);
    const result = h.call(abi.symbol, ...expected);
    assert.deepEqual(Array.from(observed), expected, `${abi.symbol}: exact typed argument values`);
    if (abi.returns !== 'System.Void') assert.equal(result,
      abi.returns === 'System.Boolean' ? 1 : answer, `${abi.symbol}: return ABI`);
  }
  // A bool return of zero and a double return of zero must survive marshaling.
  h.context.GameGlobal.WXWASMSDK = {
    WX_SyncFunction_b: () => false,
    WX_SyncFunction_nt: () => 0,
    WX_ClassOneWayFunction(...args) { assert.equal(args[8], false); }
  };
  assert.equal(h.call('WX_SyncFunction_b', 'FalseResult'), 0);
  assert.equal(h.call('WX_SyncFunction_nt', 'ZeroResult', '{}'), 0);
  h.call('WX_ClassOneWayFunction', 'A', 'B', 'id', 'S', 'F', 'C', '{}', 'cb', 0);
  assert.equal(h.errors.length, 0);
}

function loadInstalledSdk(h, variant = 'wechat-default') {
  const variantRoot = path.join(sdkRoot, '../../', variant, 'unity-sdk');
  const run = (file, transform) => vm.runInContext(transform(fs.readFileSync(path.join(variantRoot, file), 'utf8')),
    h.context, { filename: file });
  const withoutExports = source => source.replace(/^import .*;\r?\n/gm, '').replace(/\bexport /g, '');
  run('module-helper.js', source => source.replace(/^import .*;\r?\n/gm, '')
    .replace('export default', 'globalThis.moduleHelper ='));
  run('resType.js', withoutExports);
  run('resTypeOther.js', withoutExports);
  run('utils.js', withoutExports);
  run('sdk.js', source => source.replace(/^import .*;\r?\n/gm, '')
    .replace('export default', 'globalThis.sdk ='));
  run('util.js', source => source.replace(/^import .*;\r?\n/gm, '')
    .replace('export default', 'globalThis.sdkUtil ='));
  const indexSource = fs.readFileSync(path.join(variantRoot, 'index.js'), 'utf8');
  const initMethod = indexSource.match(/WXInitializeSDK\(\)\s*\{[\s\S]*?\n\s*\}/)[0];
  vm.runInContext(`Object.assign(sdk, sdkUtil, { ${initMethod} });`, h.context);
  h.context.GameGlobal.WXWASMSDK = h.context.sdk;
}

function testInstalledSdk() {
  const h = harness();
  loadInstalledSdk(h);
  const wx = h.context.wx;
  h.call('WXInitializeSDK', 'ignored-native-config');
  assert.deepEqual(h.messages.at(-1), ['WXSDKManagerHandler', 'Inited', 200]);
  let fps;
  wx.setPreferredFramesPerSecond = value => { fps = value; };
  h.call('WX_SetPreferredFramesPerSecond', 59.5);
  assert.equal(fps, 59.5);
  wx.getMenuButtonBoundingClientRect = () => ({ left: 271.5, top: 20, width: 80, height: 32 });
  const rect = JSON.parse(h.call('WX_SyncFunction_t', 'GetMenuButtonBoundingClientRect', 'ClientRect'));
  assert.equal(rect.left, 271.5);
  assert.equal(rect.width, 80);
  wx.canIUse = value => value === '键盘🙂';
  assert.equal(h.call('WX_SyncFunction_bs', 'CanIUse', '键盘🙂'), 1);
  assert.equal(h.call('WX_SyncFunction_bs', 'CanIUse', 'missing'), 0);
  wx.getText = value => `返回:${value}`;
  assert.equal(h.call('WX_SyncFunction_ss', 'GetText', '汉字🙂'), '返回:汉字🙂');
  wx.getNumber = options => options.value;
  assert.equal(h.call('WX_SyncFunction_nt', 'GetNumber', '{"value":-3.125}'), -3.125);
  let interval;
  wx.setInterval = (...args) => { interval = args; };
  h.call('WX_OneWayNoFunction_vnns', 'SetInterval', 17.25, 99.5, '回调');
  assert.deepEqual(Array.from(interval), [17.25, 99.5, '回调']);
  wx.getPosition = (x, y) => ({ left: x, top: y });
  assert.deepEqual(JSON.parse(h.call('WX_SyncFunction_tnn', 'GetPosition', 'NoSchema', 0, 17.5)),
    { left: 0, top: 17.5 });

  // Pass real SDK callbacks back through the manager's expected JSON envelope.
  wx.login = options => {
    assert.equal(options.timeout, 1000);
    options.success({ code: '登录🙂' });
    options.complete({ errMsg: 'login:ok' });
  };
  h.call('WX_OneWayFunction', 'Login', 'LoginSuccessCallbackResult', 'GeneralCallbackResult',
    'GeneralCallbackResult', '{"timeout":1000}', 'request-中文');
  const login = h.messages.filter(([, method]) => method === 'LoginCallback').map(([, , payload]) => JSON.parse(payload));
  assert.deepEqual(login.map(item => item.type), ['success', 'complete']);
  assert.equal(login[0].callbackId, 'request-中文');
  assert.equal(JSON.parse(login[0].res).code, '登录🙂');

  let listener;
  let removed;
  wx.onShow = callback => { listener = callback; };
  wx.offShow = callback => { removed = callback; };
  h.call('WX_OnEventRegister', 'OnShow', 'NoSchema');
  listener({ query: { stage: '章节一' }, scene: 1001 });
  const show = h.messages.find(([, method]) => method === '_OnShowCallback');
  assert.equal(JSON.parse(show[2]).scene, 1001);
  h.call('WX_OffEventRegister', 'OnShow');
  assert.equal(removed, listener);

  // The share entry must use share.js, which owns image selection and ACKs.
  let sharePayload;
  h.context.GameGlobal.WXWASMSDK.WXShareAppMessage = conf => { sharePayload = JSON.parse(conf); };
  wx.shareAppMessage = () => { throw new Error('generic share route bypassed share.js'); };
  h.call('WX_OneWayNoFunction_vt', 'ShareAppMessage', '{"title":"一起挑战🙂","query":"mowerShareRequest=123"}');
  assert.equal(sharePayload.title, '一起挑战🙂');
  let keyboard;
  wx.showKeyboard = conf => { keyboard = conf; };
  h.call('WX_OneWayNoFunction_vt', 'ShowKeyboard', '{"maxLength":12,"confirmHold":false}');
  assert.equal(keyboard.maxLength, 12);
  assert.equal(keyboard.confirmHold, false);
  assert.equal(h.errors.length, 0);
}

function testClassEventTeardown(variant) {
  const h = harness();
  loadInstalledSdk(h, variant);
  const ordinary = new Set();
  const named = new Set();
  let ordinarySubscriptions = 0;
  const object = {
    onState(callback) { ordinary.add(callback); ordinarySubscriptions++; },
    offState(callback) { ordinary.delete(callback); },
    on(event, callback) { assert.equal(event, 'ready'); named.add(callback); },
    off(event, callback) { assert.equal(event, 'ready'); named.delete(callback); }
  };
  h.context.wx.createTestObject = () => object;
  const id = h.call('WX_ClassFunction', 'CreateTestObject', 'WXTestObject', '{}');
  assert.ok(id.length > 0);
  // Off before On and repeated Off are both safe and never subscribe listeners.
  h.call('WX_ClassOffEventFunction', 'WXTestObject', 'OffState', id, '');
  h.call('WX_ClassOnEventFunction', 'WXTestObject', 'OnState', 'NoSchema', id, '');
  assert.equal(ordinary.size, 1);
  for (const callback of ordinary) callback({ state: 'ready' });
  assert.equal(h.messages.filter(([, method]) => method === '_WXTestObjectOnStateCallback').length, 1);
  h.call('WX_ClassOffEventFunction', 'WXTestObject', 'OffState', id, '');
  h.call('WX_ClassOffEventFunction', 'WXTestObject', 'OffState', id, '');
  assert.equal(ordinary.size, 0, `${variant}: class Off removes its exact callback`);
  assert.equal(ordinarySubscriptions, 1, `${variant}: Off never calls On`);
  const pageId = h.call('WX_ClassFunction', 'CreateTestObject', 'WXPageManager', '{}');
  h.call('WX_ClassOffEventFunction', 'WXPageManager', 'Off', pageId, 'ready');
  h.call('WX_ClassOnEventFunction', 'WXPageManager', 'On', 'NoSchema', pageId, 'ready');
  assert.equal(named.size, 1);
  h.call('WX_ClassOffEventFunction', 'WXPageManager', 'Off', pageId, 'ready');
  h.call('WX_ClassOffEventFunction', 'WXPageManager', 'Off', pageId, 'ready');
  assert.equal(named.size, 0, `${variant}: named class event teardown`);
  assert.equal(h.errors.length, 0);
}

async function testPromiseCallbacks() {
  const h = harness();
  loadInstalledSdk(h);
  h.context.wx.createTask = () => ({
    run: () => Promise.resolve({ count: 5 }),
    reject: () => Promise.reject({ errMsg: 'request rejected' })
  });
  const id = h.call('WX_ClassFunction', 'CreateTask', 'WXTestTask', '{}');
  h.call('WX_ClassOneWayFunction', 'WXTestTask', 'Run', id, 'NoSchema', 'NoSchema',
    'NoSchema', '{}', 'promise-success', 1);
  h.call('WX_ClassOneWayFunction', 'WXTestTask', 'Reject', id, 'NoSchema', 'NoSchema',
    'NoSchema', '{}', 'promise-fail', 1);
  await new Promise(setImmediate);
  const callbacks = method => h.messages.filter(([, value]) => value === method)
    .map(([, , value]) => JSON.parse(value));
  const success = callbacks('_WXTestTaskRunCallback');
  assert.deepEqual(success.map(item => item.type), ['success', 'complete']);
  assert.equal(success[0].id, id);
  assert.equal(success[0].callbackId, 'promise-success');
  assert.equal(JSON.parse(success[0].res).count, 5);
  const failure = callbacks('_WXTestTaskRejectCallback');
  assert.deepEqual(failure.map(item => item.type), ['fail', 'complete']);
  assert.equal(JSON.parse(failure[0].res).errMsg, 'request rejected');
  assert.equal(h.errors.length, 0);
}

function testFailures() {
  const h = harness();
  loadInstalledSdk(h);
  // Native capability failures must not escape into Wasm and abort startup.
  h.context.wx.getMenuButtonBoundingClientRect = () => { throw new Error('capsule unavailable'); };
  assert.equal(h.call('WX_SyncFunction_t', 'GetMenuButtonBoundingClientRect', 'ClientRect'), '{}');
  h.call('WX_OneWayFunction', 'UnsupportedApi', 'S', 'F', 'C', '{}', 'failed-request');
  const failures = h.messages.filter(([, method]) => method === 'UnsupportedApiCallback').map(([, , payload]) => JSON.parse(payload));
  assert.deepEqual(failures.map(item => item.type), ['fail', 'complete']);
  assert.equal(failures[0].callbackId, 'failed-request');
  assert.match(JSON.parse(failures[0].res).errMsg, /^SDK bridge:fail /);
  assert.equal(h.messages.some(([, , payload]) => typeof payload === 'string' && payload.includes('"type":"success"')), false);
  delete h.context.GameGlobal.WXWASMSDK.WXInitializeSDK;
  h.call('WXInitializeSDK', 'ignored-native-config');
  assert.deepEqual(h.messages.at(-1), ['WXSDKManagerHandler', 'Inited', 500]);
  h.context.GameGlobal.WXWASMSDK = null;
  h.context.window.WXWASMSDK = { WX_SyncFunction_nt: () => 19.75 };
  assert.equal(h.call('WX_SyncFunction_nt', 'Fallback', '{}'), 19.75);
  assert.ok(h.errors.length >= 3, 'all failures emit actionable SDK bridge diagnostics');
}

function testAdStyleNumericAbi() {
  const h = harness();
  const adBridge = fs.readFileSync(path.join(root,
    'Assets/WX-WASM-SDK-V2/Runtime/Plugins/SDK-WX-Bridge-Fix.jslib'), 'utf8');
  vm.runInContext(adBridge, h.context, { filename: 'SDK-WX-Bridge-Fix.jslib' });
  let result;
  h.context.window.WXWASMSDK = {
    WXADGetStyleValue(id, key) {
      assert.equal(id, 'ad-汉字');
      assert.equal(key, 'top');
      return result;
    }
  };
  for (const [input, expected] of [[0, 0], [27, 27], [-1, -1], [undefined, -1],
    ['17', -1], [Infinity, -1], [NaN, -1]]) {
    result = input;
    assert.equal(h.context.LibraryManager.library.WXADGetStyleValue(h.pointer('ad-汉字'), h.pointer('top')),
      expected, `ad style ABI: ${String(input)}`);
  }
}

function testNativeStartupDependencies() {
  const h = harness();
  loadInstalledSdk(h);
  const sdk = h.context.GameGlobal.WXWASMSDK;
  for (const file of ['touch/index.js', 'TCPSocket/index.js', 'UDPSocket/index.js',
    'bluetooth/index.js', 'gyroscope/index.js']) {
    const source = fs.readFileSync(path.join(sdkRoot, file), 'utf8')
      .replace(/^import .*;\r?\n/gm, '').replace('export default', 'return');
    const part = vm.runInContext(`(function() { ${source} })()`, h.context, { filename: file });
    Object.assign(sdk, part);
  }
  h.context.window.WXWASMSDK = sdk;
  h.context.Module = h.context.GameGlobal.Module;
  for (const file of ['SDK-WX-Bridge-Fix.jslib', 'Touch.jslib', 'TCPSocket.jslib',
    'UDPSocket.jslib', 'Gyroscope.jslib', 'WxGameDataMonitor.jslib']) loadLibrary(h, file);
  const registered = new Map();
  for (const name of manifest.initializationDependencies) {
    assert.equal(typeof h.context.LibraryManager.library[name], 'function', `${name}: actual native startup import exists`);
    if (!name.startsWith('WX_Register')) continue;
    const original = sdk[name];
    assert.equal(typeof original, 'function', `${name}: installed SDK implementation exists`);
    sdk[name] = callback => { registered.set(name, callback); original(callback); };
  }
  // Order and imports come from the actual DLL InitSDK/get_Instance/Y call graph.
  h.context.LibraryManager.library.SetUnityUIType(h.pointer('unity_no_ui'));
  assert.equal(h.call('WXIsCloudTest'), 0);
  h.context.GameGlobal.isTest = true;
  assert.equal(h.call('WXIsCloudTest'), 1);
  h.context.GameGlobal.isTest = false;
  let address = 7000;
  for (const name of manifest.initializationDependencies.filter(name => name.startsWith('WX_Register'))) {
    h.context.LibraryManager.library[name](++address);
    assert.equal(registered.get(name), address, `${name}: delegate function pointer stays numeric`);
  }
  assert.equal(registered.size, 10);
  h.call('WXInitializeSDK', '2021.3.22f1c1');
  h.context.LibraryManager.library.JSStartGameDataMonitor();
  assert.deepEqual(h.messages.at(-1), ['WXSDKManagerHandler', 'Inited', 200]);

  // Verify the new BLE register reaches the real SDK and returns the same
  // callback pointer through its dynCall, with heap-backed strings and bytes.
  Object.assign(h.context.GameGlobal.Module, {
    _malloc: h.context._malloc, _free() {}, HEAPU8: h.heap,
    lengthBytesUTF8: h.context.lengthBytesUTF8, stringToUTF8: h.context.stringToUTF8
  });
  let listener;
  let callbackResult;
  h.context.wx.onBLECharacteristicValueChange = callback => { listener = callback; };
  h.context.GameGlobal.Module.dynCall_viiiii = (callback, device, service, characteristic, value, length) => {
    callbackResult = { callback, device: h.context.UTF8ToString(device), service: h.context.UTF8ToString(service),
      characteristic: h.context.UTF8ToString(characteristic), bytes: [...h.heap.subarray(value, value + length)] };
  };
  sdk.WX_OnBLECharacteristicValueChange();
  listener({ deviceId: '设备🙂', serviceId: 'service-1', characteristicId: 'char-1',
    value: new Uint8Array([1, 2, 255]).buffer });
  assert.deepEqual(callbackResult, {
    callback: registered.get('WX_RegisterOnBLECharacteristicValueChangeCallback'),
    device: '设备🙂', service: 'service-1', characteristic: 'char-1', bytes: [1, 2, 255]
  });
  assert.equal(h.errors.length, 0);
}

function testOptionalTelemetry() {
  const metrics = ['WXGetEXFrameTime', 'WXGetTotalMemorySize', 'WXGetDynamicMemorySize',
    'WXGetUsedMemorySize', 'WXGetUnAllocatedMemorySize', 'WXGetBundleNumberInMemory',
    'WXGetBundleNumberOnDisk', 'WXGetBundleSizeInMemory', 'WXGetBundleSizeOnDisk'];
  const h = harness();
  h.context.Module = { IsWxGame: true };
  let starts = 0;
  const reports = [];
  h.context.GameGlobal.manager = { getGameDataMonitor: () => ({
    start() { starts++; }, reportUnityProfileData(value) { reports.push(value); }
  }) };
  loadLibrary(h, 'WxGameDataMonitor.jslib');
  let abortCalls = 0;
  h.context.abort = () => { abortCalls++; throw new Error('abort called'); };
  for (const name of metrics) h.context[`_${name}`] = () => 10;
  h.context._WXGetTotalMemorySize = vm.runInContext('(function() { console.error("missing function: WXGetTotalMemorySize"); abort(-1); })', h.context);
  for (let i = 0; i < 2; i++) {
    h.context.LibraryManager.library.JSStartGameDataMonitor();
    h.context.LibraryManager.library.JSReportUnityProfileData(60, 1, 2, 3, 4, 5, 6, 7, 8, 9);
  }
  assert.equal(starts, 0);
  assert.equal(reports.length, 0);
  assert.equal(abortCalls, 0, 'optional telemetry never invokes missing-function stubs');
  assert.equal(h.warnings.length, 1, 'one diagnostic covers repeated optional telemetry calls');
  h.context._WXGetTotalMemorySize = () => 4096;
  h.context.LibraryManager.library.JSStartGameDataMonitor();
  h.context.LibraryManager.library.JSReportUnityProfileData(60, 1, 2, 3, 4, 5, 6, 7, 8, 9);
  assert.equal(starts, 1);
  assert.equal(reports.length, 1);
  assert.equal(reports[0].fps.targetFrameRate, 60);
  assert.equal(reports[0].webassembly.totalHeapMemory, 4096);
  assert.equal(reports[0].render.trianglesCount, 9);
  delete h.context.GameGlobal.manager;
  assert.doesNotThrow(() => h.context.LibraryManager.library.JSStartGameDataMonitor());
  assert.doesNotThrow(() => h.context.LibraryManager.library.JSReportUnityProfileData());
}

async function main() {
  testEverySignature();
  testInstalledSdk();
  testClassEventTeardown('wechat-default');
  testClassEventTeardown('playable-default');
  await testPromiseCallbacks();
  testFailures();
  testAdStyleNumericAbi();
  testNativeStartupDependencies();
  testOptionalTelemetry();
  console.log(`PASS: ${manifest.signatures.length} typed SDK bridges; UTF-8 heap, installed SDK, JSON, share route, events, callback failures.`);
}
main().catch(error => { console.error(error); process.exitCode = 1; });
