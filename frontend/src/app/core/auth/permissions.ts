/**
 * API permissions (Keycloak client roles on apptemplate-api). Mirrors the backend's
 * AppTemplate.UseCases.Authorization.Permission. The UI uses these only to show or hide
 * actions; the API enforces every one of them itself.
 */
export const Permission = {
  UsersRead: 'users:read',
  UsersWrite: 'users:write',
  UsersDelete: 'users:delete',
} as const;

export type Permission = (typeof Permission)[keyof typeof Permission];
