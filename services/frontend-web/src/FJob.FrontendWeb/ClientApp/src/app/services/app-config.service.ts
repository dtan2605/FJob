import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AppConfig, ReadinessState } from '../models';

@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly httpClient = inject(HttpClient);

  private config: AppConfig = {
    apiGatewayBaseUrl: 'http://localhost:5100',
    appName: 'FJob Search',
    renderingMode: 'angular-standalone-spa'
  };

  async loadConfig(): Promise<AppConfig> {
    try {
      this.config = await firstValueFrom(this.httpClient.get<AppConfig>('/api/config'));
    } catch {
      this.config = {
        apiGatewayBaseUrl: 'http://localhost:5100',
        appName: 'FJob Search',
        renderingMode: 'angular-standalone-spa'
      };
    }

    return this.config;
  }

  get snapshot(): AppConfig {
    return this.config;
  }

  async loadReadiness(): Promise<ReadinessState> {
    try {
      const payload = await firstValueFrom(
        this.httpClient.get<{ ready?: boolean; error?: string }>('/ready')
      );

      return {
        ready: payload?.ready !== false,
        message: payload?.ready === false
          ? payload.error ?? 'Kiểm tra Gateway thất bại.'
          : 'Kết nối hệ thống đang sẵn sàng.'
      };
    } catch {
      return {
        ready: false,
        message: 'Không thể xác nhận trạng thái sẵn sàng của hệ thống.'
      };
    }
  }
}
