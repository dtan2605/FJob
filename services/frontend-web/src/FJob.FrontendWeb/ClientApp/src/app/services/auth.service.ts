import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AppConfigService } from './app-config.service';
import { CurrentUser, LoginRequest, LoginResponse, RegisterRequest } from '../models';

const ACCESS_TOKEN_KEY = 'fjob.access_token';
const EXPIRES_AT_KEY = 'fjob.expires_at';
const USERNAME_KEY = 'fjob.username';
const ROLE_KEY = 'fjob.role';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly httpClient = inject(HttpClient);
  private readonly configService = inject(AppConfigService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly currentUser = signal<CurrentUser | null>(null);
  readonly isAuthenticated = signal(false);

  async initialize(): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const token = this.getAccessToken();
    if (!token) {
      this.clearSession();
      return;
    }

    const expiresAt = localStorage.getItem(EXPIRES_AT_KEY);
    if (expiresAt && new Date(expiresAt).getTime() <= Date.now()) {
      this.clearSession();
      return;
    }

    const username = localStorage.getItem(USERNAME_KEY);
    const role = localStorage.getItem(ROLE_KEY);
    if (username && role) {
      this.currentUser.set({ username, role });
      this.isAuthenticated.set(true);
      return;
    }

    try {
      const user = await firstValueFrom(this.httpClient.get<CurrentUser>(
        `${this.configService.snapshot.apiGatewayBaseUrl}/api/auth/me`,
        { headers: this.buildAuthHeaders() }
      ));

      this.currentUser.set(user);
      this.isAuthenticated.set(true);
      localStorage.setItem(USERNAME_KEY, user.username);
      localStorage.setItem(ROLE_KEY, user.role);
    } catch {
      this.clearSession();
    }
  }

  async login(request: LoginRequest): Promise<void> {
    const response = await firstValueFrom(this.httpClient.post<LoginResponse>(
      `${this.configService.snapshot.apiGatewayBaseUrl}/api/auth/login`,
      request
    ));

    this.persistSession(response);
  }

  async register(request: RegisterRequest): Promise<void> {
    await firstValueFrom(this.httpClient.post(
      `${this.configService.snapshot.apiGatewayBaseUrl}/api/auth/register`,
      request
    ));
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.httpClient.post(
        `${this.configService.snapshot.apiGatewayBaseUrl}/api/auth/logout`,
        {},
        { headers: this.buildAuthHeaders() }
      ));
    } catch {
      // Ignore server logout errors and clear local session anyway.
    }

    this.clearSession();
  }

  getAccessToken(): string | null {
    return isPlatformBrowser(this.platformId)
      ? localStorage.getItem(ACCESS_TOKEN_KEY)
      : null;
  }

  private persistSession(response: LoginResponse): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(EXPIRES_AT_KEY, response.expiresAtUtc);
    localStorage.setItem(USERNAME_KEY, response.username);
    localStorage.setItem(ROLE_KEY, response.role);
    this.currentUser.set({ username: response.username, role: response.role });
    this.isAuthenticated.set(true);
  }

  private clearSession(): void {
    if (isPlatformBrowser(this.platformId)) {
      localStorage.removeItem(ACCESS_TOKEN_KEY);
      localStorage.removeItem(EXPIRES_AT_KEY);
      localStorage.removeItem(USERNAME_KEY);
      localStorage.removeItem(ROLE_KEY);
    }

    this.currentUser.set(null);
    this.isAuthenticated.set(false);
  }

  private buildAuthHeaders(): HttpHeaders {
    const token = this.getAccessToken();
    return token
      ? new HttpHeaders({ Authorization: `Bearer ${token}` })
      : new HttpHeaders();
  }
}
