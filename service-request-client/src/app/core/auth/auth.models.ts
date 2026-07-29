export const USER_ROLES = ['Employee', 'SupportAgent', 'Admin'] as const;

export type UserRole = (typeof USER_ROLES)[number];

export function isUserRole(value: unknown): value is UserRole {
  return typeof value === 'string' && (USER_ROLES as readonly string[]).includes(value);
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthenticatedUser {
  id: number;
  username: string;
  displayName: string;
  email: string;
  role: UserRole;
}

export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  user: AuthenticatedUser;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export const AUTH_LOGIN_PATH = '/api/auth/login';
export const AUTH_ME_PATH = '/api/auth/me';
