// The Angular dev server is the browser's only origin: it proxies /api to the Web API, so the
// app has no CORS and its refresh cookie stays first-party. Aspire passes the API's address in
// services__web__http__0 (WithReference in AppHost.cs); without Aspire, the API's own
// launchSettings port is used. In production, put the built app and /api behind one host.
const target =
  process.env['services__web__http__0'] ??
  process.env['services__web__https__0'] ??
  'http://localhost:5234';

export default {
  '/api': {
    target,
    secure: false,
    changeOrigin: true,
    // The API's own rate limits and forwarded headers see the browser's address.
    xfwd: true,
    pathRewrite: { '^/api': '' },
  },
};
