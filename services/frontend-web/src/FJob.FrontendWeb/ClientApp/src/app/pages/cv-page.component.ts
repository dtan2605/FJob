import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, PLATFORM_ID, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CvStoreService } from '../services/cv-store.service';
import { AppConfigService } from '../services/app-config.service';

@Component({
  selector: 'app-cv-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="page-section single-column-shell">
      <div class="luxury-panel page-intro-card">
        <p class="eyebrow">Hồ sơ ứng tuyển</p>
        <h1>Nhập CV để so sánh với từng công việc</h1>
        <p class="section-note">
          Tải CV dạng văn bản hoặc dán nội dung trực tiếp. Hệ thống sẽ dùng CV này để tính tỷ lệ phù hợp
          ngay trong mỗi thẻ việc làm, hiển thị bên dưới điểm score.
        </p>
      </div>

      <div class="luxury-panel cv-editor-card">
        <label class="field">
          <span>Tải lên CV</span>
          <input type="file" accept=".txt,.md,.html,.htm,.json,.rtf,.docx,.pdf" (change)="handleCvFileChange($event)">
        </label>

        <label class="field">
          <span>Nội dung CV</span>
          <textarea
            name="cvText"
            rows="16"
            [(ngModel)]="cvTextDraft"
            placeholder="Dán nội dung CV của bạn tại đây để hệ thống đối chiếu với việc làm..."></textarea>
        </label>

        <div class="cv-panel-meta">
          <span *ngIf="cvStore.cvFileName()">Tệp hiện tại: {{ cvStore.cvFileName() }}</span>
          <span *ngIf="!cvStore.cvFileName() && cvStore.hasCv()">Đang dùng CV đã nhập thủ công.</span>
          <span *ngIf="!cvStore.hasCv()">Chưa có CV nào được áp dụng.</span>
        </div>

        <div class="cv-actions">
          <button class="btn btn-primary" type="button" (click)="applyCvText()">Lưu CV</button>
          <button class="btn btn-secondary" type="button" (click)="clearCv()">Xóa CV</button>
        </div>
      </div>
    </section>
  `
})
export class CvPageComponent {
  protected readonly cvStore = inject(CvStoreService);
  protected readonly configService = inject(AppConfigService);
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
      const resp = await fetch(`${this.configService.snapshot.apiGatewayBaseUrl}/api/uploads/parse-cv`, {
        method: 'POST',
        body: form,
        credentials: 'include'
      });

      if (!resp.ok) {
        this.cvTextDraft = await file.text();
        this.cvStore.apply(this.cvTextDraft, name);
        return;
      }

      const payload = await resp.json();
      const text = payload?.text ?? '';
      this.cvTextDraft = text;
      this.cvStore.apply(text, name);
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
