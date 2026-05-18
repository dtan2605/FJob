import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AppConfigService } from './app-config.service';
import { CvAdviceRequest, CvAdviceResponse } from '../models';

@Injectable({ providedIn: 'root' })
export class CvAdviceService {
  private readonly httpClient = inject(HttpClient);
  private readonly configService = inject(AppConfigService);

  async getAdvice(request: CvAdviceRequest): Promise<CvAdviceResponse> {
    return await firstValueFrom(this.httpClient.post<CvAdviceResponse>(
      `${this.configService.snapshot.apiGatewayBaseUrl}/api/jobs/cv-advice`,
      request
    ));
  }
}
