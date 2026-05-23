import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AppConfigService } from './app-config.service';
import { SearchQueryState, SearchResponse, MIN_CRAWL_PAGES } from '../models';

@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly httpClient = inject(HttpClient);
  private readonly configService = inject(AppConfigService);

  async search(query: SearchQueryState): Promise<SearchResponse> {
    return await firstValueFrom(this.httpClient.post<SearchResponse>(
      `${this.configService.snapshot.apiGatewayBaseUrl}/api/jobs/search`,
      this.buildSearchPayload(query)
    ));
  }

  private buildSearchPayload(query: SearchQueryState): Record<string, unknown> {
    return {
      keyword: query.keyword.trim(),
      location: this.emptyToNull(query.location),
      tags: this.splitCsv(query.tagsText),
      sources: this.splitCsv(query.sourcesText),
      salaryMinMillions: this.toNumberOrNull(query.salaryMin),
      salaryMaxMillions: this.toNumberOrNull(query.salaryMax),
      postedWithinDays: this.toNumberOrNull(query.postedWithinDays),
      page: query.page,
      pageSize: query.pageSize,
      maxPages: query.maxPages || MIN_CRAWL_PAGES,
      sortBy: query.sortBy,
      // Control backend crawl behavior explicitly
      triggerCrawl: Boolean((query as any).triggerCrawl),
      filterOnly: Boolean((query as any).filterOnly)
    };
  }

  private splitCsv(value: string): string[] {
    return value
      .split(',')
      .map((item) => item.trim())
      .filter(Boolean);
  }

  private emptyToNull(value: string): string | null {
    const trimmed = value.trim();
    return trimmed ? trimmed : null;
  }

  private toNumberOrNull(value: string): number | null {
    if (!value) {
      return null;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
}
