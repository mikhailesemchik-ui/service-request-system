import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Category, CreateCategoryRequest } from '../models/category.models';
import { CategoryFormComponent } from './category-form.component';

const testCategory: Category = {
  id: 7,
  name: 'Hardware',
  description: 'Physical equipment issues',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('CategoryFormComponent', () => {
  let fixture: ComponentFixture<CategoryFormComponent>;
  let component: CategoryFormComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CategoryFormComponent],
    });

    fixture = TestBed.createComponent(CategoryFormComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('starts invalid when name is empty', () => {
    expect(component['form'].valid).toBeFalse();
    expect(component['form'].controls.name.errors?.['required']).toBeTrue();
  });

  it('treats a whitespace-only name as invalid', () => {
    component['form'].controls.name.setValue('    ');
    expect(component['form'].controls.name.errors?.['required']).toBeTrue();
  });

  it('enforces trimmed name length between 2 and 100 characters', () => {
    component['form'].controls.name.setValue(' a ');
    expect(component['form'].controls.name.errors?.['trimmedLength']).toBeTruthy();

    component['form'].controls.name.setValue('a'.repeat(101));
    expect(component['form'].controls.name.errors?.['trimmedLength']).toBeTruthy();

    component['form'].controls.name.setValue('Hardware');
    expect(component['form'].controls.name.errors).toBeNull();
  });

  it('enforces a 500 character maximum on the trimmed description', () => {
    component['form'].controls.description.setValue(`  ${'a'.repeat(501)}  `);
    expect(component['form'].controls.description.errors?.['trimmedMaxLength']).toBeTruthy();

    component['form'].controls.description.setValue(`  ${'a'.repeat(500)}  `);
    expect(component['form'].controls.description.errors).toBeNull();
  });

  it('normalizes name and description on submit', () => {
    let emitted: CreateCategoryRequest | undefined;
    component.save.subscribe((value) => (emitted = value));

    component['form'].setValue({ name: '  Hardware  ', description: '   ' });
    component['submit']();

    expect(emitted).toEqual({ name: 'Hardware', description: null });
  });

  it('trims a non-empty description instead of nulling it', () => {
    let emitted: CreateCategoryRequest | undefined;
    component.save.subscribe((value) => (emitted = value));

    component['form'].setValue({ name: 'Hardware', description: '  Equipment issues  ' });
    component['submit']();

    expect(emitted?.description).toBe('Equipment issues');
  });

  it('does not emit save while already saving (prevents duplicate submission)', () => {
    fixture.componentRef.setInput('isSaving', true);
    fixture.detectChanges();

    let emitted = false;
    component.save.subscribe(() => (emitted = true));

    component['form'].setValue({ name: 'Hardware', description: '' });
    component['submit']();

    expect(emitted).toBeFalse();
  });

  it('does not emit save when the form is invalid', () => {
    let emitted = false;
    component.save.subscribe(() => (emitted = true));

    component['submit']();

    expect(emitted).toBeFalse();
    expect(component['form'].controls.name.touched).toBeTrue();
  });

  it('prefills the form with the category values in edit mode', () => {
    fixture.componentRef.setInput('category', testCategory);
    fixture.detectChanges();

    expect(component['form'].controls.name.value).toBe(testCategory.name);
    expect(component['form'].controls.description.value).toBe(testCategory.description ?? '');
    expect(component['isEditMode']()).toBeTrue();
  });

  it('does not include isActive in the emitted value when editing', () => {
    fixture.componentRef.setInput('category', testCategory);
    fixture.detectChanges();

    let emitted: CreateCategoryRequest | undefined;
    component.save.subscribe((value) => (emitted = value));

    component['submit']();

    expect(emitted).toEqual({ name: testCategory.name, description: testCategory.description });
    expect((emitted as unknown as Record<string, unknown>)['isActive']).toBeUndefined();
  });

  it('emits cancel without emitting save', () => {
    let cancelled = false;
    let saved = false;
    component.cancel.subscribe(() => (cancelled = true));
    component.save.subscribe(() => (saved = true));

    component['close']();

    expect(cancelled).toBeTrue();
    expect(saved).toBeFalse();
  });

  it('displays a backend duplicate-name error', () => {
    fixture.componentRef.setInput('serverError', 'A category named "Hardware" already exists.');
    fixture.detectChanges();

    const errorEl = fixture.debugElement.query(By.css('.category-form__error'));
    expect(errorEl.nativeElement.textContent).toContain('A category named "Hardware" already exists.');
  });
});
