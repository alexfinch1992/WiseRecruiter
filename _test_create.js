// Quick test: login as admin and create a job
const http = require('http');
const { URL } = require('url');

const BASE = 'http://localhost:5236';
let cookies = '';

function request(method, path, body, headers = {}) {
  return new Promise((resolve, reject) => {
    const url = new URL(path, BASE);
    const opts = {
      hostname: url.hostname,
      port: url.port,
      path: url.pathname + url.search,
      method,
      headers: {
        ...headers,
        'Cookie': cookies,
      },
    };
    if (body) {
      opts.headers['Content-Type'] = 'application/x-www-form-urlencoded';
      opts.headers['Content-Length'] = Buffer.byteLength(body);
    }
    const req = http.request(opts, (res) => {
      let data = '';
      res.on('data', (chunk) => data += chunk);
      res.on('end', () => {
        // Collect cookies
        const sc = res.headers['set-cookie'];
        if (sc) {
          sc.forEach(c => {
            const name = c.split('=')[0];
            const val = c.split(';')[0];
            cookies = cookies.split('; ').filter(x => x && !x.startsWith(name + '=')).concat([val]).join('; ');
          });
        }
        resolve({ status: res.statusCode, headers: res.headers, body: data });
      });
    });
    req.on('error', reject);
    if (body) req.write(body);
    req.end();
  });
}

async function main() {
  // 1. GET login page to get antiforgery token
  console.log('Getting login page...');
  let r = await request('GET', '/Account/Login');
  let match = r.body.match(/name="__RequestVerificationToken".*?value="([^"]+)"/);
  if (!match) { console.log('No token found on login page'); return; }
  const loginToken = match[1];

  // 2. POST login
  console.log('Logging in...');
  r = await request('POST', '/Account/Login', 
    `username=admin%40wiserecruiter.com&password=Password123!&__RequestVerificationToken=${encodeURIComponent(loginToken)}`,
    { 'Content-Type': 'application/x-www-form-urlencoded' }
  );
  console.log('Login status:', r.status, 'Location:', r.headers.location);

  // Follow redirect if needed
  if (r.status === 302) {
    r = await request('GET', r.headers.location || '/');
  }

  // 3. GET Create page
  console.log('Getting Create page...');
  r = await request('GET', '/Admin/Create');
  if (r.status === 302) {
    console.log('Redirected to:', r.headers.location);
    r = await request('GET', r.headers.location);
  }
  match = r.body.match(/name="__RequestVerificationToken".*?value="([^"]+)"/);
  if (!match) { console.log('No token found on create page, status:', r.status); return; }
  const createToken = match[1];

  // 4. POST Create with just a title
  console.log('Creating job...');
  r = await request('POST', '/Admin/Create',
    `Title=Test+Debug+Job&Description=Test+description&__RequestVerificationToken=${encodeURIComponent(createToken)}`,
    { 'Content-Type': 'application/x-www-form-urlencoded' }
  );
  console.log('Create status:', r.status, 'Location:', r.headers.location);
  if (r.status === 200) {
    // Check for validation errors in body
    const errors = r.body.match(/validation-summary-errors[\s\S]*?<\/div>/);
    if (errors) console.log('Validation errors found');
  }
  console.log('Done!');
}

main().catch(console.error);
