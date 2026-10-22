import { ApiSchema } from '../../core/api/api-schema';

// Shapes returned by the Web API's /users endpoints, generated from its OpenAPI document.
export type UserRow = ApiSchema<'UserRecord'>;
export type UserPage = ApiSchema<'PagedResultOfUserRecord'>;
export type UpdateUserRequest = ApiSchema<'UpdateUserRequest'>;
export type UpdateUserResponse = ApiSchema<'UpdateUserResponse'>;
