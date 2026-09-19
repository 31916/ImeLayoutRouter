import {createServer} from 'node:http';
import {readFile} from 'node:fs/promises';
const content = await readFile(new URL('../tests/browser-input.html', import.meta.url));
createServer((req,res) => {
  if (req.url !== '/') {res.writeHead(404);res.end();return;}
  res.writeHead(200, {'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store',
    'Content-Security-Policy':"default-src 'none'; style-src 'unsafe-inline'; form-action 'none'"});
  res.end(content);
}).listen(4174,'127.0.0.1',()=>console.log('Input test: http://127.0.0.1:4174/'));
