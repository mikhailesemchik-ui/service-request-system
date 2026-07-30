import { DatePipe } from '@angular/common';
import { Component, input, output, signal } from '@angular/core';
import { Category } from '../models/category.models';

export interface ActiveStateChangeEvent {
  category: Category;
  isActive: boolean;
}

@Component({
  selector: 'app-category-table',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './category-table.component.html',
  styleUrl: './category-table.component.scss',
})
export class CategoryTableComponent {
  readonly categories = input.required<Category[]>();
  readonly isAdmin = input(false);
  readonly pendingActiveStateId = input<number | null>(null);

  readonly edit = output<Category>();
  readonly activeStateChange = output<ActiveStateChangeEvent>();

  protected readonly confirmingDeactivateId = signal<number | null>(null);

  protected trackByCategoryId(_index: number, category: Category): number {
    return category.id;
  }

  protected onToggleClick(category: Category): void {
    if (!category.isActive) {
      this.activeStateChange.emit({ category, isActive: true });
      return;
    }

    this.confirmingDeactivateId.set(category.id);
  }

  protected confirmDeactivate(category: Category): void {
    this.confirmingDeactivateId.set(null);
    this.activeStateChange.emit({ category, isActive: false });
  }

  protected cancelDeactivate(): void {
    this.confirmingDeactivateId.set(null);
  }
}
