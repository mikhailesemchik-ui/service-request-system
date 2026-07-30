import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { ProblemDetails } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { Category } from '../../categories/models/category.models';
import { CategoryApiService } from '../../categories/services/category-api.service';
import { RequestTableComponent } from '../components/request-table.component';
import {
  PagedResult,
  REQUEST_PRIORITIES,
  REQUEST_STATUSES,
  RequestListItem,
  RequestPriority,
  RequestStatus,
} from '../models/request.models';
import { RequestApiService } from '../services/request-api.service';

const LOAD_ERROR_FALLBACK = 'Unable to load requests. Please try again.';
const PERMISSION_DENIED_MESSAGE = 'You do not have permission to view this content.';
const SERVICE_UNAVAILABLE_MESSAGE = 'Unable to reach the server. Please check your connection and try again.';

@Component({
  selector: 'app-requests-list-page',
  standalone: true,
  imports: [RequestTableComponent],
  templateUrl: './requests-list.page.html',
  styleUrl: './requests-list.page.scss',
})
export class RequestsListPageComponent {
  private readonly requestApi = inject(RequestApiService);
  private readonly categoryApi = inject(CategoryApiService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  private isFetching = false;

  protected readonly statuses = REQUEST_STATUSES;
  protected readonly priorities = REQUEST_PRIORITIES;

  protected readonly isStaff = computed(() => this.authService.hasAnyRole(['SupportAgent', 'Admin']));
  protected readonly heading = computed(() => (this.isStaff() ? 'All requests' : 'My requests'));

  protected readonly categories = signal<Category[]>([]);

  protected readonly result = signal<PagedResult<RequestListItem> | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly loadError = signal<string | null>(null);

  protected readonly page = signal(1);
  protected readonly status = signal<RequestStatus | ''>('');
  protected readonly priority = signal<RequestPriority | ''>('');
  protected readonly categoryId = signal<number | ''>('');

  constructor() {
    const params = this.activatedRoute.snapshot.queryParamMap;
    const initialPage = Number(params.get('page'));
    this.page.set(Number.isInteger(initialPage) && initialPage > 0 ? initialPage : 1);
    this.status.set(this.parseStatus(params.get('status')));
    this.priority.set(this.parsePriority(params.get('priority')));
    const initialCategoryId = Number(params.get('categoryId'));
    this.categoryId.set(initialCategoryId > 0 ? initialCategoryId : '');

    this.loadCategories();
    this.loadRequests();
  }

  protected retryLoad(): void {
    this.loadRequests();
  }

  protected onStatusChange(value: string): void {
    this.status.set(this.parseStatus(value));
    this.page.set(1);
    this.reload();
  }

  protected onPriorityChange(value: string): void {
    this.priority.set(this.parsePriority(value));
    this.page.set(1);
    this.reload();
  }

  protected onCategoryChange(value: string): void {
    const parsed = Number(value);
    this.categoryId.set(parsed > 0 ? parsed : '');
    this.page.set(1);
    this.reload();
  }

  protected goToPreviousPage(): void {
    if (this.page() <= 1) {
      return;
    }

    this.page.update((value) => value - 1);
    this.reload();
  }

  protected goToNextPage(): void {
    const totalPages = this.result()?.totalPages ?? 1;

    if (this.page() >= totalPages) {
      return;
    }

    this.page.update((value) => value + 1);
    this.reload();
  }

  protected openDetails(request: RequestListItem): void {
    void this.router.navigate(['/requests', request.id]);
  }

  protected createRequest(): void {
    void this.router.navigate(['/requests', 'new']);
  }

  private reload(): void {
    this.updateQueryParams();
    this.loadRequests();
  }

  private updateQueryParams(): void {
    void this.router.navigate([], {
      relativeTo: this.activatedRoute,
      queryParams: {
        page: this.page(),
        status: this.status() || null,
        priority: this.priority() || null,
        categoryId: this.categoryId() || null,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private loadCategories(): void {
    this.categoryApi
      .getCategories(false)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => this.categories.set(categories),
        error: () => this.categories.set([]),
      });
  }

  private loadRequests(): void {
    if (this.isFetching) {
      return;
    }

    this.isFetching = true;
    this.isLoading.set(this.result() === null);
    this.loadError.set(null);

    this.requestApi
      .getRequests({
        page: this.page(),
        status: this.status() || undefined,
        priority: this.priority() || undefined,
        categoryId: this.categoryId() || undefined,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.isFetching = false;
          this.isLoading.set(false);
          this.result.set(result);
        },
        error: (error: unknown) => {
          this.isFetching = false;
          this.isLoading.set(false);
          this.loadError.set(this.resolveErrorMessage(error));
        },
      });
  }

  private parseStatus(value: string | null): RequestStatus | '' {
    return value && (REQUEST_STATUSES as readonly string[]).includes(value) ? (value as RequestStatus) : '';
  }

  private parsePriority(value: string | null): RequestPriority | '' {
    return value && (REQUEST_PRIORITIES as readonly string[]).includes(value) ? (value as RequestPriority) : '';
  }

  private resolveErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return SERVICE_UNAVAILABLE_MESSAGE;
      }

      if (error.status === 403) {
        return PERMISSION_DENIED_MESSAGE;
      }

      const problemDetails = error.error as ProblemDetails | null;

      if (problemDetails?.detail) {
        return problemDetails.detail;
      }
    }

    return LOAD_ERROR_FALLBACK;
  }
}
