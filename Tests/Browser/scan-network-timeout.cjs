#!/usr/bin/env node
/* Browser-only tests of the Art Forge 202 transport contract.
 * API replies and tickets are explicit testdoubles. No Unity, live HTTP, auth or device evidence is claimed.
 */
'use strict';
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto'), assert = require('node:assert/strict');
const args = {};
for (let i = 2; i < process.argv.length; i += 2) {
  if (!process.argv[i].startsWith('--') || process.argv[i + 1] === undefined) throw new Error('Use --key value arguments');
  args[process.argv[i].slice(2)] = process.argv[i + 1];
}
if (!args.html || !args.out || !args.playwright || !args.browser) throw new Error('Required: --html ABSOLUTE_PATH --out ABSOLUTE_DIRECTORY --playwright EXISTING_MODULE_PATH --browser EXISTING_CHROMIUM_PATH');
const htmlPath = path.resolve(args.html), out = path.resolve(args.out), bytes = fs.readFileSync(htmlPath), html = bytes.toString('utf8');
const sha = value => crypto.createHash('sha256').update(value).digest('hex');
const suite = args.suite || 'time-limit', phase = (args.phase || 'green').toUpperCase();
assert(['protocol', 'poll-limit', 'time-limit'].includes(suite)); assert(['RED', 'GREEN'].includes(phase));
fs.mkdirSync(out, { recursive: true });
const modulePath = args.playwright;
const executablePath = args.browser;
const ORIGIN = 'http://art-forge-scan.test', PROJECT = 'a'.repeat(40), REVISION = 'b'.repeat(64), NEXT = 'c'.repeat(64);
const tablet = { width: 1007, height: 601 };
const cases = suite === 'protocol' ? [
  ...[{ width: 1007, height: 601 }, { width: 601, height: 1007 }, { width: 390, height: 844 }].map(viewport => ({ name: 'pending-state-graph-asset', viewport })),
  ...['invalid-ticket-does-not-poll', 'other-202-code-does-not-poll', 'pending-code-on-200-does-not-poll',
    'changed-ticket-is-rejected', 'expired-ticket-is-terminal', 'mismatched-ticket-is-terminal',
    'asset-503-is-terminal', 'reload-stops-old-epoch-polls', 'poll-401-preserves-login', 'retry-delay-is-clamped'].map(name => ({ name, viewport: tablet }))
] : [{ name: suite, viewport: tablet }];
const report = { schema: 'ashen.art-forge.browser-scan-polling.v1', phase, suite, runtimeEvidence: false,
  evidenceKind: 'real Chromium execution; synthetic scan poll deliberately receives no HTTP response, testing network abort; no live Unity or device proof',
  networkResponseHeld: true,
  htmlSha256: sha(bytes), scriptSha256: sha(fs.readFileSync(__filename)), startedAt: new Date().toISOString(), results: [] };

function node(id, label, parents) {
  return { id, label, kind: parents.length ? 'mesh' : 'source', category: 'props', parentIds: parents, variantGroup: 'Synthetic polling fixture',
    artifactSha256: '1'.repeat(64), provenance: { provider: 'UI testdouble', sourceRef: 'fixture:source', sourceSha256: '2'.repeat(64), receiptSha256: '3'.repeat(64) },
    license: { state: 'unverified', identifier: '', evidenceRef: '' }, reviewState: 'review-required' };
}
function graph(revision = REVISION) {
  return { ok: true, readOnly: true, schema: 'ashen.art-graph.v1', projectSha: PROJECT, graphRevision: revision, diagnostics: [],
    nodes: [node('fixture.a', revision === NEXT ? 'Neue Quelle A' : 'Quelle A', []), node('fixture.b', 'Modell B', ['fixture.a'])] };
}
function terminal(endpoint, entry) {
  const g = entry.graph;
  if (endpoint === 'state') return { ok: true, readOnly: true, schema: g.schema, projectSha: PROJECT, graphRevision: g.graphRevision, nodeCount: g.nodes.length, diagnostics: [] };
  if (endpoint === 'graph') return g;
  return { ok: true, readOnly: true, projectSha: PROJECT, graphRevision: g.graphRevision, node: g.nodes.find(n => n.id === entry.query.id) };
}

