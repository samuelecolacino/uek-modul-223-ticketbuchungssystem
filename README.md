# TicketShop Applikation

> Applikation zum simultanen Verkauf von Tickets mit Concurrency Control.

---

## Inhaltsverzeichnis
- [Übersicht](#übersicht)
- [Architektur](#architektur)
- [Datenmodell (ERD)](#datenmodell-erd)
- [API-Dokumentation](#api-dokumentation)
- [Wichtige Abläufe](#wichtige-abläufe)
- [Voraussetzungen](#voraussetzungen)
- [Installation & Start](#installation--start)
- [Testbenutzer](#testbenutzer)
- [Tests ausführen](#tests-ausführen)

---

## Übersicht

TicketShop ist ein Multiuser-Ticketbuchungssystem, das im Rahmen des **uek Modul 223** entstanden ist. Die zentrale fachliche Anforderung lautet: zwei Benutzer, die zeitgleich dasselbe Ticket kaufen wollen, dürfen nicht beide erfolgreich sein. Das System löst dies über **Optimistic Concurrency Control** mit einer `[Timestamp]`-Spalte (`RowVersion`) auf der Tabelle `Tickets`. Schlägt der zweite Schreibvorgang fehl, antwortet die API mit `HTTP 409 Conflict`, und das Frontend zeigt dem User eine klare rote Meldung.

Technisch besteht das Projekt aus drei Teilen:

- **Backend** – .NET 10 Web API in Clean-Architecture-Struktur (`Core` → `Application` → `Infrastructure` → `Api`), JWT-Authentifizierung, FluentValidation, Swagger.
- **Frontend** – Angular 19 SPA mit standalone Components, JWT-Interceptor, reaktivem Formular und Toast-Feedback bei `HTTP 409`.
- **Tests** – xUnit-Unit-Tests gegen einen SQLite-In-Memory-Doppelpacker (echte RowVersion-Kollision **und** gemockter `DbUpdateConcurrencyException`-Pfad) plus einen NBomber-Lasttest, der 50 gleichzeitige User für 30 Sekunden auf den `/api/tickets/buy`-Endpoint feuert und beweist, dass es weder 5xx-Fehler noch *Lost Updates* gibt.

## Architektur

```mermaid
graph TD
    Client[Angular Frontend] --> API[.NET 10 API]
    API --> App[Application Layer]
    App --> Domain[Core Domain]
    App --> Infra[Infrastructure]
    Infra --> DB[(SQL Server Docker)]
```

Die `Api`-Schicht hält ausschliesslich HTTP-Anliegen (Controller, Auth-Pipeline, Swagger, CORS). `Application` definiert DTOs, Service-Interfaces (`IAuthService`, `ITicketService`, `ITokenService`) und FluentValidation-Regeln. `Infrastructure` implementiert diese Services und enthält den `AppDbContext` samt EF-Core-Mapping. `Core` enthält nur reine Domain-Entities ohne EF- oder ASP.NET-Abhängigkeiten.

## Datenmodell (ERD)

```mermaid
erDiagram
    USER ||--o{ TICKET : "kauft"
    TICKET_CATEGORY ||--o{ TICKET : "klassifiziert"

    USER {
        int Id PK
        string Username UK "max 64, unique"
        string PasswordHash "BCrypt"
        string Role "max 32 (Admin/User)"
    }

    TICKET_CATEGORY {
        int Id PK
        string Name "max 64"
        decimal Price "decimal(18,2)"
    }

    TICKET {
        int Id PK
        int TicketCategoryId FK
        int UserId FK "nullable"
        bool IsSold
        bytes RowVersion "Timestamp (Optimistic Concurrency)"
    }
```

Wichtig: `Ticket.RowVersion` ist als `[Timestamp]` markiert und in `OnModelCreating` mit `.IsRowVersion()` konfiguriert. SQL Server pflegt die Spalte automatisch — jede `UPDATE`-Anweisung enthält implizit `WHERE Id = @id AND RowVersion = @originalVersion`. Stimmt die Version nicht überein, sind 0 Zeilen betroffen und EF wirft eine `DbUpdateConcurrencyException`.

## API-Dokumentation

| Methode | Pfad                     | Auth         | Body                              | Antworten                                                                                                                                  |
| ------- | ------------------------ | ------------ | --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| POST    | `/api/auth/login`        | öffentlich   | `{ "username", "password" }`      | `200 OK` `{ token, username, role }` · `401 Unauthorized` `{ message: "Ungültiger Benutzername oder Passwort." }`                          |
| GET     | `/api/tickets/available` | Bearer (JWT) | –                                 | `200 OK` `AvailableCategoryDto[]` (Kategorien mit `availableCount` und `ticketIds`) · `401 Unauthorized`                                   |
| POST    | `/api/tickets/buy`       | Bearer (JWT) | `{ "ticketId": <int, > 0> }`      | `200 OK` `{ ticketId, userId }` · `400 BadRequest` (FluentValidation) · `404 NotFound` (Ticket bereits verkauft) · **`409 Conflict`** `{ message: "Das Ticket wurde in der Zwischenzeit von einem anderen Benutzer gekauft." }` |

Die JWT-Tokens werden mit HMAC-SHA256 signiert (Secret aus `appsettings.json`, Section `Jwt`) und sind 120 Minuten gültig. Swagger UI unter `https://localhost:5001/swagger` bietet einen **Authorize**-Button oben rechts zum Einfügen des Tokens.

## Wichtige Abläufe

Der zentrale Ablauf des Kaufs mit Optimistic Concurrency Control, wenn zwei User dasselbe Ticket gleichzeitig anfragen:

```mermaid
sequenceDiagram
    autonumber
    actor UserA as User A (Browser)
    actor UserB as User B (Browser)
    participant API as TicketShop API
    participant Svc as TicketService
    participant DB as SQL Server

    par Zwei Käufer gleichzeitig
        UserA->>API: POST /api/tickets/buy { ticketId: 42 }<br/>Authorization: Bearer <jwt>
        API->>Svc: BuyAsync(42, userIdA)
        Svc->>DB: BEGIN TRANSACTION
        Svc->>DB: SELECT * FROM Tickets WHERE Id=42 AND IsSold=0
        DB-->>Svc: Ticket (RowVersion = v1)
    and
        UserB->>API: POST /api/tickets/buy { ticketId: 42 }<br/>Authorization: Bearer <jwt>
        API->>Svc: BuyAsync(42, userIdB)
        Svc->>DB: BEGIN TRANSACTION
        Svc->>DB: SELECT * FROM Tickets WHERE Id=42 AND IsSold=0
        DB-->>Svc: Ticket (RowVersion = v1)
    end

    Svc->>DB: UPDATE Tickets SET IsSold=1, UserId=A<br/>WHERE Id=42 AND RowVersion=v1
    DB-->>Svc: 1 Zeile betroffen → RowVersion = v2
    Svc->>DB: COMMIT
    API-->>UserA: 200 OK { ticketId: 42, userId: A }

    Svc->>DB: UPDATE Tickets SET IsSold=1, UserId=B<br/>WHERE Id=42 AND RowVersion=v1
    DB-->>Svc: 0 Zeilen betroffen
    Note over Svc,DB: EF Core erkennt den Mismatch und<br/>wirft DbUpdateConcurrencyException
    Svc-->>API: TicketPurchaseResult(ConcurrencyConflict, "Das Ticket wurde in der Zwischenzeit ...")
    API-->>UserB: 409 Conflict { message: "Das Ticket wurde in der Zwischenzeit von einem anderen Benutzer gekauft." }

    Note over UserB: Frontend zeigt roten Toast:<br/>"Achtung: Jemand war schneller! Bitte erneut versuchen."
```

## Voraussetzungen
- .NET 10 SDK
- Node.js 20+
- Docker Desktop

## Installation & Start

Repository klonen und in den Projekt-Root wechseln, dann in dieser Reihenfolge:

### 1. Datenbank starten

```bash
docker compose up -d
```

Startet den Container `ticketshop-mssql` (SQL Server 2022) auf Port `1433`. SA-Passwort: `Passwort123!`. Die Daten liegen persistent im benannten Volume `mssql_data`.

### 2. Backend starten

```bash
cd Backend
dotnet restore
dotnet run --project TicketShop.Api --launch-profile https
```

Die API hört auf `https://localhost:5001` (und `http://localhost:5000`). Beim ersten Start führt der `DbSeeder` automatisch `MigrateAsync` aus und legt zwei Test-User (`admin`/`admin`, `user`/`user`), die Kategorien **VIP** (CHF 150.00) und **Standard** (CHF 80.00) sowie 50 Tickets pro Kategorie an. Swagger-UI: `https://localhost:5001/swagger`.

### 3. Frontend starten

```bash
cd Frontend
npm install
npm start
```

Angular Dev-Server unter `http://localhost:4200`. Die API-URL ist in `Frontend/src/environments/environment.ts` als `https://localhost:5001/api` konfiguriert; CORS für diesen Origin ist in der Backend-Pipeline aktiv.

## Testbenutzer
- `admin` / `admin` (Role `Admin`)
- `user` / `user` (Role `User`)

## Tests ausführen

Alle Tests (xUnit + NBomber-Lasttest) laufen aus dem `Backend`-Ordner:

```bash
cd Backend
dotnet test
```

Erwartung: **8 / 8 grün**, Gesamtlaufzeit ca. 45 s — davon ca. 30 s NBomber-Lastlauf.

Einzelne Test-Gruppen lassen sich gezielt starten:

```bash
# Nur die TicketService Unit-Tests (Happy-Path)
dotnet test --filter "FullyQualifiedName~TicketServiceTests"

# Nur die Concurrency-Tests (inkl. BuyTicket_ConcurrentAccess_ThrowsException)
dotnet test --filter "FullyQualifiedName~Concurrency"

# Nur der NBomber-Lasttest (50 User × 30 s)
dotnet test --filter "FullyQualifiedName~TicketLoadTest"
```

Der NBomber-Lasttest läuft in-process via `WebApplicationFactory<Program>` gegen ein frisch geseedetes SQLite-In-Memory-Backend (20 000 Tickets, ein dedizierter Loaduser). Er druckt eine vollständige NBomber-Reports-Tabelle auf die Console und assertet zwei Dinge:

- **Stabilität** — `fail count == 0` (keine 5xx).
- **Keine Lost Updates** — die Anzahl `IsSold = true`-Zeilen in der Datenbank stimmt exakt mit der Anzahl `HTTP 200`-Antworten überein.
