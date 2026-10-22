// Fails when the built index.html contains something the backend for frontend's
// Content-Security-Policy (script-src 'self', no 'unsafe-inline') would block: an inline
// <script> or an inline event handler such as onload="...". Angular's critical CSS inlining
// adds exactly such a handler, which is why it's turned off in angular.json.
import { readFileSync } from 'node:fs';

const path = process.argv[2] ?? 'dist/frontend/browser/index.html';
const html = readFileSync(path, 'utf8');
const problems = [];

for (const match of html.matchAll(/<script\b([^>]*)>/gi)) {
  if (!/\bsrc\s*=/i.test(match[1])) {
    problems.push(`inline script: ${match[0]}`);
  }
}
for (const match of html.matchAll(/<[^>]*\s(on[a-z]+)\s*=[^>]*>/gi)) {
  problems.push(`inline ${match[1]} handler: ${match[0]}`);
}

if (problems.length > 0) {
  console.error(`${path} would be blocked by the Content-Security-Policy:`);
  for (const problem of problems) console.error(`  ${problem}`);
  process.exit(1);
}
console.log(`${path} has no inline scripts or event handlers.`);
