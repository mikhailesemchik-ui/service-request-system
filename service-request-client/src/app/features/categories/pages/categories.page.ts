import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ProblemDetails } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';
import { CategoryFormComponent } from '../components/category-form.component';
import { ActiveStateChangeEvent, CategoryTableComponent } from '../components/category-table.component';
import { Category, CreateCategoryRequest } from '../models/category.models';
import { CategoryApiService } from '../services/category-api.service';

type FormState = { mode: 'closed' } | { mode: 'create' } | { mode: 'edit'; category: Category };
type Banner = { type: 'success' | 'error'; text: string };

const LOAD_ERROR_FALLBACK = 'Unable to load categories. Please try again.';
const SAVE_ERROR_FALLBACK = 'Unable to save the category. Please try again.';
const ACTIVE_STATE_ERROR_FALLBACK = 'Unable to update the category status. Please try again.';
const PERMISSION_DENIED_MESSAGE = 'You do not have permission to perform this action.';
const SERVICE_UNAVAILABLE_MESSAGE = 'Unable to reach the server. Please check your connection and try again.';

@Component({
  selector: 'app-categories-page',
  standalone: true,
  imports: [CategoryFormComponent, CategoryTableComponent],
  templateUrl: './categories.page.html',
  styleUrl: './categories.page.scss',
})
export class CategoriesPageComponent {
  private readonly categoryApi = inject(CategoryApiService);
  private readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  private isFetching = false;

  protected readonly isAdmin = computed(() => this.authService.hasRole('Admin'));

  protected readonly categories = signal<Category[] | null>(null);
  protected readonly isLoading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly includeInactive = signal(false);

  protected readonly formState = signal<FormState>({ mode: 'closed' });
  protected readonly isFormOpen = computed(() => this.formState().mode !== 'closed');
  protected readonly editingCategory = computed(() => {
    const state = this.formState();
    return state.mode === 'edit' ? state.category : null;
  });
  protected readonly isSaving = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly pendingActiveStateId = signal<number | null>(null);
  protected readonly banner = signal<Banner | null>(null);

  protected readonly categoryCount = computed(() => this.categories()?.length ?? 0);

  constructor() {
    this.loadCategories();
  }

  protected retryLoad(): void {
    this.loadCategories();
  }

  protected toggleIncludeInactive(): void {
    this.includeInactive.update((value) => !value);
    this.loadCategories();
  }

  protected openCreateForm(): void {
    this.formError.set(null);
    this.formState.set({ mode: 'create' });
  }

  protected openEditForm(category: Category): void {
    this.formError.set(null);
    this.formState.set({ mode: 'edit', category });
  }

  protected closeForm(): void {
    this.formState.set({ mode: 'closed' });
    this.formError.set(null);
  }

  protected handleFormSave(request: CreateCategoryRequest): void {
    const editingCategory = this.editingCategory();

    this.isSaving.set(true);
    this.formError.set(null);

    const request$ = editingCategory
      ? this.categoryApi.updateCategory(editingCategory.id, request)
      : this.categoryApi.createCategory(request);

    request$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (category) => {
        this.isSaving.set(false);
        this.closeForm();
        this.loadCategories();
        this.showBanner(
          'success',
          editingCategory ? `"${category.name}" was updated.` : `"${category.name}" was created.`,
        );
      },
      error: (error: unknown) => {
        this.isSaving.set(false);
        this.formError.set(this.resolveErrorMessage(error, SAVE_ERROR_FALLBACK));
      },
    });
  }

  protected handleActiveStateChange(event: ActiveStateChangeEvent): void {
    this.pendingActiveStateId.set(event.category.id);
    this.banner.set(null);

    this.categoryApi
      .setActiveState(event.category.id, event.isActive)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (category) => {
          this.pendingActiveStateId.set(null);
          this.loadCategories();
          this.showBanner('success', `"${category.name}" is now ${category.isActive ? 'active' : 'inactive'}.`);
        },
        error: (error: unknown) => {
          this.pendingActiveStateId.set(null);
          this.showBanner('error', this.resolveErrorMessage(error, ACTIVE_STATE_ERROR_FALLBACK));
        },
      });
  }

  protected dismissBanner(): void {
    this.banner.set(null);
  }

  private loadCategories(): void {
    if (this.isFetching) {
      return;
    }

    this.isFetching = true;
    this.isLoading.set(this.categories() === null);
    this.loadError.set(null);

    const includeInactive = this.isAdmin() && this.includeInactive();

    this.categoryApi
      .getCategories(includeInactive)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (categories) => {
          this.isFetching = false;
          this.isLoading.set(false);
          this.categories.set(categories);
        },
        error: (error: unknown) => {
          this.isFetching = false;
          this.isLoading.set(false);
          this.loadError.set(this.resolveErrorMessage(error, LOAD_ERROR_FALLBACK));
        },
      });
  }

  private showBanner(type: Banner['type'], text: string): void {
    this.banner.set({ type, text });
  }

  private resolveErrorMessage(error: unknown, fallback: string): string {
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

      const firstValidationError = problemDetails?.errors && Object.values(problemDetails.errors)[0]?.[0];
      if (firstValidationError) {
        return firstValidationError;
      }
    }

    return fallback;
  }
}
