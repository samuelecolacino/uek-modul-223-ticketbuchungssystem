import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SignalRService } from './tickets/signalr.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  private readonly signalr = inject(SignalRService);

  async ngOnInit(): Promise<void> {
    try {
      await this.signalr.start();
    } catch (err) {
      console.warn('SignalR connection failed', err);
    }
  }
}
