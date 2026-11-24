#!/usr/bin/env node
/* Real Chromium execution against the product HTML with synthetic API responses.
 * This is a UI regression harness, not live HTTP, Unity runtime or physical-device evidence.
 * No package installation, active browser profile, Unity action or external request is used.
 */
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');

const args = {};
for (let i = 2; i < process.argv.length; i += 2) {
  if (!process.argv[i].startsWith('--') || process.argv[i + 1] === undefined) throw new Error('Use --key value arguments');
  args[process.argv[i].slice(2)] = process.argv[i + 1];
}
if (!args.html || !args.out || !args.playwright || !args.browser) throw new Error('Required: --html ABSOLUTE_PATH --out ABSOLUTE_DIRECTORY --playwright EXISTING_MODULE_PATH --browser EXISTING_CHROMIUM_PATH');
const htmlPath = path.resolve(args.html), out = path.resolve(args.out);
const htmlBytes = fs.readFileSync(htmlPath), html = htmlBytes.toString('utf8');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const phase = (args.phase || 'green').toUpperCase();
if (!['RED', 'GREEN'].includes(phase)) throw new Error('phase must be red or green');
fs.mkdirSync(out, { recursive: true });
const executablePath = args.browser;
const playwright = args.playwright;
const PROJECT = 'a'.repeat(40), INITIAL_REVISION = 'b'.repeat(64), NEXT_REVISION = 'c'.repeat(64);
const ORIGIN = 'http://art-forge-race.test';
const viewports = [{ width: 1007, height: 601 }, { width: 601, height: 1007 }, { width: 390, height: 844 }];
const scenarios = ['older-409-before-newer-503', 'older-409-after-newer-503', 'older-200-does-not-overwrite-newer-200', 'previous-epoch-409-does-not-clear-new-graph'];
const report = {
  schema: 'ashen.art-forge.browser-revision-races.v1', phase,
  runtimeEvidence: false, evidenceKind: 'real Chromium UI execution with explicitly synthetic API responses; not Unity or live HTTP',
  htmlSha256: hash(htmlBytes), scriptSha256: hash(fs.readFileSync(__filename)),
  browserExecutable: executablePath, startedAt: new Date().toISOString(), results: []
};

function node(id, label, parents) {
  return { id, label, kind: parents.length ? 'mesh' : 'source', category: 'props', parentIds: parents,
    variantGroup: 'Synthetic UI race fixture', artifactSha256: '1'.repeat(64),
    provenance: { provider: 'UI testdouble', sourceRef: 'fixture:source', sourceSha256: '2'.repeat(64), receiptSha256: '3'.repeat(64) },
    license: { identifier: '', evidenceRef: '', state: 'unverified' }, reviewState: 'review-required' };
}
function graph(revision) {
  return { ok: true, readOnly: true, schema: 'ashen.art-graph.v1', projectSha: PROJECT, graphRevision: revision, diagnostics: [],
    nodes: [node('fixture.a', revision === INITIAL_REVISION ? 'Quelle A' : 'Neue Quelle A', []), node('fixture.b', 'Modell B', ['fixture.a'])] };
}
function failure(status) {
  return status === 409 ? { ok: false, code: 'GRAPH_REVISION_CONFLICT', error: 'Synthetic graph revision conflict.' }
    : { ok: false, code: 'EDITOR_BUSY', error: 'Synthetic editor busy response.' };
}

