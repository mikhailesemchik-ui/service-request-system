import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** Trims before checking length; whitespace-only input is treated as empty (invalid). */
export function trimmedTitleValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const trimmed = typeof control.value === 'string' ? control.value.trim() : '';

    if (trimmed.length === 0) {
      return { required: true };
    }

    if (trimmed.length < 3 || trimmed.length > 200) {
      return { trimmedLength: { min: 3, max: 200 } };
    }

    return null;
  };
}

/** Trims before checking length; whitespace-only input is treated as empty (invalid). */
export function trimmedDescriptionValidator(minLength = 10): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const trimmed = typeof control.value === 'string' ? control.value.trim() : '';

    if (trimmed.length === 0) {
      return { required: true };
    }

    if (trimmed.length < minLength || trimmed.length > 4000) {
      return { trimmedLength: { min: minLength, max: 4000 } };
    }

    return null;
  };
}
