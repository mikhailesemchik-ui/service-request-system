import { DatePipe } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { RequestListItem } from '../models/request.models';

@Component({
  selector: 'app-request-table',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './request-table.component.html',
  styleUrl: './request-table.component.scss',
})
export class RequestTableComponent {
  readonly requests = input.required<RequestListItem[]>();

  readonly open = output<RequestListItem>();

  protected trackByRequestId(_index: number, request: RequestListItem): number {
    return request.id;
  }
}
