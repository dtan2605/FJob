import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-register-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="auth-shell">
      <div class="auth-hero">
        <p class="eyebrow">Đăng ký</p>
        <h1>Tạo tài khoản mới để bắt đầu trải nghiệm tìm việc chuyên nghiệp hơn.</h1>
        <p>
          Tài khoản ứng viên giúp bạn duy trì phiên làm việc, chuẩn bị cho các tính năng lưu tìm kiếm
          và cá nhân hóa trong các bước tiếp theo của hệ thống.
        </p>
      </div>

      <form class="luxury-panel auth-card" (ngSubmit)="handleSubmit()">
        <div class="section-heading">
          <h2>Tạo tài khoản</h2>
          <p class="section-note">Điền thông tin cơ bản để khởi tạo tài khoản ứng viên.</p>
        </div>

        <label class="field">
          <span>Tên đăng nhập</span>
          <input name="username" [(ngModel)]="username" autocomplete="username" required>
        </label>

        <label class="field">
          <span>Mật khẩu</span>
          <input type="password" name="password" [(ngModel)]="password" autocomplete="new-password" required>
        </label>

        <label class="field">
          <span>Xác nhận mật khẩu</span>
          <input type="password" name="confirmPassword" [(ngModel)]="confirmPassword" autocomplete="new-password" required>
        </label>

        <div class="status-panel" *ngIf="message" [ngClass]="'status-' + tone">
          <strong>{{ tone === 'danger' ? 'Không thể đăng ký' : 'Đăng ký thành công' }}</strong>
          <p>{{ message }}</p>
        </div>

        <button class="btn btn-primary btn-block" type="submit" [disabled]="isLoading">
          {{ isLoading ? 'Đang tạo tài khoản...' : 'Đăng ký tài khoản' }}
        </button>

        <p class="auth-switch">
          Đã có tài khoản?
          <a routerLink="/dang-nhap">Đăng nhập</a>
        </p>
      </form>
    </section>
  `
})
export class RegisterPageComponent {
  protected username = '';
  protected password = '';
  protected confirmPassword = '';
  protected message = '';
  protected tone: 'success' | 'danger' = 'success';
  protected isLoading = false;

  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected async handleSubmit(): Promise<void> {
    if (this.password !== this.confirmPassword) {
      this.tone = 'danger';
      this.message = 'Mật khẩu xác nhận không khớp.';
      return;
    }

    this.isLoading = true;
    this.message = '';

    try {
      await this.authService.register({
        username: this.username.trim(),
        password: this.password,
        confirmPassword: this.confirmPassword
      });

      this.tone = 'success';
      this.message = 'Tạo tài khoản thành công. Bạn có thể đăng nhập ngay bây giờ.';
      await this.router.navigateByUrl('/dang-nhap');
    } catch (error: unknown) {
      this.tone = 'danger';
      this.message = this.extractErrorMessage(error);
    } finally {
      this.isLoading = false;
    }
  }

  private extractErrorMessage(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'status' in error) {
      const httpError = error as { status?: number; error?: { message?: string } };
      if (httpError.status === 409) {
        return 'Tên đăng nhập đã tồn tại. Hãy chọn tên khác.';
      }
    }

    return 'Không thể tạo tài khoản vào lúc này.';
  }
}
