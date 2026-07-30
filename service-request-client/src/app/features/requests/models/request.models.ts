export const REQUEST_STATUSES = ['New', 'InProgress', 'WaitingForUser', 'Resolved', 'Closed', 'Cancelled'] as const;

export type RequestStatus = (typeof REQUEST_STATUSES)[number];

export const REQUEST_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const;

export type RequestPriority = (typeof REQUEST_PRIORITIES)[number];

export interface RequestCategorySummary {
  id: number;
  name: string;
}

export interface RequestUserSummary {
  id: number;
  displayName: string;
}

export interface RequestListItem {
  id: number;
  title: string;
  status: RequestStatus;
  priority: RequestPriority;
  category: RequestCategorySummary;
  createdBy: RequestUserSummary;
  assignedTo: RequestUserSummary | null;
  createdAt: string;
  updatedAt: string;
}

export interface RequestDetails {
  id: number;
  title: string;
  description: string;
  status: RequestStatus;
  priority: RequestPriority;
  category: RequestCategorySummary;
  createdBy: RequestUserSummary;
  assignedTo: RequestUserSummary | null;
  createdAt: string;
  updatedAt: string;
  resolvedAt: string | null;
  closedAt: string | null;
  cancelledAt: string | null;
}

export interface CreateRequestPayload {
  title: string;
  description: string;
  categoryId: number;
  priority: RequestPriority;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface RequestListQuery {
  page?: number;
  pageSize?: number;
  status?: RequestStatus;
  priority?: RequestPriority;
  categoryId?: number;
}

export const REQUESTS_PATH = '/api/requests';

export const DEFAULT_PAGE_SIZE = 20;
