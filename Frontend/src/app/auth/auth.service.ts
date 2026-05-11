import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  username: string;
  role: string;
}

const TOKEN_KEY = 'ticketshop.token';
const USERNAME_KEY = 'ticketshop.username';
const ROLE_KEY = 'ticketshop.role';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  readonly username = signal<string | null>(localStorage.getItem(USERNAME_KEY));
  readonly role = signal<string | null>(localStorage.getItem(ROLE_KEY));

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiUrl}/auth/login`, request)
      .pipe(tap(response => this.setSession(response)));
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USERNAME_KEY);
    localStorage.removeItem(ROLE_KEY);
    this.username.set(null);
    this.role.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return this.getToken() !== null;
  }

  isAdmin(): boolean {
    const token = this.getToken();
    if (!token) {
      return false;
    }
    try {
      const payloadSegment = token.split('.')[1];
      if (!payloadSegment) {
        return false;
      }
      const padded = payloadSegment.replace(/-/g, '+').replace(/_/g, '/');
      const payload = JSON.parse(atob(padded));
      const claim =
        payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ??
        payload['role'];
      const roles = Array.isArray(claim) ? claim : [claim];
      return roles.includes('Admin');
    } catch {
      return false;
    }
  }

  private setSession(response: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USERNAME_KEY, response.username);
    localStorage.setItem(ROLE_KEY, response.role);
    this.username.set(response.username);
    this.role.set(response.role);
  }
}
