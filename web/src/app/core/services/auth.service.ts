import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap, tap } from 'rxjs';
import { ApiUrlService } from './api-url.service';

const TOKEN_KEY = 'tradebridge-access-token';
const ROLE_KEY = 'tradebridge-user-role';

interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  id: string;
  email: string;
  role: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiUrlService);

  isAuthenticated(): boolean {
    return sessionStorage.getItem(TOKEN_KEY) !== null;
  }

  login(identifier: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.api.url('/api/auth/login'), { identifier, password }).pipe(
      tap(result => {
        sessionStorage.setItem(TOKEN_KEY, result.accessToken);
        sessionStorage.setItem(ROLE_KEY, result.role);
      })
    );
  }

  signup(profile: {
    name: string;
    organization: string;
    email: string;
    phoneNumber: string;
    password: string;
    role: 'Importer' | 'Exporter' | 'CHA';
  }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(this.api.url('/api/auth/register'), profile).pipe(
      switchMap(() => this.login(profile.email || profile.phoneNumber, profile.password))
    );
  }


  logout(): void {
    this.http.post(this.api.url('/api/auth/logout'), {}).subscribe({
      complete: () => this.clearSession(),
      error: () => this.clearSession()
    });
    this.clearSession();
  }

  private clearSession(): void {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(ROLE_KEY);
  }

  currentRole(): string | null {
    return sessionStorage.getItem(ROLE_KEY);
  }
}

export const authTokenInterceptor: HttpInterceptorFn = (request, next) => {
  const token = sessionStorage.getItem(TOKEN_KEY);
  if (!token) return next(request);
  return next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
