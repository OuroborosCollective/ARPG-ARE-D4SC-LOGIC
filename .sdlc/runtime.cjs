#!/usr/bin/env node
const { spawnSync } = require("node:child_process");
const { join } = require("node:path");
const { pathToFileURL } = require("node:url");
const entry = join(__dirname, "tooling", "node_modules", "codex-sdlc", "dist", "bin.js");
if (process.argv[2] === "restore") {
  const result = spawnSync("npm", ["install", "--ignore-scripts"], { cwd: join(__dirname, "tooling"), stdio: "inherit", shell: false });
  process.exitCode = result.status ?? 1;
} else {
  process.chdir(join(__dirname, ".."));
  import(pathToFileURL(entry).href).catch((error) => {
    console.error("codex-sdlc runtime is unavailable. Run: node .sdlc/runtime.cjs restore");
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  });
}
