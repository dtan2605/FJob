import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { SearchResultItem } from '../models';

const CV_STORAGE_KEY = 'fjob.cv_text';
const CV_NAME_STORAGE_KEY = 'fjob.cv_name';

@Injectable({ providedIn: 'root' })
export class CvStoreService {
  private readonly platformId = inject(PLATFORM_ID);

  readonly cvText = signal('');
  readonly cvFileName = signal('');

  initialize(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.cvText.set(localStorage.getItem(CV_STORAGE_KEY) ?? '');
    this.cvFileName.set(localStorage.getItem(CV_NAME_STORAGE_KEY) ?? '');
  }

  apply(text: string, fileName = ''): void {
    const normalizedText = text.trim();
    this.cvText.set(normalizedText);
    this.cvFileName.set(normalizedText ? fileName : '');
    this.persist();
  }

  clear(): void {
    this.cvText.set('');
    this.cvFileName.set('');
    this.persist();
  }

  hasCv(): boolean {
    return this.cvText().trim().length > 0;
  }

  computeMatch(item: SearchResultItem): number {
    const activeCvText = this.cvText().trim();
    if (!activeCvText) {
      return 0;
    }

    const cvTokens = this.tokenize(activeCvText);
    if (cvTokens.size === 0) {
      return 0;
    }

    const titleTokens = this.tokenize(item.title);
    const tagTokens = this.tokenize((item.tags ?? []).join(' '));
    const descriptionTokens = this.tokenize(item.description);
    const metaTokens = this.tokenize(`${item.company} ${item.location}`);

    const titleScore = this.overlapRatio(cvTokens, titleTokens);
    const tagScore = this.overlapRatio(cvTokens, tagTokens);
    const descriptionScore = this.overlapRatio(cvTokens, descriptionTokens);
    const metaScore = this.overlapRatio(cvTokens, metaTokens);

    const weighted = (titleScore * 0.4) + (tagScore * 0.25) + (descriptionScore * 0.25) + (metaScore * 0.1);
    return Math.min(1, Math.max(0, weighted));
  }

  private persist(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    localStorage.setItem(CV_STORAGE_KEY, this.cvText());
    localStorage.setItem(CV_NAME_STORAGE_KEY, this.cvFileName());
  }

  private overlapRatio(cvTokens: Set<string>, jobTokens: Set<string>): number {
    if (jobTokens.size === 0) {
      return 0;
    }

    let matches = 0;
    jobTokens.forEach((token) => {
      if (cvTokens.has(token)) {
        matches += 1;
      }
    });

    return matches / jobTokens.size;
  }

  private tokenize(value: string): Set<string> {
    const normalized = value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/[^a-z0-9+#.\s]/g, ' ');

    return new Set(
      normalized
        .split(/\s+/)
        .map((token) => token.trim())
        .filter((token) => token.length >= 2)
    );
  }
}
