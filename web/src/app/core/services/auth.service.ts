import { Injectable } from '@angular/core';

const SESSION_KEY = 'tradebridge-demo-session';
const SIGNUP_KEY = 'tradebridge-demo-account';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly demoEmail = 'admin@tradebridge.com';
  readonly demoPassword = 'TradeBridge123!';

  isAuthenticated(): boolean {
    return localStorage.getItem(SESSION_KEY) === 'true';
  }

  login(email: string, password: string): boolean {
    const savedAccount = localStorage.getItem(SIGNUP_KEY);
    const account = savedAccount ? JSON.parse(savedAccount) as { email: string; password: string } : null;
    const validDemoLogin = email === this.demoEmail && password === this.demoPassword;
    const validLocalSignup = account?.email === email && account.password === password;

    if (!validDemoLogin && !validLocalSignup) return false;
    localStorage.setItem(SESSION_KEY, 'true');
    return true;
  }

  signup(name: string, email: string, password: string): void {
    localStorage.setItem(SIGNUP_KEY, JSON.stringify({ name, email, password }));
    localStorage.setItem(SESSION_KEY, 'true');
  }

  logout(): void {
    localStorage.removeItem(SESSION_KEY);
  }
}
