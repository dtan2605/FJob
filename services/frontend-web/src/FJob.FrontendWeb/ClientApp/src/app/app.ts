import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AppConfigService } from './services/app-config.service';
import { AuthService } from './services/auth.service';
import { CvStoreService } from './services/cv-store.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="site-shell">
      <header class="site-header">
        <div class="brand-lockup">
          <a class="brand-mark" routerLink="/">FJ</a>
          <div>
            <strong class="brand-name">{{ configService.snapshot.appName }}</strong>
            <p class="brand-subtitle">Nền tảng tổng hợp việc làm</p>
          </div>
        </div>

        <nav class="main-nav">
          <a routerLink="/" routerLinkActive="nav-active" [routerLinkActiveOptions]="{ exact: true }">Trang chủ</a>
          <a routerLink="/viec-lam" routerLinkActive="nav-active">Việc làm</a>
          <a routerLink="/cv" routerLinkActive="nav-active">CV của tôi</a>
          <a routerLink="/dang-nhap" routerLinkActive="nav-active" *ngIf="!authService.isAuthenticated()">Đăng nhập</a>
          <a routerLink="/dang-ky" routerLinkActive="nav-active" *ngIf="!authService.isAuthenticated()">Đăng ký</a>
        </nav>

        <div class="user-actions">
          <div class="account-pill" *ngIf="authService.currentUser() as user; else guestState">
            <span>{{ user.username }}</span>
            <button type="button" class="link-button" (click)="handleLogout()">Đăng xuất</button>
          </div>

          <ng-template #guestState>
            <a class="btn btn-secondary" routerLink="/dang-nhap">Truy cập tài khoản</a>
          </ng-template>
        </div>
      </header>

      <main class="content-shell">
        <router-outlet></router-outlet>
      </main>

      <footer class="site-footer">
        <div>
          <strong>{{ configService.snapshot.appName }}</strong>
          <p>Giao diện Angular SSR nhiều trang dành cho hệ thống tìm kiếm việc làm production.</p>
        </div>

        <div class="footer-meta">
          <span>{{ configService.snapshot.renderingMode }}</span>
          <span>{{ authService.isAuthenticated() ? 'Phiên đã xác thực' : 'Khách truy cập' }}</span>
        </div>
      </footer>
    </div>
  `,
  styleUrl: './app.css'
})
export class AppComponent implements OnInit {
  protected readonly configService = inject(AppConfigService);
  protected readonly authService = inject(AuthService);
  protected readonly cvStore = inject(CvStoreService);

  private readonly platformId = inject(PLATFORM_ID);

  async ngOnInit(): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    await this.configService.loadConfig();
    await this.authService.initialize();
    this.cvStore.initialize();
  }

  protected async handleLogout(): Promise<void> {
    await this.authService.logout();
  }
}
