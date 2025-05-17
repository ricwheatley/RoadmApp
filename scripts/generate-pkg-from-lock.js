#!/usr/bin/env node
/**
 * Generate a bare-bones package.json from package-lock.json
 * Usage: node scripts/generate-pkg-from-lock.js
 */
const fs = require('fs');
const path = require('path');

const lockPath = path.resolve('package-lock.json');
if (!fs.existsSync(lockPath)) {
  console.error('❌  package-lock.json not found in this folder');
  process.exit(1);
}
const lock = JSON.parse(fs.readFileSync(lockPath, 'utf8'));

const pkg = {
  name: lock.name || path.basename(process.cwd()).toLowerCase(),
  version: '1.0.0',
  description: '',
  scripts: {},          // add your build/test scripts later
  dependencies: {},
  devDependencies: {},
};

for (const [name, info] of Object.entries(lock.packages || lock.dependencies)) {
  if (name === '') continue;                       // skip root entry (lockfile v2/v3)
  const version = info.version || info.resolved?.match(/(@[^/]+)$/)?.[1] || 'latest';
  const target = info.dev ? 'devDependencies' : 'dependencies';
  pkg[target][name.replace(/^node_modules\//, '')] = version;
}

fs.writeFileSync('package.json', JSON.stringify(pkg, null, 2) + '\n');
console.log('✅  package.json created – review the file, then run npm install');
