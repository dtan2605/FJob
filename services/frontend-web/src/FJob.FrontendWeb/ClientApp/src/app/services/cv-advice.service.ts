import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AppConfigService } from './app-config.service';
import { CvAdviceRequest, CvAdviceResponse } from '../models';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class CvAdviceService {
  private readonly httpClient = inject(HttpClient);
  private readonly configService = inject(AppConfigService);
  private readonly authService = inject(AuthService);

  private buildAuthHeaders(): HttpHeaders {
    const token = this.authService.getAccessToken();
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : new HttpHeaders();
  }

  async getAdvice(request: CvAdviceRequest): Promise<CvAdviceResponse> {
    return await firstValueFrom(this.httpClient.post<CvAdviceResponse>(
      `${this.configService.snapshot.apiGatewayBaseUrl}/api/jobs/cv-advice`,
      request,
      { headers: this.buildAuthHeaders() }
    ));
  }
}
