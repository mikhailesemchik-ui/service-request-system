import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { ProblemDetails } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import {
  availableStatusTransitions,
  canAssignToSelf,
  canManageAssignment,
  canRemoveOwnAssignment,
} from '../requests-status-transitions';
import {
  RequestAssignee,
  RequestDetails,
  RequestHistoryItem,
  RequestStatus,
} from '../models/request.models';
import { RequestApiService } from '../services/request-api.service';

const NOT_FOUND_MESSAGE = 'This request does not exist or is not available to you.';
const LOAD_ERROR_FALLBACK = 'Unable to load this request. Please try again.';
const HISTORY_ERROR_FALLBACK = 'Unable to load history. Please try again.';
const ASSIGNEES_ERROR_FALLBACK = 'Unable to load eligible assignees. Please try again.';
const ASSIGNMENT_ERROR_FALLBACK = 'Unable to update the assignment. Please try again.';
const STATUS_ERROR_FALLBACK = 'Unable to update the status. Please try again.';
const SERVICE_UNAVAILABLE_MESSAGE = 'Unable to reach the server. Please check your connection and try again.';
const PERMISSION_DENIED_MESSAGE = 'You do not have permission to perform this action.';
const INVALID_ID_MESSAGE = 'This request could not be found.';

const STATUS_LABELS: Record<RequestStatus, string> = {
  New: 'New',
  InProgress: 'Start progress',
  WaitingForUser: 'Wait for user',
  Resolved: 'Resolve',
  Closed: 'Close request',
  Cancelled: 'Cancel request',
};

const CONFIRMATION_REQUIRED_STATUSES: readonly RequestStatus[] = ['Cancelled', 'Closed'];

@Component({
  selector: 'app-request-details-page',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './request-details.page.html',
  styleUrl: './request-details.page.scss',
})
export class RequestDetailsPageComponent {
  private readonly requestApi = inject(RequestApiService);
  private readonly authService = inject(AuthService);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly statusLabels = STATUS_LABELS;

  protected readonly request = signal<RequestDetails | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  protected readonly history = signal<RequestHistoryItem[] | null>(null);
  protected readonly isHistoryLoading = signal(false);
  protected readonly historyError = signal<string | null>(null);

  protected readonly assignees = signal<RequestAssignee[] | null>(null);
  protected readonly assigneesError = signal<string | null>(null);
  protected readonly selectedAssigneeId = signal<number | ''>('');

  protected readonly isAssignmentSaving = signal(false);
  protected readonly assignmentError = signal<string | null>(null);

  protected readonly isStatusSaving = signal(false);
  protected readonly statusError = signal<string | null>(null);
  protected readonly confirmingStatus = signal<RequestStatus | null>(null);

  protected readonly currentUserId = computed(() => this.authService.currentUser()?.id ?? null);
  protected readonly currentUserRole = computed(() => this.authService.currentUser()?.role ?? null);

  protected readonly canManageAssignment = computed(() => {
    const role = this.currentUserRole();
    return role !== null && canManageAssignment(role);
  });

  protected readonly isAdmin = computed(() => this.currentUserRole() === 'Admin');

  protected readonly canAssignToSelf = computed(() => {
    const role = this.currentUserRole();
    const request = this.request();
    return role !== null && request !== null && canAssignToSelf(role, request.assignedTo?.id ?? null);
  });

  protected readonly canRemoveOwnAssignment = computed(() => {
    const role = this.currentUserRole();
    const request = this.request();
    const userId = this.currentUserId();
    return (
      role !== null &&
      request !== null &&
      userId !== null &&
      canRemoveOwnAssignment(role, request.assignedTo?.id ?? null, userId)
    );
  });

  protected readonly statusOptions = computed<RequestStatus[]>(() => {
    const request = this.request();
    const role = this.currentUserRole();
    const userId = this.currentUserId();

    if (!request || !role) {
      return [];
    }

    return availableStatusTransitions({
      currentStatus: request.status,
      role,
      isAssignedToCurrentUser: request.assignedTo?.id === userId,
    });
  });

  constructor() {
    this.loadRequest();
  }

  protected retryLoad(): void {
    this.loadRequest();
  }

  protected retryHistory(): void {
    this.loadHistory();
  }

  protected retryAssignees(): void {
    this.loadEligibleAssignees();
  }

  protected backToList(): void {
    void this.router.navigate(['/requests']);
  }

  protected assignToSelf(): void {
    const userId = this.currentUserId();
    if (userId !== null) {
      this.submitAssignment(userId);
    }
  }

  protected removeOwnAssignment(): void {
    this.submitAssignment(null);
  }

  protected removeAssignment(): void {
    this.submitAssignment(null);
  }

  protected onAssigneeSelectChange(value: string): void {
    const parsed = Number(value);
    this.selectedAssigneeId.set(parsed > 0 ? parsed : '');
  }

