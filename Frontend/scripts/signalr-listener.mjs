// Simulates a second browser window subscribing to the TicketHub.
// Logs every TicketSold event for ~12 seconds, then exits.
import process from 'node:process';
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr');

const hubUrl = 'https://localhost:5001/hubs/tickets';

const connection = new HubConnectionBuilder()
  .withUrl(hubUrl)
  .configureLogging(LogLevel.Warning)
  .build();

connection.on('TicketSold', event => {
  console.log(`[event] TicketSold categoryId=${event.categoryId} ticketId=${event.ticketId}`);
});

await connection.start();
console.log('[ready] subscribed at', hubUrl);

setTimeout(async () => {
  await connection.stop();
  console.log('[done] stopped');
  process.exit(0);
}, 12000);
