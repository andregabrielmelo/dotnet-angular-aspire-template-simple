// The Angular dev server is the browser's only origin: it proxies /api to the Web API, so the
// app has no CORS and its cookies stay first-party. Both legs are HTTPS (ADR 016): Aspire passes
// the API's HTTPS address in services__web__https__0 (WithReference in AppHost.cs) and makes Node
// trust the ASP.NET Core development certificate; without Aspire, the API's launchSettings port
// is used, and the certificate must be trusted (dotnet dev-certs https --trust). In production,
// put the built app and /api behind one host.
const target = process.env['services__web__https__0'] ?? 'https://localhost:7243';

export default {
  '/api': {
    target,
    // Verify the API's certificate: the development certificate is trusted.
    secure: true,
    changeOrigin: true,
    // The API trusts loopback as a proxy in Development, so rate limits see the browser's address.
    xfwd: true,
    pathRewrite: { '^/api': '' },
  },
};
