import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ProblemDetails } from '../../../core/auth/auth.models';
import { AuthService } from '../../../core/auth/auth.service';

const GENERIC_INVALID_CREDENTIALS_MESSAGE = 'Invalid username or password.';
const SERVICE_UNAVAILABLE_MESSAGE = 'The service is currently unavailable. Please try again.';

/** Rejects protocol-relative and external return URLs; only a single-leading-slash internal path is safe. */
function getSafeReturnUrl(candidate: string | null, fallback: string): string {
  if (!candidate) {
    return fallback;
  }

  const isInternalPath = candidate.startsWith('/') && !candidate.startsWith('//') && !candidate.includes('\\');

  return isInternalPath ? candidate : fallback;
}

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
})
export class LoginPageComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);

  protected readonly form = this.formBuilder.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
  });

  protected readonly isSubmitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected submit(): void {
    if (this.isSubmitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const { username, password } = this.form.getRawValue();

    this.authService.login(username, password).subscribe({
      next: () => {
        const returnUrl = this.activatedRoute.snapshot.queryParamMap.get('returnUrl');
        void this.router.navigateByUrl(getSafeReturnUrl(returnUrl, '/dashboard'));
      },
      error: (error: unknown) => {
        this.isSubmitting.set(false);
        this.form.controls.password.setValue('');
        this.errorMessage.set(this.resolveErrorMessage(error));
      },
    });
  }

  private resolveErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 0) {
        return SERVICE_UNAVAILABLE_MESSAGE;
      }

      const problemDetails = error.error as ProblemDetails | null;
      if (problemDetails?.detail) {
        return problemDetails.detail;
      }
    }

    return GENERIC_INVALID_CREDENTIALS_MESSAGE;
  }
}
