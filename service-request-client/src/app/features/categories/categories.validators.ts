import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Trims before checking length; whitespace-only input is treated as empty (invalid). */
export function trimmedNameValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const trimmed = typeof control.value === 'string' ? control.value.trim() : '';

    if (trimmed.length === 0) {
      return { required: true };
    }

    if (trimmed.length < 2 || trimmed.length > 100) {
      return { trimmedLength: { min: 2, max: 100 } };
    }

    return null;
  };
}

/** Description is optional; only its trimmed length is validated. */
export function trimmedDescriptionValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const trimmed = typeof control.value === 'string' ? control.value.trim() : '';

    if (trimmed.length > 500) {
      return { trimmedMaxLength: { max: 500 } };
    }

    return null;
  };
}

/** Converts a whitespace-only value to `null`; otherwise returns the trimmed string. */
export function normalizeOptionalText(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}
