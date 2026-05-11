import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';

export interface TicketSoldEvent {
  categoryId: number;
  ticketId: number;
}

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private connection?: HubConnection;
  private readonly soldSubject = new Subject<TicketSoldEvent>();

  readonly ticketSold$ = this.soldSubject.asObservable();
  readonly connected = signal(false);

  async start(): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    const hubUrl = environment.apiUrl.replace(/\/api\/?$/, '') + '/hubs/tickets';

    this.connection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('TicketSold', (event: TicketSoldEvent) => {
      this.soldSubject.next(event);
    });

    this.connection.onreconnected(() => this.connected.set(true));
    this.connection.onclose(() => this.connected.set(false));

    await this.connection.start();
    this.connected.set(true);
  }

  async stop(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = undefined;
      this.connected.set(false);
    }
  }
}
