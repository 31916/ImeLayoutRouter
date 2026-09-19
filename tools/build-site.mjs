import { mkdir, copyFile, appendFile, readdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
const root = new URL('../', import.meta.url);
const review = process.argv.includes('--review');
const output = new URL(review ? '.site-review-dist/' : '.site-dist/', root);
await mkdir(output, { recursive: true });
// Explicit allowlist: draft controls and unpublished packages are not deployed.
const files = ['index.html', 'style.css', '_headers'];
if (review) files.push('preview.html', 'preview.css', 'preview.js');
for (const existing of await readdir(output)) {
  if (!files.includes(existing)) throw new Error('Unexpected file in deployment output: ' + existing);
}
for (const name of files) {
  await copyFile(new URL('site/' + name, root), new URL(name, output));
}
if (review) await appendFile(new URL('_headers', output), '\n/*\n  X-Robots-Tag: noindex, nofollow\n');
console.log('Static site ready: ' + fileURLToPath(output));
