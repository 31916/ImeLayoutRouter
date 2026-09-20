import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
const types = { 'app.ico': 'image/vnd.microsoft.icon', 'index.html': 'text/html; charset=utf-8', 'en.html': 'text/html; charset=utf-8', 'preview.html': 'text/html; charset=utf-8', 'style.css': 'text/css; charset=utf-8', 'preview.css': 'text/css; charset=utf-8', 'preview.js': 'text/javascript; charset=utf-8' };
types['social-ja-v1.png'] = 'image/png';
types['social-en-v1.png'] = 'image/png';
types['social-ja-v2.png'] = 'image/png';
types['social-en-v2.png'] = 'image/png';
createServer(async (request, response) => {
  const path = new URL(request.url, 'http://127.0.0.1').pathname;
  const name = path === '/' ? 'index.html' : path === '/en' ? 'en.html' : path.slice(1);
  if (!Object.hasOwn(types, name)) { response.writeHead(404); response.end('Not found'); return; }
  try {
    const source = name === 'app.ico' ? '../Assets/app.ico' : '../site/' + name;
    const content = await readFile(new URL(source, import.meta.url));
    response.writeHead(200, { 'Content-Type': types[name], 'Cache-Control': 'no-store' });
    response.end(content);
  } catch { response.writeHead(500); response.end('Preview unavailable'); }
}).listen(4173, '127.0.0.1', () => console.log('Preview: http://127.0.0.1:4173/preview.html'));
