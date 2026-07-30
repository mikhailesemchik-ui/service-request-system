import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ProblemDetails } from '../../../core/auth/auth.models';
import { Category } from '../../categories/models/category.models';
import { CategoryApiService } from '../../categories/services/category-api.service';
import { trimmedDescriptionValidator, trimmedTitleValidator } from '../requests.validators';
import { REQUEST_PRIORITIES, RequestPriority } from '../models/request.models';
import { RequestApiService } from '../services/request-api.service';

const CREATE_ERROR_FALLBACK = 'Unable to create the request. Please try again.';
const CATEGORY_INACTIVE_MESSAGE = 'The selected category is no longer available. Please choose another category.';
const CATEGORIES_LOAD_ERROR = 'Unable to load categories. Please try again.';
const SERVICE_UNAVAILABLE_MESSAGE = 'Unable to reach the server. Please check your connection and try again.';
const DEFAULT_PRIORITY: RequestPriority = 'Medium';

@Component({
  selector: 'app-create-request-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './create-request.page.html',
  styleUrl: './create-request.page.scss',
})
export class CreateRequestPageComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly requestApi = inject(RequestApiService);
  private readonly categoryApi = inject(CategoryApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly priorities = REQUEST_PRIORITIES;

  protected readonly categories = signal<Category[] | null>(null);
  protected readonly categoriesError = signal<string | null>(null);

  protected readonly isSaving = signal(false);
  protected readonly serverError = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    title: ['', [trimmedTitleValidator()]],
    description: ['', [trimmedDescriptionValidator()]],
    categoryId: [null as number | null],
    priority: [DEFAULT_PRIORITY],
  });

  constructor() {
    this.loadCategories();
  }

  protected retryLoadCategories(): void {
    this.loadCategories();
  }

  protected cancel(): void {
    void this.router.navigate(['/requests']);
  }

  protected submit(): void {
    if (this.isSaving() || this.categories() === null) {
      return;
    }

    if (this.form.invalid || this.form.controls.categoryId.value === null) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.serverError.set(null);

    const raw = this.form.getRawValue();

    this.requestApi
      .createRequest({
        title: raw.title.trim(),
        description: raw.description.trim(),
        categoryId: raw.categoryId!,
        priority: raw.priority,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (created) => {
          this.isSaving.set(false);
          void this.router.navigate(['/requests', created.id]);
        },
        error: (error: unknown) => {
          this.isSaving.set(false);
          this.serverError.set(this.resolveErrorMessage(error));
        },
      });
  }

  private loadCategories(): void {
    this.categoriesError.set(null);

    this.categoryApi
      .getCategories(false)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => this.categories.set(categories),
        error: () => this.categoriesError.set(CATEGORIES_LOAD_ERROR),
      });
  }

  private resolveErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return SERVICE_UNAVAILABLE_MESSAGE;
      }

      if (error.status === 409) {
        return CATEGORY_INACTIVE_MESSAGE;
      }

      const problemDetails = error.error as ProblemDetails | null;

      if (problemDetails?.detail) {
        return problemDetails.detail;
      }

      const firstValidationError = problemDetails?.errors && Object.values(problemDetails.errors)[0]?.[0];
      if (firstValidationError) {
        return firstValidationError;
      }
    }

    return CREATE_ERROR_FALLBACK;
  }
}
