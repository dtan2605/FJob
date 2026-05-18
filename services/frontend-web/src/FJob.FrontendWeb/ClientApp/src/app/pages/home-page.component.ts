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
        <p class="eyebrow">Nền tảng tìm việc thông minh</p>
        <h1>Tìm đúng việc nhanh hơn với trải nghiệm gọn gàng và đáng tin cậy.</h1>
        <p class="hero-text">
          FJob kết nối hệ thống crawl, lập chỉ mục và tìm kiếm để gom việc làm từ nhiều nguồn về một nơi,
          giúp bạn theo dõi cơ hội mới với bộ lọc rõ ràng và giao diện dễ dùng.
        </p>

        <div class="hero-actions">
          <a class="btn btn-primary" routerLink="/viec-lam">Khám phá việc làm</a>
          <a class="btn btn-secondary" routerLink="/cv">Nhập CV của bạn</a>
          <a class="btn btn-secondary" routerLink="/dang-ky">Tạo tài khoản</a>
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
        <h2>Tìm kiếm theo từ khóa và bộ lọc</h2>
        <p>
          Từ vị trí, mức lương đến nguồn dữ liệu, mọi truy vấn đều được gom về một giao diện tìm kiếm
          duy nhất để bạn thao tác nhanh và dễ đối chiếu.
        </p>
      </article>

      <article class="luxury-panel feature-card">
        <p class="eyebrow">02</p>
        <h2>Crawl có kiểm soát</h2>
        <p>
          Hệ thống cho phép giới hạn số trang crawl để cân bằng giữa độ phủ dữ liệu và tốc độ phản hồi
          thực tế cho từng lần tìm kiếm.
        </p>
      </article>

      <article class="luxury-panel feature-card">
        <p class="eyebrow">03</p>
        <h2>So khớp CV ngay trên từng job</h2>
        <p>
          CV của bạn có thể được nhập một lần và dùng lại trong toàn bộ phiên, giúp đối chiếu nhanh
          mức độ phù hợp với từng vị trí ngay ở danh sách kết quả.
        </p>
      </article>
    </section>
  `
})
export class HomePageComponent {
  protected readonly configService = inject(AppConfigService);
  protected readonly authService = inject(AuthService);
}