async function runCase(browser, test) {
  const context = await browser.newContext({ viewport: test.viewport, hasTouch: true, reducedMotion: 'reduce', deviceScaleFactor: 1 });
  context.setDefaultTimeout(6000);
  await context.addInitScript(() => localStorage.setItem('fw.token', 'synthetic-scan-test-only'));
  const page = await context.newPage(), tickets = new Map(), routeErrors = [], browserErrors = [];
  let serial = 0, activeGraph = graph();
  const result = { ...test, passed: false, requests: [], routeErrors, browserErrors, screenshots: [], failedRequests: [] };
  page.on('pageerror', e => browserErrors.push(e.message));
  page.on('requestfailed', r => result.failedRequests.push({ path: new URL(r.url()).pathname, failure: r.failure()?.errorText }));
  await page.route('**/*', async route => {
    try {
      const request = route.request(), url = new URL(request.url());
      if (url.origin === ORIGIN && url.pathname === '/') { await route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: html }); return; }
      const query = Object.fromEntries(url.searchParams), item = { method: request.method(), path: url.pathname, query, at: Date.now() };
      result.requests.push(item);
      assert.equal(url.origin, ORIGIN); assert.equal(request.method(), 'GET'); assert.equal(request.postData(), null);
      assert(/^\/api\/x\/art\/(state|graph|asset)$/.test(url.pathname), 'Only existing Art routes may be called');
      const endpoint = url.pathname.split('/').pop(), scanId = query.scanId;
      delete query.scanId;
      // Keep an immutable trace including scanId; query is kept separately for ticket matching.
      item.query = Object.fromEntries(url.searchParams);
      const send = (body, status = 200) => route.fulfill({ status, contentType: 'application/json; charset=utf-8', body: JSON.stringify(body) });
      const pending = (id, delay = 200) => ({ ok: false, readOnly: true, code: 'ART_SCAN_PENDING', scanId: id, retryAfterMs: delay, error: 'Synthetic scan is pending.' });
      if (!scanId) {
        if (endpoint !== 'state') {
          assert.equal(query.expectedProjectSha, PROJECT); assert.equal(query.expectedGraphRevision, activeGraph.graphRevision);
        }
        if ((test.name === 'reload-stops-old-epoch-polls' || test.name === 'retry-delay-is-clamped') && endpoint !== 'asset' && endpoint !== 'state') {
          await send(terminal(endpoint, { graph: activeGraph, query })); return;
        }
        if (test.name === 'reload-stops-old-epoch-polls' && endpoint === 'state') {
          await send(terminal(endpoint, { graph: activeGraph, query })); return;
        }
        const id = (++serial).toString(16).padStart(32, '0');
        const entry = { endpoint, path: url.pathname, query, graph: activeGraph, polls: 0, createdAt: Date.now() };
        tickets.set(id, entry);
        if (test.name === 'invalid-ticket-does-not-poll') { await send(pending('INVALID_TICKET'), 202); return; }
        if (test.name === 'other-202-code-does-not-poll') { await send({ ...pending(id), code: 'UNRELATED_PENDING' }, 202); return; }
        if (test.name === 'pending-code-on-200-does-not-poll') { await send(pending(id), 200); return; }
        await send(pending(id, test.name === 'retry-delay-is-clamped' ? -50 : test.name === 'poll-limit' ? 100 : test.name === 'time-limit' ? 1000 : 200), 202); return;
      }
      assert(/^[0-9a-f]{32}$/.test(scanId), 'Opaque ticket format must be preserved');
      const entry = tickets.get(scanId); assert(entry, 'Only a previously issued, unconsumed ticket can be polled');
      assert.equal(url.pathname, entry.path, 'Poll route cannot change'); assert.deepEqual(query, entry.query, 'All original query fields must remain identical');
      entry.polls++;
      if (test.name === 'changed-ticket-is-rejected') { await send(pending('f'.repeat(32)), 202); return; }
      if (test.name === 'expired-ticket-is-terminal') { tickets.delete(scanId); await send({ ok: false, code: 'ART_SCAN_EXPIRED', error: 'Synthetic scan expired.' }, 410); return; }
      if (test.name === 'mismatched-ticket-is-terminal') { tickets.delete(scanId); await send({ ok: false, code: 'ART_SCAN_MISMATCH', error: 'Synthetic scan query mismatch.' }, 400); return; }
      if (test.name === 'poll-401-preserves-login') { tickets.delete(scanId); await send({ ok: false, error: 'Synthetic unauthorized response.' }, 401); return; }
      if (test.name === 'asset-503-is-terminal' && endpoint === 'asset') { tickets.delete(scanId); await send({ ok: false, code: 'PROVENANCE_UNAVAILABLE', error: 'Synthetic provenance unavailable.' }, 503); return; }
      if (test.name === 'reload-stops-old-epoch-polls' && endpoint === 'asset') { await send(pending(scanId), 202); return; }
      if (test.name === 'time-limit') return; // Deliberately held synthetic poll: the real browser must abort it.
      if (test.name === 'poll-limit') { await send(pending(scanId, 100), 202); return; }
      if (entry.polls === 1) { await send(pending(scanId, test.name === 'retry-delay-is-clamped' ? 5000 : 200), 202); return; }
      tickets.delete(scanId); await send(terminal(endpoint, entry));
    } catch (error) {
      routeErrors.push(error.message);
      try { await route.abort('failed'); } catch { }
    }
  });
  async function settled(timeout = 6000) {
    await page.waitForFunction(() => document.querySelector('#artcanvas')?.getAttribute('aria-busy') === 'false', undefined, { timeout });
  }
  async function loaded(label = 'Quelle A') {
    await page.waitForFunction(value => [...document.querySelectorAll('#artnodes strong')].some(n => n.textContent === value), label);
    await settled(); assert.equal(await page.locator('#artnodes .art-node').count(), 2);
  }
  async function pick() { await page.locator('#artnodes button').filter({ has: page.locator('strong', { hasText: /^Modell B$/ }) }).click(); }
  async function waitForCount(predicate, timeout = 6000) {
    const deadline = Date.now() + timeout;
    while (!predicate() && Date.now() < deadline) await new Promise(resolve => setTimeout(resolve, 10));
    assert(predicate(), 'Expected synthetic request sequence was not observed');
  }
  try {
    await page.goto(ORIGIN + '/?bau&art');
    if (test.name === 'pending-state-graph-asset') {
      await loaded(); await pick();
      await page.waitForFunction(() => document.querySelector('#artdetail h2')?.textContent === 'Modell B');
      for (const endpoint of ['state', 'graph', 'asset']) {
        const calls = result.requests.filter(r => r.path.endsWith('/' + endpoint));
        assert.equal(calls.length, 3, endpoint + ' must issue one request and two polls');
        assert(!calls[0].query.scanId); assert(calls[1].query.scanId); assert.equal(calls[1].query.scanId, calls[2].query.scanId);
      }
      assert.equal(await page.locator('#artnodes .art-node').count(), 2);
    } else if (test.name === 'asset-503-is-terminal') {
      await loaded(); await pick();
      await page.waitForFunction(() => document.querySelector('#artdetail h2')?.textContent === 'Details nicht verfügbar');
      assert((await page.locator('#artstatus').innerText()).includes('unbelegt'));
      assert.equal(result.requests.filter(r => r.path.endsWith('/asset')).length, 2);
      assert.equal(await page.locator('#artnodes .art-node').count(), 2);
    } else if (test.name === 'reload-stops-old-epoch-polls') {
      await loaded(); await pick();
      await waitForCount(() => result.requests.filter(r => r.path.endsWith('/asset') && r.query.scanId).length >= 1);
      activeGraph = graph(NEXT); await page.locator('#artrefresh').click(); await loaded('Neue Quelle A');
      const count = result.requests.filter(r => r.path.endsWith('/asset')).length;
      await page.waitForTimeout(550);
      assert.equal(result.requests.filter(r => r.path.endsWith('/asset')).length, count, 'Reload must stop polls belonging to its previous epoch');
      assert.equal(await page.locator('#artnodes .art-node').count(), 2);
    } else if (test.name === 'retry-delay-is-clamped') {
      await loaded();
      const calls = result.requests.filter(r => r.path.endsWith('/state'));
      assert.equal(calls.length, 3);
      const gaps = [calls[1].at - calls[0].at, calls[2].at - calls[1].at];
      result.retryGapsMs = gaps;
      assert(gaps[0] >= 90 && gaps[0] < 1500, 'Negative delay must clamp to approximately 100 ms');
      assert(gaps[1] >= 900 && gaps[1] < 2200, 'Huge delay must clamp to approximately 1000 ms');
    } else if (test.name === 'poll-limit' || test.name === 'time-limit') {
      await settled(test.name === 'poll-limit' ? 26000 : 47000);
      const calls = result.requests.filter(r => r.path.endsWith('/state'));
      result.elapsedMs = Date.now() - calls[0].at;
      assert((await page.locator('#artstatus').innerText()).includes('dauert zu lange'), 'Bounded polling must report its timeout');
      assert.equal(await page.locator('#artnodes .art-node').count(), 0);
      if (test.name === 'poll-limit') {
        assert.equal(calls.length, 151, 'Only the first request plus at most 150 polls may be sent');
        assert(result.elapsedMs >= 14000 && result.elapsedMs < 26000);
      } else {
        assert(result.elapsedMs >= 39000 && result.elapsedMs < 47000, 'The actual wall-time budget must be approximately 40 seconds');
        assert.equal(calls.length, 2, 'The hanging poll must not trigger any replacement scan or retry');
        await waitForCount(() => result.failedRequests.length > 0);
        assert(result.failedRequests.some(r => /ABORTED/.test(r.failure || '')), 'The actual fetch must be aborted by the browser');
      }
    } else if (test.name === 'poll-401-preserves-login') {
      await page.locator('#login').waitFor({ state: 'visible' }); await settled();
      assert.equal(result.requests.length, 2); assert.equal(await page.locator('#artnodes .art-node').count(), 0);
    } else {
      await settled(); assert.equal(await page.locator('#artnodes .art-node').count(), 0);
      const expected = ['invalid-ticket-does-not-poll', 'other-202-code-does-not-poll', 'pending-code-on-200-does-not-poll'].includes(test.name) ? 1 : 2;
      assert.equal(result.requests.length, expected, 'Invalid or terminal responses must not start another scan');
      if (test.name === 'expired-ticket-is-terminal') assert((await page.locator('#artstatus').innerText()).includes('Synthetic scan expired.'));
      if (test.name === 'mismatched-ticket-is-terminal') assert((await page.locator('#artstatus').innerText()).includes('Synthetic scan query mismatch.'));
    }
    assert.deepEqual(routeErrors, []); assert.deepEqual(browserErrors, []);
    result.observed = await page.evaluate(() => ({ nodes: document.querySelectorAll('#artnodes .art-node').length,
      title: document.querySelector('#artdetail h2')?.textContent, status: document.querySelector('#artstatus')?.textContent,
      documentWidth: document.documentElement.scrollWidth, viewportWidth: innerWidth }));
    assert(result.observed.documentWidth <= result.observed.viewportWidth);
    result.passed = true;
  } catch (error) {
    result.error = { message: error.message, stack: String(error.stack).split('\n').slice(0, 4).join('\n') };
    try { result.observed = await page.evaluate(() => ({ nodes: document.querySelectorAll('#artnodes .art-node').length, status: document.querySelector('#artstatus')?.textContent })); } catch { }
  } finally {
    const filename = test.viewport.width + 'x' + test.viewport.height + '-' + test.name + (result.passed ? '-PASS' : '-FAIL') + '.png';
    try { await page.screenshot({ path: path.join(out, filename) }); result.screenshots.push({ file: filename, sha256: sha(fs.readFileSync(path.join(out, filename))) }); } catch (error) { result.screenshotError = error.message; }
    await context.close();
  }
  return result;
}

