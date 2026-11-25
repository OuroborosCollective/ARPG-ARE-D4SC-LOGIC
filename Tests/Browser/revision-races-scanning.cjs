#!/usr/bin/env node
// Run the same revision-race assertions with ticket-bound synthetic 202 responses.
const runRaces = require('./revision-races.cjs');
runRaces(true).catch(error => { console.error(error); process.exitCode = 1; });