async function runScenario(browser, viewport, scenario) {
  const context = await browser.newContext({ viewport, hasTouch: true, deviceScaleFactor: 1, reducedMotion: 'reduce' });
  context.setDefaultTimeout(6000);
  await context.addInitScript(() => localStorage.setItem('fw.token', 'synthetic-race-test-only'));
  const page = await context.newPage();
  const pending = new Map(), browserErrors = [], routeErrors = [];
  let currentGraph = graph(INITIAL_REVISION);
  const result = { viewport, scenario, passed: false, requests: [], releaseOrder: [], browserErrors, routeErrors, screenshots: [] };
  page.on('pageerror', error => browserErrors.push(error.message));
  await page.route('**/*', async route => {
    try {
      const request = route.request(), url = new URL(request.url());
      if (url.origin === ORIGIN && url.pathname === '/') {
        await route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: html }); return;
      }
      const item = { method: request.method(), path: url.pathname, query: Object.fromEntries(url.searchParams) };
      result.requests.push(item);
      if (url.origin !== ORIGIN || request.method() !== 'GET' || !/^\/api\/x\/art\/(state|graph|asset)$/.test(url.pathname)) {
        item.unexpected = true; await route.abort('blockedbyclient'); return;
      }
      const send = (data, status = 200) => route.fulfill({ status, contentType: 'application/json; charset=utf-8', body: JSON.stringify(data) });
      const endpoint = url.pathname.split('/').pop();
      if (endpoint === 'state') {
        await send({ ok: true, readOnly: true, schema: currentGraph.schema, projectSha: PROJECT,
          graphRevision: currentGraph.graphRevision, nodeCount: currentGraph.nodes.length, diagnostics: [] }); return;
      }
      assert.equal(url.searchParams.get('expectedProjectSha'), PROJECT, 'Every graph/detail read must bind the project revision');
      assert.equal(url.searchParams.get('expectedGraphRevision'), currentGraph.graphRevision, 'Every graph/detail read must bind the current graph revision');
      if (endpoint === 'graph') { await send(currentGraph); return; }
      const id = url.searchParams.get('id');
      assert(currentGraph.nodes.some(n => n.id === id), 'Known synthetic node ID required');
      assert(!pending.has(id), 'Only one held request per node is expected in each isolated scenario');
      pending.set(id, { send, capturedGraph: currentGraph });
    } catch (error) {
      routeErrors.push(error.message);
      try { await route.abort('failed'); } catch { }
    }
  });
  async function loaded(label = 'Quelle A') {
    await page.locator('#artcanvas[aria-busy="false"]').waitFor();
    await page.waitForFunction(expected => [...document.querySelectorAll('#artnodes strong')].some(e => e.textContent === expected), label);
    assert.equal(await page.locator('#artnodes .art-node').count(), 2);
  }
  async function select(label, id) {
    const requestSeen = page.waitForRequest(request => new URL(request.url()).pathname.endsWith('/art/asset') && new URL(request.url()).searchParams.get('id') === id);
    await page.locator('#artnodes button').filter({ has: page.locator('strong', { hasText: new RegExp('^' + label + '$') }) }).click();
    await requestSeen;
    // The route handler is dispatched after the request event; wait for the held route, without timing the product.
    for (let step = 0; !pending.has(id) && step < 100; step++) await new Promise(resolve => setTimeout(resolve, 5));
    assert(pending.has(id), 'The API detail request must remain held before its chosen response is released');
  }
  async function release(id, status) {
    const held = pending.get(id);
    assert(held, 'Expected held request for ' + id);
    result.releaseOrder.push({ id, status });
    const finished = page.waitForResponse(response => new URL(response.url()).pathname.endsWith('/art/asset') && new URL(response.url()).searchParams.get('id') === id);
    const data = status === 200 ? { ok: true, readOnly: true, projectSha: PROJECT, graphRevision: held.capturedGraph.graphRevision,
      node: held.capturedGraph.nodes.find(n => n.id === id) } : failure(status);
    await held.send(data, status);
    pending.delete(id);
    await (await finished).finished();
  }
  async function assertCleared() {
    await page.waitForFunction(() => document.querySelectorAll('#artnodes .art-node').length === 0, undefined, { timeout: 1800 });
    assert((await page.locator('#artstatus').innerText()).includes('geändert'), 'Revision conflict remains visible after later detail responses');
    assert.equal(await page.locator('#artdetail h2').innerText(), 'Asset-Details');
    assert(!(await page.locator('#artdetail').innerText()).includes('1'.repeat(64)), 'No stale artifact hash remains');
  }
  async function snapshot() {
    return page.evaluate(() => ({ nodes: document.querySelectorAll('#artnodes .art-node').length,
      labels: [...document.querySelectorAll('#artnodes strong')].map(e => e.textContent),
      detailTitle: document.querySelector('#artdetail h2')?.textContent,
      status: document.querySelector('#artstatus')?.textContent,
      documentWidth: document.documentElement.scrollWidth, viewportWidth: innerWidth }));
  }
  try {
    await page.goto(ORIGIN + '/?bau&art'); await loaded();
    await select('Quelle A', 'fixture.a');
    if (scenario === 'previous-epoch-409-does-not-clear-new-graph') {
      currentGraph = graph(NEXT_REVISION);
      await page.locator('#artrefresh').click(); await loaded('Neue Quelle A');
      await release('fixture.a', 409);
      await page.waitForFunction(() => document.querySelector('#artdetail h2')?.textContent === 'Asset-Details');
      assert.equal(await page.locator('#artnodes .art-node').count(), 2);
      assert((await page.locator('#artnodes').innerText()).includes('Neue Quelle A'));
      assert(!(await page.locator('#artstatus').innerText()).includes('geändert'));
    } else {
      await select('Modell B', 'fixture.b');
      assert.equal(pending.size, 2, 'A and B requests must genuinely overlap');
      if (scenario === 'older-409-before-newer-503') {
        await release('fixture.a', 409); await release('fixture.b', 503); await assertCleared();
      } else if (scenario === 'older-409-after-newer-503') {
        await release('fixture.b', 503);
        await page.waitForFunction(() => document.querySelector('#artdetail h2')?.textContent === 'Details nicht verfügbar');
        await release('fixture.a', 409); await assertCleared();
      } else {
        await release('fixture.b', 200);
        await page.waitForFunction(() => document.querySelector('#artdetail h2')?.textContent === 'Modell B');
        await release('fixture.a', 200);
        assert.equal(await page.locator('#artdetail h2').innerText(), 'Modell B');
        assert.equal(await page.locator('#artnodes .art-node').count(), 2);
      }
    }
    assert(result.requests.every(r => r.method === 'GET' && !r.unexpected && r.path.startsWith('/api/x/art/')));
    assert.deepEqual(browserErrors, []); assert.deepEqual(routeErrors, []);
    result.observed = await snapshot();
    assert(result.observed.documentWidth <= result.observed.viewportWidth, 'No document-wide horizontal overflow');
    result.passed = true;
  } catch (error) {
    result.error = { message: error.message, stack: String(error.stack).split('\n').slice(0, 4).join('\n') };
    try { result.observed = await snapshot(); } catch { }
  } finally {
    const filename = viewport.width + 'x' + viewport.height + '-' + scenario + (result.passed ? '-PASS' : '-FAIL') + '.png';
    try {
      await page.screenshot({ path: path.join(out, filename) });
      result.screenshots.push({ file: filename, sha256: hash(fs.readFileSync(path.join(out, filename))) });
    } catch (error) { result.screenshotError = error.message; }
    await context.close();
  }
  return result;
}

