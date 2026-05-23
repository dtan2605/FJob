import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CvStoreService } from '../services/cv-store.service';
import { AppConfigService } from '../services/app-config.service';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-cv-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="page-section single-column-shell">
      <div class="luxury-panel page-intro-card">
        <p class="eyebrow">Hồ sơ ứng tuyển</p>
        <h1>Quản lý CV — sang trọng và đơn giản</h1>
        <p class="section-note">Tải lên hoặc dán CV, hệ thống sẽ lưu an toàn và sử dụng khi bạn yêu cầu phân tích AI.
          Chức năng CV và AI chỉ khả dụng khi bạn đã đăng nhập.</p>
      </div>

      <div *ngIf="authService.isAuthenticated(); else loginCTA" class="luxury-panel cv-editor-card">
        <div class="field file-input-wrapper">
          <span>Tải lên CV</span>
          <div class="file-controls">
            <input #fileInput type="file" accept=".txt,.md,.html,.htm,.json,.rtf,.docx,.pdf" (change)="handleCvFileChange($event)" hidden>
            <button class="btn btn-secondary file-select-btn" type="button" (click)="fileInput.click()">Chọn tệp</button>
            <span class="file-name">{{ cvStore.cvFileName() || 'Chưa chọn tệp' }}</span>
          </div>
        </div>

        <label class="field">
          <span>Nội dung CV</span>
          <textarea
            name="cvText"
            rows="16"
            [(ngModel)]="cvTextDraft"
            placeholder="Dán nội dung CV tại đây..."></textarea>
        </label>

        <div class="cv-panel-meta">
          <span *ngIf="cvStore.cvFileName()">Tệp hiện tại: {{ cvStore.cvFileName() }}</span>
          <span *ngIf="!cvStore.cvFileName() && cvStore.hasCv()">Đang dùng CV đã nhập thủ công.</span>
          <span *ngIf="!cvStore.hasCv()">Chưa có CV nào được áp dụng.</span>
        </div>

        <div class="cv-actions cv-actions-compact">
          <div class="left-actions">
            <button class="btn btn-outline" type="button" (click)="clearCv()">Xóa</button>
          </div>
          <div class="right-actions">
            <button class="btn btn-secondary" type="button" (click)="applyCvText()">Lưu</button>
          </div>
        </div>
      </div>

      <ng-template #loginCTA>
        <div class="luxury-panel cv-editor-card">
          <p>Vui lòng đăng nhập để sử dụng chức năng CV và AI.</p>
          <a class="btn btn-primary" routerLink="/dang-nhap">Đăng nhập</a>
        </div>
      </ng-template>
    </section>
  `
})
export class CvPageComponent {
  protected readonly cvStore = inject(CvStoreService);
  protected readonly configService = inject(AppConfigService);
  protected readonly http = inject(HttpClient);
  protected readonly authService = inject(AuthService);
  protected cvTextDraft = '';

  private readonly platformId = inject(PLATFORM_ID);

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.cvStore.initialize();
      this.cvTextDraft = this.cvStore.cvText();
    }
  }

  protected async handleCvFileChange(event: Event): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const name = file.name ?? '';
    const form = new FormData();
    form.append('file', file);

    try {
      const headers = this.authService.getAccessToken()
        ? { headers: new HttpHeaders({ Authorization: `Bearer ${this.authService.getAccessToken()}` }) }
        : {};

      const resp = await firstValueFrom(this.http.post<any>(
        `${this.configService.snapshot.apiGatewayBaseUrl}/api/uploads/parse-cv`,
        form,
        headers
      ));

      const text = resp?.text ?? '';
      if (text) {
        this.cvTextDraft = text;
        this.cvStore.apply(text, name);
      } else {
        this.cvTextDraft = await file.text();
        this.cvStore.apply(this.cvTextDraft, name);
      }
    } catch {
      this.cvTextDraft = await file.text();
      this.cvStore.apply(this.cvTextDraft, name);
    }
  }

  protected applyCvText(): void {
    this.cvStore.apply(this.cvTextDraft, this.cvStore.cvFileName());
  }

  protected clearCv(): void {
    this.cvTextDraft = '';
    this.cvStore.clear();
  }
}
