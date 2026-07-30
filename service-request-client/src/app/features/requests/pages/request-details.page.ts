import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { ProblemDetails } from '../../../core/auth/auth.models';
import { RequestDetails } from '../models/request.models';
import { RequestApiService } from '../services/request-api.service';

const NOT_FOUND_MESSAGE = 'This request does not exist or is not available to you.';
const LOAD_ERROR_FALLBACK = 'Unable to load this request. Please try again.';
const SERVICE_UNAVAILABLE_MESSAGE = 'Unable to reach the server. Please check your connection and try again.';
const INVALID_ID_MESSAGE = 'This request could not be found.';

@Component({
  selector: 'app-request-details-page',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './request-details.page.html',
  styleUrl: './request-details.page.scss',
})
export class RequestDetailsPageComponent {
  private readonly requestApi = inject(RequestApiService);
  private readonly activatedRoute = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly request = signal<RequestDetails | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  constructor() {
    this.loadRequest();
  }

  protected retryLoad(): void {
    this.loadRequest();
  }

  protected backToList(): void {
    void this.router.navigate(['/requests']);
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
        },
        error: (error: unknown) => {
          this.isLoading.set(false);
          this.loadError.set(this.resolveErrorMessage(error));
        },
      });
  }

  private resolveErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return SERVICE_UNAVAILABLE_MESSAGE;
      }

      if (error.status === 404) {
        return NOT_FOUND_MESSAGE;
      }

      const problemDetails = error.error as ProblemDetails | null;

      if (problemDetails?.detail) {
        return problemDetails.detail;
      }
    }

    return LOAD_ERROR_FALLBACK;
  }
}