  protected assignSelected(): void {
    const assigneeId = this.selectedAssigneeId();
    if (assigneeId !== '') {
      this.submitAssignment(assigneeId);
    }
  }

  protected requestStatusChange(status: RequestStatus): void {
    if (this.isStatusSaving()) {
      return;
    }

    if (CONFIRMATION_REQUIRED_STATUSES.includes(status)) {
      if (this.confirmingStatus() === status) {
        this.confirmingStatus.set(null);
        this.submitStatusChange(status);
        return;
      }

      this.confirmingStatus.set(status);
      return;
    }

    this.submitStatusChange(status);
  }

  protected cancelStatusConfirmation(): void {
    this.confirmingStatus.set(null);
  }

  private submitAssignment(assignedToUserId: number | null): void {
    const request = this.request();
    if (!request || this.isAssignmentSaving() || this.isStatusSaving()) {
      return;
    }

    this.isAssignmentSaving.set(true);
    this.assignmentError.set(null);

    this.requestApi
      .setAssignment(request.id, { assignedToUserId })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.isAssignmentSaving.set(false);
          this.request.set(updated);
          this.selectedAssigneeId.set('');
          this.loadHistory();
        },
        error: (error: unknown) => {
          this.isAssignmentSaving.set(false);
          this.assignmentError.set(this.resolveErrorMessage(error, ASSIGNMENT_ERROR_FALLBACK));
        },
      });
  }

  private submitStatusChange(status: RequestStatus): void {
    const request = this.request();
    if (!request || this.isStatusSaving() || this.isAssignmentSaving()) {
      return;
    }

    this.isStatusSaving.set(true);
    this.statusError.set(null);

    this.requestApi
      .changeStatus(request.id, { status })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.isStatusSaving.set(false);
          this.request.set(updated);
          this.loadHistory();
        },
        error: (error: unknown) => {
          this.isStatusSaving.set(false);
          this.statusError.set(this.resolveErrorMessage(error, STATUS_ERROR_FALLBACK));
        },
      });
  }

  private loadRequest(): void {
    const rawId = this.activatedRoute.snapshot.paramMap.get('requestId');
    const requestId = Number(rawId);

    if (!rawId || !Number.isInteger(requestId) || requestId <= 0) {
      this.isLoading.set(false);
      this.loadError.set(INVALID_ID_MESSAGE);
      return;
    }

    this.isLoading.set(true);
    this.loadError.set(null);

    this.requestApi
      .getRequest(requestId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (request) => {
          this.isLoading.set(false);
          this.request.set(request);
          this.loadHistory();
          this.loadEligibleAssignees();
        },
        error: (error: unknown) => {
          this.isLoading.set(false);
          this.loadError.set(this.resolveErrorMessage(error, LOAD_ERROR_FALLBACK));
        },
      });
  }

  private loadHistory(): void {
    const request = this.request();
    if (!request) {
      return;
    }

    this.isHistoryLoading.set(true);
    this.historyError.set(null);

    this.requestApi
      .getHistory(request.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (history) => {
          this.isHistoryLoading.set(false);
          this.history.set(history);
        },
        error: (error: unknown) => {
          this.isHistoryLoading.set(false);
          this.historyError.set(this.resolveErrorMessage(error, HISTORY_ERROR_FALLBACK));
        },
      });
  }

  private loadEligibleAssignees(): void {
    if (this.currentUserRole() !== 'Admin') {
      return;
    }

    this.assigneesError.set(null);

    this.requestApi
      .getEligibleAssignees()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (assignees) => this.assignees.set(assignees),
        error: (error: unknown) => this.assigneesError.set(this.resolveErrorMessage(error, ASSIGNEES_ERROR_FALLBACK)),
      });
  }

  protected historyDescription(entry: RequestHistoryItem): string {
    if (entry.action === 'StatusChanged') {
      return `Status changed from ${entry.previousDisplayValue} to ${entry.newDisplayValue}`;
    }

    if (entry.action === 'AssignmentChanged') {
      if (entry.previousDisplayValue && entry.newDisplayValue) {
        return `Reassigned from ${entry.previousDisplayValue} to ${entry.newDisplayValue}`;
      }

      if (entry.newDisplayValue) {
        return `Assigned to ${entry.newDisplayValue}`;
      }

      return 'Assignment removed';
    }

    return entry.action;
  }

  private resolveErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return SERVICE_UNAVAILABLE_MESSAGE;
      }

      if (error.status === 403) {
        return PERMISSION_DENIED_MESSAGE;
      }

      if (error.status === 404) {
        return NOT_FOUND_MESSAGE;
      }

      const problemDetails = error.error as ProblemDetails | null;

      if (problemDetails?.detail) {
        return problemDetails.detail;
      }
    }

    return fallback;
  }
}
