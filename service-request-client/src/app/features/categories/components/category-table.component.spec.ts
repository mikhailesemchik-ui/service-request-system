import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Category } from '../models/category.models';
import { ActiveStateChangeEvent, CategoryTableComponent } from './category-table.component';

const activeCategory: Category = {
  id: 1,
  name: 'Hardware',
  description: 'Physical equipment issues',
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-02-15T00:00:00Z',
};

const inactiveCategoryNoDescription: Category = {
  id: 2,
  name: 'Legacy',
  description: null,
  isActive: false,
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-02-20T00:00:00Z',
};

describe('CategoryTableComponent', () => {
  let fixture: ComponentFixture<CategoryTableComponent>;
  let component: CategoryTableComponent;

  function setCategories(categories: Category[], isAdmin = false): void {
    fixture.componentRef.setInput('categories', categories);
    fixture.componentRef.setInput('isAdmin', isAdmin);
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CategoryTableComponent],
    });

    fixture = TestBed.createComponent(CategoryTableComponent);
    component = fixture.componentInstance;
  });

  it('shows a fallback when a category has no description', () => {
    setCategories([inactiveCategoryNoDescription]);

    const cell = fixture.debugElement.query(By.css('td[data-label="Description"]'));
    expect(cell.nativeElement.textContent.trim()).toBe('No description');
  });

  it('shows a visible status label for active and inactive categories', () => {
    setCategories([activeCategory, inactiveCategoryNoDescription]);

    const statusCells = fixture.debugElement.queryAll(By.css('td[data-label="Status"] .status'));
    expect(statusCells[0].nativeElement.textContent.trim()).toBe('Active');
    expect(statusCells[1].nativeElement.textContent.trim()).toBe('Inactive');
  });

  it('renders dates without crashing', () => {
    expect(() => setCategories([activeCategory])).not.toThrow();

    const cell = fixture.debugElement.query(By.css('td[data-label="Updated"]'));
    expect(cell.nativeElement.textContent.trim().length).toBeGreaterThan(0);
  });

  it('does not render an actions column for non-admin users', () => {
    setCategories([activeCategory], false);

    expect(fixture.debugElement.query(By.css('th:last-child'))?.nativeElement.textContent).not.toContain('Actions');
    expect(fixture.debugElement.query(By.css('.category-table__actions'))).toBeNull();
  });

  it('renders management actions for admin users', () => {
    setCategories([activeCategory], true);

    const buttons = fixture.debugElement.queryAll(By.css('.category-table__actions button'));
    const labels = buttons.map((button) => button.nativeElement.textContent.trim());
    expect(labels).toContain('Edit');
    expect(labels).toContain('Deactivate');
  });

  it('emits edit with the selected category', () => {
    setCategories([activeCategory], true);

    let emitted: Category | undefined;
    component.edit.subscribe((category) => (emitted = category));

    fixture.debugElement.query(By.css('.category-table__actions button')).nativeElement.click();

    expect(emitted).toEqual(activeCategory);
  });

  it('activating an inactive category emits immediately without confirmation', () => {
    setCategories([inactiveCategoryNoDescription], true);

    let emitted: ActiveStateChangeEvent | undefined;
    component.activeStateChange.subscribe((event) => (emitted = event));

    const reactivateButton = fixture.debugElement.queryAll(By.css('.category-table__actions button'))[1];
    reactivateButton.nativeElement.click();

    expect(emitted).toEqual({ category: inactiveCategoryNoDescription, isActive: true });
  });

  it('deactivating an active category requires confirmation before emitting', () => {
    setCategories([activeCategory], true);

    let emitted: ActiveStateChangeEvent | undefined;
    component.activeStateChange.subscribe((event) => (emitted = event));

    const deactivateButton = fixture.debugElement.queryAll(By.css('.category-table__actions button'))[1];
    deactivateButton.nativeElement.click();
    fixture.detectChanges();

    expect(emitted).toBeUndefined();
    expect(fixture.debugElement.query(By.css('.confirm'))).not.toBeNull();
  });

  it('does not emit when the confirmation is cancelled', () => {
    setCategories([activeCategory], true);

    let emitted = false;
    component.activeStateChange.subscribe(() => (emitted = true));

    fixture.debugElement.queryAll(By.css('.category-table__actions button'))[1].nativeElement.click();
    fixture.detectChanges();

    const cancelButton = fixture.debugElement.query(By.css('.confirm button:last-child'));
    cancelButton.nativeElement.click();
    fixture.detectChanges();

    expect(emitted).toBeFalse();
    expect(fixture.debugElement.query(By.css('.confirm'))).toBeNull();
  });

  it('emits deactivation once confirmed', () => {
    setCategories([activeCategory], true);

    let emitted: ActiveStateChangeEvent | undefined;
    component.activeStateChange.subscribe((event) => (emitted = event));

    fixture.debugElement.queryAll(By.css('.category-table__actions button'))[1].nativeElement.click();
    fixture.detectChanges();

    const confirmButton = fixture.debugElement.query(By.css('.confirm button:first-of-type'));
    confirmButton.nativeElement.click();

    expect(emitted).toEqual({ category: activeCategory, isActive: false });
  });

  it('disables the toggle action while it is pending for that category', () => {
    setCategories([activeCategory], true);
    fixture.componentRef.setInput('pendingActiveStateId', activeCategory.id);
    fixture.detectChanges();

    const toggleButton = fixture.debugElement.queryAll(By.css('.category-table__actions button'))[1];
    expect(toggleButton.nativeElement.disabled).toBeTrue();
  });
});
