import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="auth-shell">
      <div class="auth-hero">
        <p class="eyebrow">Đăng nhập</p>
        <h1>Truy cập không gian tìm việc cá nhân của bạn.</h1>
        <p>
          Đăng nhập để lưu trạng thái sử dụng, chuẩn bị cho các luồng cá nhân hóa và quản trị truy cập về sau.
        </p>
      </div>

      <form class="luxury-panel auth-card" (ngSubmit)="handleSubmit()">
        <div class="section-heading">
          <h2>Chào mừng quay lại</h2>
          <p class="section-note">Nhập thông tin tài khoản để tiếp tục.</p>
        </div>

        <label class="field">
          <span>Tên đăng nhập</span>
          <input name="username" [(ngModel)]="username" autocomplete="username" required>
        </label>

        <label class="field">
          <span>Mật khẩu</span>
          <input type="password" name="password" [(ngModel)]="password" autocomplete="current-password" required>
        </label>

        <div class="status-panel" *ngIf="message" [ngClass]="'status-' + tone">
          <strong>{{ tone === 'danger' ? 'Không thể đăng nhập' : 'Thông báo' }}</strong>
          <p>{{ message }}</p>
        </div>

        <button class="btn btn-primary btn-block" type="submit" [disabled]="isLoading">
          {{ isLoading ? 'Đang xác thực...' : 'Đăng nhập' }}
        </button>

        <p class="auth-switch">
          Chưa có tài khoản?
          <a routerLink="/dang-ky">Đăng ký ngay</a>
        </p>
      </form>
    </section>
  `
})
export class LoginPageComponent {
  protected username = '';
  protected password = '';
  protected message = '';
  protected tone: 'success' | 'danger' = 'success';
  protected isLoading = false;

  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected async handleSubmit(): Promise<void> {
    this.isLoading = true;
    this.message = '';

    try {
      await this.authService.login({
        username: this.username.trim(),
        password: this.password
      });

      this.tone = 'success';
      this.message = 'Đăng nhập thành công. Bạn đang được chuyển tới trang việc làm.';
      await this.router.navigateByUrl('/viec-lam');
    } catch (error: unknown) {
      this.tone = 'danger';
      this.message = this.extractErrorMessage(error);
    } finally {
      this.isLoading = false;
    }
  }

  private extractErrorMessage(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'status' in error) {
      const httpError = error as { status?: number };
      if (httpError.status === 401) {
        return 'Tên đăng nhập hoặc mật khẩu chưa chính xác.';
      }
    }

    return 'Đăng nhập thất bại. Vui lòng thử lại.';
  }
}
