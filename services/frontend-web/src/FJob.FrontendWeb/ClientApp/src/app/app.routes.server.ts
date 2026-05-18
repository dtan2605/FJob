import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  { path: '', renderMode: RenderMode.Server },
  { path: 'viec-lam', renderMode: RenderMode.Server },
  { path: 'dang-nhap', renderMode: RenderMode.Server },
  { path: 'dang-ky', renderMode: RenderMode.Server },
  { path: '**', renderMode: RenderMode.Server }
];
