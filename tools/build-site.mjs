import { mkdir, copyFile, appendFile, readdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
const root = new URL('../', import.meta.url);
const review = process.argv.includes('--review');
const output = new URL(review ? '.site-review-dist/' : '.site-dist/', root);
await mkdir(output, { recursive: true });
// Explicit allowlist: draft controls and unpublished packages are not deployed.
const files = ['index.html', 'en.html', 'style.css', '_headers', 'app.ico', 'sitemap.xml', 'social-ja-v1.png', 'social-en-v1.png', 'social-ja-v2.png', 'social-en-v2.png'];
if (review) files.push('preview.html', 'preview.css', 'preview.js');
for (const existing of await readdir(output)) {
  if (!files.includes(existing)) throw new Error('Unexpected file in deployment output: ' + existing);
}
for (const name of files) {
  const source = name === 'app.ico' ? 'Assets/app.ico' : 'site/' + name;
  await copyFile(new URL(source, root), new URL(name, output));
}
if (review) await appendFile(new URL('_headers', output), '  X-Robots-Tag: noindex, nofollow\n');
console.log('Static site ready: ' + fileURLToPath(output));
