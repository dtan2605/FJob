export interface AppConfig {
  apiGatewayBaseUrl: string;
  appName: string;
  renderingMode: string;
}

export interface SearchResultItem {
  id?: string;
  jobId?: string;
  title: string;
  company: string;
  source: string;
  url: string;
  location: string;
  salary: string;
  description: string;
  salaryMinMillions?: number | null;
  salaryMaxMillions?: number | null;
  tags: string[];
  postedAtUtc?: string | null;
  score?: number | null;
}

export interface CvAdviceRequest {
  cvText: string;
  jobTitle: string;
  company: string;
  location: string;
  description: string;
  tags: string[];
}

export interface CvAdviceResponse {
  matchPercent: number;
  summary: string;
  strengths: string[];
  missingSkills: string[];
  improvements: string[];
}

export interface SearchResponse {
  items: SearchResultItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SearchQueryState {
  keyword: string;
  location: string;
  tagsText: string;
  sourcesText: string;
  salaryMin: string;
  salaryMax: string;
  postedWithinDays: string;
  sortBy: string;
  maxPages: number;
  page: number;
  pageSize: number;
}

export interface SearchResultsState {
  items: SearchResultItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface StatusState {
  tone: 'muted' | 'info' | 'success' | 'danger';
  title: string;
  message: string;
}

export interface ReadinessState {
  ready: boolean | null;
  message: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
  confirmPassword: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  username: string;
  role: string;
}

export interface CurrentUser {
  username: string;
  role: string;
}

export const DEFAULT_PAGE_SIZE = 10;
export const MIN_CRAWL_PAGES = 5;
export const MAX_CRAWL_PAGES = 50;
