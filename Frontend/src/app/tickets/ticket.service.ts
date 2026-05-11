import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AvailableCategory {
  categoryId: number;
  name: string;
  price: number;
  availableCount: number;
  ticketIds: number[];
  isAdminOnly: boolean;
}

export interface BuyTicketResponse {
  ticketId: number;
  userId: number;
}

@Injectable({ providedIn: 'root' })
export class TicketService {
  private readonly http = inject(HttpClient);

  getAvailable(): Observable<AvailableCategory[]> {
    return this.http.get<AvailableCategory[]>(`${environment.apiUrl}/tickets/available`);
  }

  buy(ticketId: number): Observable<BuyTicketResponse> {
    return this.http.post<BuyTicketResponse>(`${environment.apiUrl}/tickets/buy`, { ticketId });
  }
}
