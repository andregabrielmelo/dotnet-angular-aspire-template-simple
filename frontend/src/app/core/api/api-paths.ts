/**
 * Base path of the Web API, reached through the dev server proxy (proxy.conf.mjs) or, in
 * production, the host that serves the app; both strip `api/`. Feature services build their
 * URLs from it.
 */
export const API_BASE = 'api';
