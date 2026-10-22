// Returned by GET /backend-for-frontend/user. The session itself lives in an HTTP-only cookie
// the app can't read - this is all the frontend ever learns about it (no tokens).
export interface BackendForFrontendUser {
  name: string | null;
  email: string | null;
  logoutUrl: string;
}
