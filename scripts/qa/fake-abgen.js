#!/usr/bin/env node
// Stands in for the abgen sidecar on 127.0.0.1:5147 so the Explorer's
// resident-server reconcile can be driven from the outside.
//
//   node fake-abgen.js adopt     [realm]   the pinned abgen, healthy   -> expect the Explorer to adopt it
//   node fake-abgen.js stale     [realm]   a different build           -> expect it named in the AB panel, left running
//   node fake-abgen.js degraded  [realm]   the pinned abgen, broken    -> expect it named in the AB panel, left running
//   node fake-abgen.js foreign   [realm]   not an abgen at all         -> expect it named in the AB panel, left running
//
// Only "adopt" is used by the Explorer; every other mode must survive the run untouched.
//
// realm defaults to http://127.0.0.1:8000 (the sdk7 preview server).
// It reports its own pid, so the panel's instruction can be checked against a real process.

const http = require('http');

const PIN = '0.17.13';                 // must match PINNED_VERSION in AbgenSidecar.cs
const mode = (process.argv[2] || 'adopt').toLowerCase();
const PORT = Number(process.env.PORT || 5147);   // override only to smoke-test the fixture itself
const realm = (process.argv[3] || 'http://127.0.0.1:8000').replace(/\/+$/, '');

const MODES = {
  stale:    { version: '0.17.11', status: 'ready',    code: 200 },
  degraded: { version: PIN,       status: 'degraded', code: 503 },
  adopt:    { version: PIN,       status: 'ready',    code: 200 },
  foreign:  null,
};

if (!(mode in MODES)) {
  console.error(`unknown mode "${mode}" — use stale | degraded | foreign | adopt`);
  process.exit(2);
}

const cfg = MODES[mode];
const body = cfg && JSON.stringify({
  status: cfg.status,
  version: cfg.version,
  pid: process.pid,
  catalyst_url: `${realm}/content`,
});

const server = http.createServer((req, res) => {
  if (req.method === 'HEAD') return res.writeHead(200).end();

  // "foreign" answers every path the way an unrelated web server would: a body
  // that carries none of the fields identifying an abgen.
  if (!cfg || req.url !== '/health')
    return res.writeHead(cfg ? 404 : 200, { 'Content-Type': 'text/html' }).end('<html>not abgen</html>');

  res.writeHead(cfg.code, { 'Content-Type': 'application/json' }).end(body);
});

server.listen(PORT, '127.0.0.1', () => {
  console.log(`fake abgen [${mode}] pid=${process.pid} on http://127.0.0.1:${PORT}`);
  if (cfg) console.log(`  /health -> ${cfg.code} ${body}`);
  else console.log('  /health -> 200 text/html (not an abgen)');
  console.log('  waiting for the Explorer. Ctrl-C to stop.');
});

server.on('error', (e) => {
  console.error(e.code === 'EADDRINUSE'
    ? `port ${PORT} is already taken — close the other Explorer or abgen first`
    : e.message);
  process.exit(1);
});
