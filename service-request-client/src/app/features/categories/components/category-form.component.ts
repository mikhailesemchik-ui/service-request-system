import { AfterViewInit, Component, ElementRef, ViewChild, computed, effect, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Category, CreateCategoryRequest } from '../models/category.models';
import { normalizeOptionalText, trimmedDescriptionValidator, trimmedNameValidator } from '../categories.validators';

@Component({
  selector: 'app-category-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './category-form.component.html',
  styleUrl: './category-form.component.scss',
})
export class CategoryFormComponent implements AfterViewInit {
  private readonly formBuilder = inject(FormBuilder);

  @ViewChild('nameInput') private readonly nameInputRef?: ElementRef<HTMLInputElement>;

  /** `null` means create mode; a category means edit mode (prefilled, `isActive` untouched). */
  readonly category = input<Category | null>(null);
  readonly isSaving = input(false);
  readonly serverError = input<string | null>(null);

  readonly save = output<CreateCategoryRequest>();
  readonly cancel = output<void>();

  protected readonly isEditMode = computed(() => this.category() !== null);

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [trimmedNameValidator()]],
    description: ['', [trimmedDescriptionValidator()]],
  });

  constructor() {
    effect(() => {
      const category = this.category();
      this.form.reset({
        name: category?.name ?? '',
        description: category?.description ?? '',
      });
    });
  }

  protected submit(): void {
    if (this.isSaving()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();

    this.save.emit({
      name: raw.name.trim(),
      description: normalizeOptionalText(raw.description),
    });
  }

  protected close(): void {
    this.cancel.emit();
  }

  ngAfterViewInit(): void {
    this.nameInputRef?.nativeElement.focus();
  }
}
