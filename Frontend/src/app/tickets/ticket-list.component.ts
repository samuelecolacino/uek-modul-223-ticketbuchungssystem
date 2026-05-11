import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { SignalRService, TicketSoldEvent } from './signalr.service';
import { AvailableCategory, TicketService } from './ticket.service';

@Component({
  selector: 'app-ticket-list',
  imports: [
    DecimalPipe,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatToolbarModule
  ],
  templateUrl: './ticket-list.component.html',
  styleUrl: './ticket-list.component.scss'
})
export class TicketListComponent implements OnInit {
  private readonly tickets = inject(TicketService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly signalr = inject(SignalRService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly snackBar = inject(MatSnackBar);

  readonly categories = signal<AvailableCategory[]>([]);
  readonly loading = signal(false);
  readonly buyingCategoryId = signal<number | null>(null);
  readonly username = computed(() => this.auth.username());
  readonly liveConnected = this.signalr.connected;

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
        this.openError('Tickets konnten nicht geladen werden.');
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
        this.openSuccess(`Ticket #${response.ticketId} (${category.name}) erfolgreich gekauft.`);
        this.reload();
      },
      error: (err: HttpErrorResponse) => {
        this.buyingCategoryId.set(null);
        if (err.status === 409) {
          this.openError('Achtung: Jemand war schneller! Bitte erneut versuchen.');
        } else if (err.status === 404) {
          this.openError('Ticket nicht verfügbar oder bereits verkauft.');
        } else if (err.status === 401) {
          this.auth.logout();
          this.router.navigateByUrl('/login');
          return;
        } else {
          this.openError('Kauf fehlgeschlagen. Bitte erneut versuchen.');
        }
        this.reload();
      }
    });
  }

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/login');
  }

  private openError(message: string): void {
    this.snackBar.open(message, 'Schliessen', {
      duration: 5000,
      panelClass: ['snackbar-error'],
      horizontalPosition: 'center',
      verticalPosition: 'bottom'
    });
  }

  private openSuccess(message: string): void {
    this.snackBar.open(message, 'OK', {
      duration: 3500,
      panelClass: ['snackbar-success'],
      horizontalPosition: 'center',
      verticalPosition: 'bottom'
    });
  }
}
