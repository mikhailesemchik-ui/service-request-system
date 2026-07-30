import { UserRole } from '../../core/auth/auth.models';
import { RequestStatus } from './models/request.models';

/**
 * UI-only mirror of the backend's transition policy (RequestStatusTransitions) and role
 * permissions, used solely to decide which action buttons to show. The backend independently
 * enforces every rule; this never substitutes for that enforcement.
 */
const ALLOWED_TRANSITIONS: Record<RequestStatus, readonly RequestStatus[]> = {
  New: ['InProgress', 'Cancelled'],
  InProgress: ['WaitingForUser', 'Resolved', 'Cancelled'],
  WaitingForUser: ['InProgress', 'Resolved', 'Cancelled'],
  Resolved: ['InProgress', 'Closed'],
  Closed: [],
  Cancelled: [],
};

const EMPLOYEE_ALLOWED_TARGETS: readonly RequestStatus[] = ['Cancelled', 'Closed'];
const SUPPORT_AGENT_ALLOWED_TARGETS: readonly RequestStatus[] = ['InProgress', 'WaitingForUser', 'Resolved', 'Cancelled'];
const ADMIN_ALLOWED_TARGETS: readonly RequestStatus[] = ['InProgress', 'WaitingForUser', 'Resolved', 'Closed', 'Cancelled'];

export function canTransition(from: RequestStatus, to: RequestStatus): boolean {
  return ALLOWED_TRANSITIONS[from].includes(to);
}

export interface AvailableStatusTransitionsParams {
  currentStatus: RequestStatus;
  role: UserRole;
  isAssignedToCurrentUser: boolean;
}

export function availableStatusTransitions(params: AvailableStatusTransitionsParams): RequestStatus[] {
  const { currentStatus, role, isAssignedToCurrentUser } = params;

  if (role === 'Employee') {
    return EMPLOYEE_ALLOWED_TARGETS.filter((target) => canTransition(currentStatus, target));
  }

  if (role === 'SupportAgent') {
    if (!isAssignedToCurrentUser) {
      return [];
    }

    return SUPPORT_AGENT_ALLOWED_TARGETS.filter((target) => canTransition(currentStatus, target));
  }

  return ADMIN_ALLOWED_TARGETS.filter((target) => canTransition(currentStatus, target));
}

export function canManageAssignment(role: UserRole): boolean {
  return role === 'SupportAgent' || role === 'Admin';
}

export function canAssignToSelf(role: UserRole, assignedToId: number | null): boolean {
  return role === 'SupportAgent' && assignedToId === null;
}

export function canRemoveOwnAssignment(role: UserRole, assignedToId: number | null, currentUserId: number): boolean {
  return role === 'SupportAgent' && assignedToId === currentUserId;
}
