import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { RequestListItem } from '../models/request.models';
import { RequestTableComponent } from './request-table.component';

const assignedRequest: RequestListItem = {
  id: 1,
  title: 'Printer not working',
  status: 'New',
  priority: 'High',
  category: { id: 1, name: 'Hardware' },
  createdBy: { id: 3, displayName: 'Development Employee' },
  assignedTo: { id: 4, displayName: 'Support Agent' },
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

const unassignedRequest: RequestListItem = {
  ...assignedRequest,
  id: 2,
  title: 'Software license expired',
  assignedTo: null,
};

describe('RequestTableComponent', () => {
  let fixture: ComponentFixture<RequestTableComponent>;
  let component: RequestTableComponent;

  function setRequests(requests: RequestListItem[]): void {
    fixture.componentRef.setInput('requests', requests);
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [RequestTableComponent] });
    fixture = TestBed.createComponent(RequestTableComponent);
    component = fixture.componentInstance;
  });

  it('shows the assignee display name when assigned', () => {
    setRequests([assignedRequest]);

    const cell = fixture.debugElement.query(By.css('td[data-label="Assigned to"]'));
    expect(cell.nativeElement.textContent.trim()).toBe('Support Agent');
  });

  it('shows an Unassigned fallback when there is no assignee', () => {
    setRequests([unassignedRequest]);

    const cell = fixture.debugElement.query(By.css('td[data-label="Assigned to"]'));
    expect(cell.nativeElement.textContent.trim()).toBe('Unassigned');
  });

  it('renders dates without crashing', () => {
    expect(() => setRequests([assignedRequest])).not.toThrow();
    const cell = fixture.debugElement.query(By.css('td[data-label="Created"]'));
    expect(cell.nativeElement.textContent.trim().length).toBeGreaterThan(0);
  });

  it('emits open with the selected request when its View action is clicked', () => {
    setRequests([assignedRequest, unassignedRequest]);

    let emitted: RequestListItem | undefined;
    component.open.subscribe((request) => (emitted = request));

    fixture.debugElement.queryAll(By.css('button'))[1].nativeElement.click();

    expect(emitted).toEqual(unassignedRequest);
  });
});
