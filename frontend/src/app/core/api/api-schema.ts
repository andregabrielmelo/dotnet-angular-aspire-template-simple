import { components } from './api-types';

/**
 * A response or request shape from the Web API's OpenAPI document, by schema name. The types
 * are generated (npm run api:generate), so a backend contract change surfaces here as a
 * compile error instead of a runtime surprise.
 */
export type ApiSchema<Name extends keyof components['schemas']> = components['schemas'][Name];
