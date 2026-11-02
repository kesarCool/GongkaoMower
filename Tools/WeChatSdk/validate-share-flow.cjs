// Focused native boundary tests; run: node Tools/WeChatSdk/validate-share-flow.cjs
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.resolve(__dirname,
    '../../Assets/WX-WASM-SDK-V2/Runtime/wechat-default/unity-sdk/share.js'), 'utf8')
    .replace(/^import .*;\r?\n/gm, '')
    .replace('export default {', 'const share = {') + '\nthis.share = share;';
let passed = 0;
function setup(native) {
    let now = 1000;
    const events = { hide: [], show: [] }, messages = [], calls = [];
    const wx = {
        onHide: callback => events.hide.push(callback),
        onShow: callback => events.show.push(callback),
        showShareMenu() {}, onShareAppMessage(callback) { this.menuShare = callback; },
        shareAppMessage(conf) { calls.push(conf); if (native) native(conf, events); },
    };
    const context = { wx, window: {}, GameGlobal: { Module: { SendMessage(receiver, method, json) {
        assert.equal(receiver, 'WXShareRequestRunner'); assert.equal(method, 'OnShareBridgeState'); messages.push(JSON.parse(json));
    } } }, console: { info() {}, warn() {}, error() {} },
        moduleHelper: { send() {} }, formatJsonStr: JSON.parse, Date: { now: () => now } };
    vm.runInNewContext(source, context);
    return { wx, window: context.window, calls, messages, share: context.share, events,
        request: id => context.share.WXShareAppMessage(JSON.stringify({ title: 'test', query: 'mowerShareRequest=' + id })),
        fire: event => events[event].forEach(callback => callback()), setTime: time => { now = time; },
        states: () => messages.map(message => message.state) };
}
function check(name, fn) { fn(); passed++; console.log('PASS ' + name); }

check('dispatch acknowledgement alone grants no return and includes correlation id', () => {
    const h = setup(); h.request('one');
    assert.deepEqual(h.states(), ['dispatched']); assert.equal(h.messages[0].requestId, 'one');
    assert.match(h.calls[0].query, /mowerShareRequest=one/); assert.ok(h.calls[0].imageUrlId);
    h.fire('show'); assert.deepEqual(h.states(), ['dispatched']);
});
check('hide then show returns once; duplicate lifecycle events cannot re-complete', () => {
    const h = setup(); h.request('one'); h.fire('hide'); h.fire('show'); h.fire('show'); h.fire('hide'); h.fire('show');
    assert.deepEqual(h.states(), ['dispatched', 'hidden', 'returned']);
});
check('native synchronous hide/show preserves dispatch-before-return ordering', () => {
    const h = setup((conf, events) => { events.hide.forEach(f => f()); events.show.forEach(f => f()); });
    h.request('sync'); assert.deepEqual(h.states(), ['hidden', 'dispatched', 'returned']);
});
check('native throw and unavailable API fail without dispatch or return', () => {
    const h = setup(() => { throw new Error('blocked'); }); h.request('throw');
    assert.deepEqual(h.states(), ['failed']); assert.equal(h.messages[0].error, 'blocked');
    const missing = setup(); delete missing.wx.shareAppMessage; missing.request('unsupported');
    assert.deepEqual(missing.states(), ['failed']);
});
check('explicit cancellation/error when exposed prevents later return rewards', () => {
    const h = setup(); h.request('cancel'); h.calls[0].fail({ errMsg: 'shareAppMessage:fail cancel' });
    h.fire('hide'); h.fire('show'); assert.deepEqual(h.states(), ['dispatched', 'cancelled']);
});
check('late native return fails at the same 120 second boundary', () => {
    const h = setup(); h.request('expired'); h.fire('hide'); h.setTime(121000); h.fire('show');
    assert.deepEqual(h.states(), ['dispatched', 'hidden', 'failed']);
});
check('replacement request isolates old native failure callbacks', () => {
    const h = setup(); h.request('old'); h.request('new');
    h.calls[0].fail({ errMsg: 'shareAppMessage:fail cancel' }); h.fire('hide'); h.fire('show');
    assert.deepEqual(h.states(), ['dispatched', 'failed', 'dispatched', 'hidden', 'returned']);
    assert.equal(h.messages.at(-1).requestId, 'new'); assert.equal(h.events.hide.length, 1); assert.equal(h.events.show.length, 1);
});
check('active and menu shares preserve configured image pool', () => {
    const h = setup(); h.window.SetShareImageIds('["configured-image"]');
    assert.equal(h.wx.menuShare().imageUrlId, 'configured-image');
    h.request('configured'); assert.equal(h.calls[0].imageUrlId, 'configured-image');
    // Public image pool setter lives on window and is shared by both entry points.
    const custom = { imageUrlId: 'caller-image', query: '' };
    h.share.WXShareAppMessage(JSON.stringify(custom)); assert.equal(h.calls[1].imageUrlId, 'caller-image');
});
console.log(`${passed} share boundary checks passed. Native UI delivery requires real-device validation.`);