(async () => {
  let browser;
  try {
    const { chromium } = require(modulePath);
    browser = await chromium.launch({ executablePath, headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
    report.browserVersion = browser.version();
    for (const test of cases) report.results.push(await runCase(browser, test));
  } catch (error) { report.blocker = { message: error.message }; }
  finally {
    if (browser) await browser.close();
    report.finishedAt = new Date().toISOString(); report.htmlUnchanged = sha(fs.readFileSync(htmlPath)) === report.htmlSha256;
    report.passed = !report.blocker && report.htmlUnchanged && report.results.length === cases.length && report.results.every(r => r.passed);
    fs.writeFileSync(path.join(out, 'scan-polling.json'), JSON.stringify(report, null, 2) + '\n');
    process.stdout.write(JSON.stringify({ phase, suite, passed: report.passed, runtimeEvidence: false, htmlSha256: report.htmlSha256,
      scriptSha256: report.scriptSha256, blocker: report.blocker,
      results: report.results.map(r => ({ name: r.name, viewport: r.viewport, passed: r.passed, requests: r.requests.length,
        elapsedMs: r.elapsedMs, retryGapsMs: r.retryGapsMs, observed: r.observed, error: r.error?.message })) }, null, 2) + '\n');
    process.exitCode = report.passed ? 0 : 1;
  }
})().catch(error => { process.stderr.write(String(error.stack) + '\n'); process.exitCode = 1; });
