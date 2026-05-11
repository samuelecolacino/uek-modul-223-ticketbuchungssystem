import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { SignalRService, TicketSoldEvent } from './signalr.service';
import { AvailableCategory, TicketService } from './ticket.service';

type ToastKind = 'success' | 'error';

interface Toast {
  kind: ToastKind;
  message: string;
}

@Component({
  selector: 'app-ticket-list',
  imports: [DecimalPipe],
  templateUrl: './ticket-list.component.html',
  styleUrl: './ticket-list.component.scss'
})
export class TicketListComponent implements OnInit {
  private readonly tickets = inject(TicketService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly signalr = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);

  readonly categories = signal<AvailableCategory[]>([]);
  readonly loading = signal(false);
  readonly buyingCategoryId = signal<number | null>(null);
  readonly toast = signal<Toast | null>(null);
  readonly username = computed(() => this.auth.username());

  ngOnInit(): void {
    this.reload();

    this.signalr.ticketSold$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(event => this.applyTicketSold(event));
  }

  private applyTicketSold(event: TicketSoldEvent): void {
    this.categories.update(list =>
      list.map(c =>
        c.categoryId === event.categoryId
          ? {
              ...c,
              availableCount: Math.max(0, c.availableCount - 1),
              ticketIds: c.ticketIds.filter(id => id !== event.ticketId)
            }
          : c));
  }

  reload(): void {
    this.loading.set(true);
    this.tickets.getAvailable().subscribe({
      next: data => {
        this.categories.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.showToast('error', 'Tickets konnten nicht geladen werden.');
      }
    });
  }

  buyFirst(category: AvailableCategory): void {
    if (category.ticketIds.length === 0 || this.buyingCategoryId() !== null) {
      return;
    }
    const ticketId = category.ticketIds[0];

    this.buyingCategoryId.set(category.categoryId);

    this.tickets.buy(ticketId).subscribe({
      next: response => {
        this.buyingCategoryId.set(null);
        this.showToast('success', `Ticket #${response.ticketId} (${category.name}) erfolgreich gekauft.`);
        this.reload();
      },
      error: (err: HttpErrorResponse) => {
        this.buyingCategoryId.set(null);
        if (err.status === 409) {
          this.showToast('error', 'Achtung: Jemand war schneller! Bitte erneut versuchen.');
        } else if (err.status === 404) {
          this.showToast('error', 'Ticket nicht verfügbar oder bereits verkauft.');
        } else if (err.status === 401) {
          this.auth.logout();
          this.router.navigateByUrl('/login');
          return;
        } else {
          this.showToast('error', 'Kauf fehlgeschlagen. Bitte erneut versuchen.');
        }
        this.reload();
      }
    });
  }

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/login');
  }

  private showToast(kind: ToastKind, message: string): void {
    this.toast.set({ kind, message });
    setTimeout(() => {
      const current = this.toast();
      if (current?.message === message) {
        this.toast.set(null);
      }
    }, 5000);
  }
}