(async () => {
  let browser;
  try {
    const { chromium } = require(playwright);
    browser = await chromium.launch({ executablePath, headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
    report.browserVersion = browser.version();
    for (const viewport of viewports) for (const scenario of scenarios) report.results.push(await runScenario(browser, viewport, scenario));
  } catch (error) { report.blocker = { message: error.message, stack: String(error.stack).split('\n').slice(0, 4).join('\n') }; }
  finally {
    if (browser) await browser.close();
    report.finishedAt = new Date().toISOString();
    report.htmlUnchanged = hash(fs.readFileSync(htmlPath)) === report.htmlSha256;
    report.passed = !report.blocker && report.htmlUnchanged && report.results.length === viewports.length * scenarios.length && report.results.every(r => r.passed);
    fs.writeFileSync(path.join(out, 'revision-races.json'), JSON.stringify(report, null, 2) + '\n');
    process.stdout.write(JSON.stringify({ phase, passed: report.passed, runtimeEvidence: false, htmlSha256: report.htmlSha256,
      scriptSha256: report.scriptSha256, blocker: report.blocker,
      results: report.results.map(r => ({ viewport: r.viewport, scenario: r.scenario, passed: r.passed, observed: r.observed, error: r.error?.message })) }, null, 2) + '\n');
    process.exitCode = report.passed ? 0 : 1;
  }
})().catch(error => { process.stderr.write(String(error.stack) + '\n'); process.exitCode = 1; });
