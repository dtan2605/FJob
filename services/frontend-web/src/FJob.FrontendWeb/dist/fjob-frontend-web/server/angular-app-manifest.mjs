
export default {
  bootstrap: () => import('./main.server.mjs').then(m => m.default),
  inlineCriticalCss: false,
  baseHref: '/',
  locale: undefined,
  routes: [
  {
    "renderMode": 0,
    "route": "/"
  },
  {
    "renderMode": 0,
    "route": "/viec-lam"
  },
  {
    "renderMode": 0,
    "route": "/cv"
  },
  {
    "renderMode": 0,
    "route": "/dang-nhap"
  },
  {
    "renderMode": 0,
    "route": "/dang-ky"
  },
  {
    "renderMode": 0,
    "redirectTo": "/",
    "route": "/**"
  }
],
  entryPointToBrowserMapping: undefined,
  assets: {
    'index.csr.html': {size: 613, hash: '235d5d264d87aa7d69057a538a986454c8c7cc7396cc94bae8dcf6f20a41c51b', text: () => import('./assets-chunks/index_csr_html.mjs').then(m => m.default)},
    'index.server.html': {size: 1153, hash: '507f81e520f0d8711a845e2aa0102b3fc9458e87f84d805df92827cb03ed0c2c', text: () => import('./assets-chunks/index_server_html.mjs').then(m => m.default)}
  },
};
