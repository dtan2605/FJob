import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppConfigService } from '../services/app-config.service';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <section class="page-section hero-grid">
      <div class="hero-copy">
        <p class="eyebrow">Tìm việc dễ dàng</p>
        <h1>Giao diện đen-đỏ, rõ nét, tiết kiệm thời gian của bạn.</h1>
        <p class="hero-text">
          FJob giúp bạn tìm việc nhanh chóng với tìm kiếm đơn giản, kết quả dễ đọc và thao tác trực quan.
        </p>

        <div class="hero-actions">
          <a class="btn btn-primary" routerLink="/viec-lam">Tìm việc ngay</a>
          <a class="btn btn-secondary" routerLink="/cv">Sửa CV</a>
          <a class="btn btn-secondary" routerLink="/dang-nhap">Đăng nhập</a>
        </div>
      </div>

      <div class="hero-card luxury-panel">
        <div class="hero-stat">
          <span>Ứng dụng</span>
          <strong>{{ configService.snapshot.appName }}</strong>
        </div>
        <div class="hero-stat">
          <span>Chế độ hiển thị</span>
          <strong>{{ configService.snapshot.renderingMode }}</strong>
        </div>
        <div class="hero-stat">
          <span>Trạng thái tài khoản</span>
          <strong>{{ authService.isAuthenticated() ? 'Đã đăng nhập' : 'Chưa đăng nhập' }}</strong>
        </div>
      </div>
    </section>

    <section class="page-section section-grid">
      <article class="luxury-panel feature-card">
        <p class="eyebrow">01</p>
        <h2>Giao diện dễ đọc</h2>
        <p>Chữ rõ ràng, bảng mục kéo thả tối giản, không rườm rà để bạn tập trung chọn đúng việc.</p>
      </article>

      <article class="luxury-panel feature-card">
        <p class="eyebrow">02</p>
        <h2>Tập trung vào kết quả</h2>
        <p>Thông tin mỗi job được hiển thị súc tích, chỉ giữ những nội dung quan trọng nhất.</p>
      </article>

      <article class="luxury-panel feature-card">
        <p class="eyebrow">03</p>
        <h2>Quản lý CV nhanh</h2>
        <p>Nhập hoặc dán CV dễ dàng, hệ thống gợi ý phù hợp với việc làm ngay lập tức.</p>
      </article>
    </section>
  `
})
export class HomePageComponent {
  protected readonly configService = inject(AppConfigService);
  protected readonly authService = inject(AuthService);
}
