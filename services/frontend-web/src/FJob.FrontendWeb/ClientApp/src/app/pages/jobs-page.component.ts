import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  CvAdviceResponse,
  DEFAULT_PAGE_SIZE,
  MAX_CRAWL_PAGES,
  MIN_CRAWL_PAGES,
  ReadinessState,
  SearchQueryState,
  SearchResultsState,
  SearchResultItem,
  StatusState
} from '../models';
import { SearchService } from '../services/search.service';
import { AppConfigService } from '../services/app-config.service';
import { CvStoreService } from '../services/cv-store.service';
import { CvAdviceService } from '../services/cv-advice.service';

@Component({
  selector: 'app-jobs-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="page-section search-shell">
      <aside class="luxury-panel filter-column">
        <div class="section-heading">
          <p class="eyebrow">Bộ lọc</p>
          <h1>Tìm việc làm</h1>
          <p class="section-note">Tùy chỉnh truy vấn, số trang crawl và sắp xếp theo nhu cầu của bạn.</p>
        </div>

        <form class="stack-form" (ngSubmit)="handleSubmit()">
          <label class="field">
            <span>Từ khóa</span>
            <input name="keyword" [(ngModel)]="query.keyword" placeholder="Ví dụ: python intern, .NET intern">
          </label>

          <label class="field">
            <span>Địa điểm</span>
            <input name="location" [(ngModel)]="query.location" placeholder="Hà Nội, Thành phố Hồ Chí Minh">
          </label>

          <label class="field">
            <span>Thẻ kỹ năng</span>
            <input name="tagsText" [(ngModel)]="query.tagsText" placeholder="python, intern, backend">
          </label>

          <label class="field">
            <span>Nguồn dữ liệu</span>
            <input name="sourcesText" [(ngModel)]="query.sourcesText" placeholder="TopCV, Indeed, Vieclam24h, Careerviet">
          </label>

          <label class="field">
            <span>Số trang crawl</span>
            <div class="range-wrap">
              <input type="range" name="maxPages" [min]="minCrawlPages" [max]="maxCrawlPages" [(ngModel)]="query.maxPages">
              <div class="range-caption">
                <strong>{{ query.maxPages }} trang</strong>
                <span>Giảm số trang để phản hồi nhanh hơn, tăng số trang để phủ dữ liệu rộng hơn.</span>
              </div>
            </div>
          </label>

          <div class="form-grid">
            <label class="field">
              <span>Lương từ (triệu)</span>
              <input type="number" min="0" step="1" name="salaryMin" [(ngModel)]="query.salaryMin">
            </label>

            <label class="field">
              <span>Lương đến (triệu)</span>
              <input type="number" min="0" step="1" name="salaryMax" [(ngModel)]="query.salaryMax">
            </label>
          </div>

          <div class="form-grid">
            <label class="field">
              <span>Thời gian đăng</span>
              <select name="postedWithinDays" [(ngModel)]="query.postedWithinDays">
                <option value="">Tất cả</option>
                <option value="7">Trong 7 ngày</option>
                <option value="14">Trong 14 ngày</option>
                <option value="30">Trong 30 ngày</option>
              </select>
            </label>

            <label class="field">
              <span>Sắp xếp</span>
              <select name="sortBy" [(ngModel)]="query.sortBy">
                <option value="relevance">Liên quan</option>
                <option value="recent">Mới nhất</option>
              </select>
            </label>
          </div>

          <div class="luxury-panel cv-panel">
            <div class="section-heading section-heading-compact">
              <p class="eyebrow">CV matching</p>
              <h2>So sánh CV với việc làm</h2>
              <p class="section-note">Nhập CV một lần để hệ thống tính tỷ lệ phù hợp và đưa ra góp ý cải thiện cho từng công việc.</p>
            </div>

            <label class="field">
              <span>Tải lên CV</span>
              <input type="file" accept=".txt,.md,.html,.htm,.json,.rtf,.docx,.pdf" (change)="handleCvFileChange($event)">
            </label>

            <label class="field">
              <span>Hoặc dán nội dung CV</span>
              <textarea
                name="cvText"
                rows="8"
                [(ngModel)]="cvTextDraft"
                placeholder="Dán nội dung CV của bạn tại đây để so sánh với công việc..."></textarea>
            </label>

            <div class="cv-panel-meta">
              <span *ngIf="cvStore.cvFileName()">Tệp hiện tại: {{ cvStore.cvFileName() }}</span>
              <span *ngIf="!cvStore.cvFileName() && cvStore.hasCv()">Đang dùng nội dung CV đã nhập thủ công.</span>
              <span *ngIf="!cvStore.hasCv()">Chưa có CV để so sánh.</span>
            </div>

            <div class="cv-actions">
              <button class="btn btn-secondary" type="button" (click)="applyCvText()">Áp dụng CV</button>
              <button class="btn btn-secondary" type="button" (click)="clearCv()">Xóa CV</button>
              <a class="btn btn-secondary" routerLink="/cv">Mở trang CV</a>
            </div>
          </div>

          <button class="btn btn-primary btn-block" type="submit" [disabled]="isLoading">
            {{ isLoading ? 'Đang tìm kiếm...' : 'Tìm việc ngay' }}
          </button>
        </form>
      </aside>

      <section class="result-column">
        <div class="luxury-panel result-header-card">
          <div>
            <p class="eyebrow">Kết quả</p>
            <h2>Danh sách việc làm</h2>
            <p class="section-note">{{ buildSummary() }}</p>
          </div>

          <div class="meta-badges">
            <span class="pill">{{ readiness.ready === false ? 'Hệ thống suy giảm' : 'Hệ thống sẵn sàng' }}</span>
            <span class="pill">{{ query.sortBy === 'relevance' ? 'Ưu tiên liên quan' : 'Ưu tiên mới nhất' }}</span>
          </div>
        </div>

        <div class="status-panel" [ngClass]="'status-' + status.tone">
          <strong>{{ status.title }}</strong>
          <p>{{ status.message }}</p>
        </div>

        <div class="luxury-panel empty-panel" *ngIf="isLoading">
          <h3>Đang tải kết quả</h3>
          <p>Hệ thống đang gọi API Gateway, đồng bộ crawl và truy vấn chỉ mục tìm kiếm.</p>
        </div>

        <div class="luxury-panel empty-panel" *ngIf="!isLoading && !hasSearched">
          <h3>Chưa có truy vấn</h3>
          <p>Nhập từ khóa hoặc chọn bộ lọc để bắt đầu một lần tìm kiếm mới.</p>
        </div>

        <div class="luxury-panel empty-panel" *ngIf="!isLoading && hasSearched && !results.items.length">
          <h3>Không tìm thấy việc phù hợp</h3>
          <p>Hãy nới lỏng bộ lọc, thay đổi từ khóa hoặc tăng số trang crawl để mở rộng kết quả.</p>
        </div>

        <div class="job-list" *ngIf="!isLoading && results.items.length">
          <article class="luxury-panel job-card" *ngFor="let item of results.items; trackBy: trackByJob">
            <div class="job-head">
              <div>
                <p class="job-kicker">{{ item.company }} · {{ item.source }}</p>
                <h3>{{ item.title }}</h3>
              </div>
              <span class="score-tag">Điểm {{ formatScore(item.score) }}</span>
            </div>

            <div class="match-row">
              <span class="match-tag" *ngIf="cvStore.hasCv(); else noCvTag">
                Mức phù hợp CV: {{ formatMatchPercent(item) }}
              </span>
              <ng-template #noCvTag>
                <span class="match-tag match-tag-muted">Chưa có CV để so sánh</span>
              </ng-template>
            </div>

            <p class="job-meta">{{ item.location || 'Chưa rõ địa điểm' }} · {{ item.salary || 'Thỏa thuận' }} · Đăng {{ formatDate(item.postedAtUtc) }}</p>
            <p class="job-description">{{ item.description || 'Chưa có mô tả chi tiết cho vị trí này.' }}</p>

            <div class="chip-row" *ngIf="item.tags?.length">
              <span class="chip" *ngFor="let tag of item.tags">{{ tag }}</span>
            </div>

            <div class="job-actions job-actions-rich">
              <a class="btn btn-secondary" [href]="item.url" target="_blank" rel="noreferrer">Xem bài đăng gốc</a>
              <button class="btn btn-secondary" type="button" (click)="toggleAdvice(item)" [disabled]="!cvStore.hasCv() || isAdviceLoading(item)">
                {{ adviceLabel(item) }}
              </button>
            </div>

            <div class="luxury-panel advice-panel" *ngIf="isAdviceExpanded(item)">
              <div *ngIf="isAdviceLoading(item)" class="advice-loading">Đang phân tích CV cho công việc này...</div>
              <div *ngIf="adviceError(item)" class="advice-error">{{ adviceError(item) }}</div>

              <div *ngIf="adviceFor(item) as advice" class="advice-content">
                <div class="advice-header">
                  <span class="match-tag">AI đánh giá: {{ advice.matchPercent }}%</span>
                </div>

                <p class="advice-summary">{{ advice.summary }}</p>

                <div class="advice-grid">
                  <div>
                    <h4>Điểm mạnh nên giữ</h4>
                    <ul class="advice-list">
                      <li *ngFor="let strength of advice.strengths">{{ strength }}</li>
                    </ul>
                  </div>

                  <div>
                    <h4>Kỹ năng còn thiếu</h4>
                    <ul class="advice-list">
                      <li *ngFor="let missing of advice.missingSkills">{{ missing }}</li>
                    </ul>
                  </div>
                </div>

                <div>
                  <h4>Nên cải thiện gì trong CV</h4>
                  <ul class="advice-list">
                    <li *ngFor="let improvement of advice.improvements">{{ improvement }}</li>
                  </ul>
                </div>
              </div>
            </div>
          </article>
        </div>

        <div class="pagination" *ngIf="hasSearched && results.items.length">
          <button class="btn btn-secondary" type="button" (click)="handlePreviousPage()" [disabled]="!canGoPrevious">Trang trước</button>
          <button
            class="page-number"
            type="button"
            *ngFor="let page of pageNumbers()"
            [class.page-number-active]="page === query.page"
            (click)="goToPage(page)">
            {{ page }}
          </button>
          <button class="btn btn-secondary" type="button" (click)="handleNextPage()" [disabled]="!canGoNext">Trang sau</button>
        </div>
      </section>
    </section>
  `
})
export class JobsPageComponent implements OnInit {
  protected readonly minCrawlPages = MIN_CRAWL_PAGES;
  protected readonly maxCrawlPages = MAX_CRAWL_PAGES;

  protected query: SearchQueryState = this.createDefaultQuery();
  protected results: SearchResultsState = {
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: DEFAULT_PAGE_SIZE
  };
  protected readiness: ReadinessState = {
    ready: null,
    message: 'Đang kiểm tra hệ thống...'
  };
  protected status: StatusState = {
    tone: 'muted',
    title: 'Sẵn sàng',
    message: 'Thiết lập bộ lọc rồi bắt đầu tìm kiếm việc làm.'
  };
  protected isLoading = false;
  protected hasSearched = false;
  protected cvTextDraft = '';

  protected readonly searchService = inject(SearchService);
  protected readonly configService = inject(AppConfigService);
  protected readonly cvStore = inject(CvStoreService);
  protected readonly cvAdviceService = inject(CvAdviceService);

  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly platformId = inject(PLATFORM_ID);

  private readonly adviceStateByJob: Record<string, CvAdviceResponse | undefined> = {};
  private readonly adviceLoadingStateByJob: Record<string, boolean | undefined> = {};
  private readonly adviceErrorStateByJob: Record<string, string | undefined> = {};
  private readonly adviceExpandedStateByJob: Record<string, boolean | undefined> = {};

  async ngOnInit(): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.cvStore.initialize();
    this.cvTextDraft = this.cvStore.cvText();
    this.readiness = await this.configService.loadReadiness();
    this.route.queryParams.subscribe(() => {
      this.query = this.readQueryFromRoute();
    });
  }

  protected async handleSubmit(): Promise<void> {
    const nextQuery = { ...this.query, page: 1 };
    await this.navigateWithQuery(nextQuery);
    await this.runSearch(nextQuery);
  }

  protected async handlePreviousPage(): Promise<void> {
    if (!this.canGoPrevious) {
      return;
    }

    const nextQuery = { ...this.query, page: this.query.page - 1 };
    await this.navigateWithQuery(nextQuery);
    await this.runSearch(nextQuery);
  }

  protected async handleNextPage(): Promise<void> {
    if (!this.canGoNext) {
      return;
    }

    const nextQuery = { ...this.query, page: this.query.page + 1 };
    await this.navigateWithQuery(nextQuery);
    await this.runSearch(nextQuery);
  }

  protected async goToPage(page: number): Promise<void> {
    if (page === this.query.page || page < 1 || this.isLoading) {
      return;
    }

    const nextQuery = { ...this.query, page };
    await this.navigateWithQuery(nextQuery);
    await this.runSearch(nextQuery);
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
        this.resetAdviceState();
        return;
      }

      const payload = await resp.json();
      const text = payload?.text ?? '';
      this.cvTextDraft = text;
      this.cvStore.apply(text, name);
      this.resetAdviceState();
    } catch {
      this.cvTextDraft = await file.text();
      this.cvStore.apply(this.cvTextDraft, name);
      this.resetAdviceState();
    }
  }

  protected applyCvText(): void {
    this.cvStore.apply(this.cvTextDraft, this.cvStore.cvFileName());
    this.resetAdviceState();
  }

  protected clearCv(): void {
    this.cvTextDraft = '';
    this.cvStore.clear();
    this.resetAdviceState();
  }

  protected formatMatchPercent(item: SearchResultItem): string {
    const score = this.cvStore.computeMatch(item);
    return `${Math.round(score * 100)}%`;
  }

  protected async toggleAdvice(item: SearchResultItem): Promise<void> {
    if (!this.cvStore.hasCv()) {
      return;
    }

    const key = this.jobKey(item);
    const isExpanded = this.adviceExpandedStateByJob[key] === true;
    this.adviceExpandedStateByJob[key] = !isExpanded;

    if (isExpanded || this.adviceStateByJob[key] || this.adviceLoadingStateByJob[key]) {
      return;
    }

    this.adviceLoadingStateByJob[key] = true;
    this.adviceErrorStateByJob[key] = undefined;

    try {
      this.adviceStateByJob[key] = await this.cvAdviceService.getAdvice({
        cvText: this.cvStore.cvText(),
        jobTitle: item.title ?? '',
        company: item.company ?? '',
        location: item.location ?? '',
        description: item.description ?? '',
        tags: item.tags ?? []
      });
    } catch {
      this.adviceErrorStateByJob[key] = 'Không thể tạo góp ý AI cho công việc này ở thời điểm hiện tại.';
    } finally {
      this.adviceLoadingStateByJob[key] = false;
    }
  }

  protected adviceLabel(item: SearchResultItem): string {
    if (!this.cvStore.hasCv()) {
      return 'Cần nhập CV trước';
    }

    return this.isAdviceExpanded(item) ? 'Ẩn góp ý AI' : 'AI góp ý CV';
  }

  protected isAdviceExpanded(item: SearchResultItem): boolean {
    return this.adviceExpandedStateByJob[this.jobKey(item)] === true;
  }

  protected isAdviceLoading(item: SearchResultItem): boolean {
    return this.adviceLoadingStateByJob[this.jobKey(item)] === true;
  }

  protected adviceFor(item: SearchResultItem): CvAdviceResponse | undefined {
    return this.adviceStateByJob[this.jobKey(item)];
  }

  protected adviceError(item: SearchResultItem): string | undefined {
    return this.adviceErrorStateByJob[this.jobKey(item)];
  }

  protected trackByJob = (_index: number, item: SearchResultItem): string => {
    return this.jobKey(item);
  };

  protected get canGoPrevious(): boolean {
    return this.query.page > 1 && !this.isLoading;
  }

  protected get canGoNext(): boolean {
    return !this.isLoading && this.query.page * this.results.pageSize < this.results.totalCount;
  }

  protected buildSummary(): string {
    if (this.isLoading) {
      return 'Đang truy vấn dữ liệu mới nhất từ hệ thống.';
    }

    if (!this.hasSearched) {
      return 'Truy vấn sẽ được lưu trên URL để bạn có thể chia sẻ lại cấu hình tìm kiếm.';
    }

    return `Tìm thấy ${this.results.totalCount} kết quả, đang hiển thị ${this.results.items.length} việc làm trên trang này.`;
  }

  protected formatDate(value?: string | null): string {
    if (!value) {
      return 'không rõ ngày';
    }

    return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(value));
  }

  protected formatScore(value?: number | null): string {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed.toFixed(2) : '--';
  }

  protected pageNumbers(): number[] {
    const totalPages = Math.max(1, Math.ceil((this.results.totalCount || 0) / (this.results.pageSize || DEFAULT_PAGE_SIZE)));
    const current = Math.min(this.query.page, totalPages);
    const start = Math.max(1, current - 2);
    const end = Math.min(totalPages, start + 4);
    const adjustedStart = Math.max(1, end - 4);
    const pages: number[] = [];

    for (let page = adjustedStart; page <= end; page += 1) {
      pages.push(page);
    }

    return pages;
  }

  private async navigateWithQuery(query: SearchQueryState): Promise<void> {
    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: this.toQueryParams(query)
    });
  }

  private async runSearch(nextQuery: SearchQueryState, updateStatus = true): Promise<void> {
    this.isLoading = true;
    this.resetAdviceState();

    if (updateStatus) {
      this.status = {
        tone: 'info',
        title: 'Đang tìm kiếm',
        message: 'Frontend đang đồng bộ crawl và truy vấn chỉ mục việc làm.'
      };
    }

    try {
      const data = await this.searchService.search(nextQuery);
      this.results = {
        items: data.items ?? [],
        totalCount: data.totalCount ?? 0,
        page: data.page ?? nextQuery.page,
        pageSize: data.pageSize ?? nextQuery.pageSize
      };
      this.query = nextQuery;
      this.hasSearched = true;
      this.status = {
        tone: 'success',
        title: 'Đã cập nhật kết quả',
        message: `Đã nhận ${this.results.items.length} việc làm trên tổng ${this.results.totalCount} kết quả.`
      };
    } catch (error: unknown) {
      this.results = {
        items: [],
        totalCount: 0,
        page: nextQuery.page,
        pageSize: nextQuery.pageSize
      };
      this.query = nextQuery;
      this.hasSearched = true;
      this.status = {
        tone: 'danger',
        title: 'Tìm kiếm gặp sự cố',
        message: this.extractErrorMessage(error)
      };
    } finally {
      this.isLoading = false;
    }
  }

  private extractErrorMessage(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'status' in error) {
      const httpError = error as { status?: number };
      if (httpError.status === 429) {
        return 'Hệ thống đang giới hạn tần suất truy vấn. Vui lòng thử lại sau ít phút.';
      }

      if (httpError.status) {
        return `Gateway trả về lỗi ${httpError.status}.`;
      }
    }

    return 'Không thể tải dữ liệu tìm kiếm ở thời điểm này.';
  }

  private readQueryFromRoute(): SearchQueryState {
    const params = this.route.snapshot.queryParamMap;
    return {
      keyword: params.get('keyword') ?? '',
      location: params.get('location') ?? '',
      tagsText: params.get('tags') ?? '',
      sourcesText: params.get('sources') ?? '',
      salaryMin: params.get('salaryMin') ?? '',
      salaryMax: params.get('salaryMax') ?? '',
      postedWithinDays: params.get('postedWithinDays') ?? '',
      sortBy: params.get('sortBy') ?? 'relevance',
      maxPages: this.clamp(this.positiveInt(params.get('maxPages'), MIN_CRAWL_PAGES), MIN_CRAWL_PAGES, MAX_CRAWL_PAGES),
      page: this.positiveInt(params.get('page'), 1),
      pageSize: this.positiveInt(params.get('pageSize'), DEFAULT_PAGE_SIZE)
    };
  }

  private toQueryParams(query: SearchQueryState): Record<string, string | number | null> {
    return {
      keyword: query.keyword || null,
      location: query.location || null,
      tags: query.tagsText || null,
      sources: query.sourcesText || null,
      salaryMin: query.salaryMin || null,
      salaryMax: query.salaryMax || null,
      postedWithinDays: query.postedWithinDays || null,
      sortBy: query.sortBy !== 'relevance' ? query.sortBy : null,
      maxPages: query.maxPages !== MIN_CRAWL_PAGES ? query.maxPages : null,
      page: query.page > 1 ? query.page : null,
      pageSize: query.pageSize !== DEFAULT_PAGE_SIZE ? query.pageSize : null
    };
  }

  private hasMeaningfulQuery(query: SearchQueryState): boolean {
    return Boolean(
      query.keyword.trim() ||
      query.location.trim() ||
      query.tagsText.trim() ||
      query.sourcesText.trim() ||
      query.salaryMin ||
      query.salaryMax ||
      query.postedWithinDays
    );
  }

  private createDefaultQuery(): SearchQueryState {
    return {
      keyword: '',
      location: '',
      tagsText: '',
      sourcesText: '',
      salaryMin: '',
      salaryMax: '',
      postedWithinDays: '',
      sortBy: 'relevance',
      maxPages: MIN_CRAWL_PAGES,
      page: 1,
      pageSize: DEFAULT_PAGE_SIZE
    };
  }

  private positiveInt(value: string | null, fallback: number): number {
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
  }

  private clamp(value: number, min: number, max: number): number {
    return Math.min(max, Math.max(min, value));
  }

  private jobKey(item: SearchResultItem): string {
    return item.jobId ?? item.id ?? item.url ?? `${item.source}-${item.title}`;
  }

  private resetAdviceState(): void {
    for (const key of Object.keys(this.adviceStateByJob)) {
      delete this.adviceStateByJob[key];
    }

    for (const key of Object.keys(this.adviceLoadingStateByJob)) {
      delete this.adviceLoadingStateByJob[key];
    }

    for (const key of Object.keys(this.adviceErrorStateByJob)) {
      delete this.adviceErrorStateByJob[key];
    }

    for (const key of Object.keys(this.adviceExpandedStateByJob)) {
      delete this.adviceExpandedStateByJob[key];
    }
  }
}
