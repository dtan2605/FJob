import { Routes } from '@angular/router';
import { HomePageComponent } from './pages/home-page.component';
import { JobsPageComponent } from './pages/jobs-page.component';
import { LoginPageComponent } from './pages/login-page.component';
import { RegisterPageComponent } from './pages/register-page.component';
import { CvPageComponent } from './pages/cv-page.component';

export const appRoutes: Routes = [
  { path: '', component: HomePageComponent, title: 'FJob | Trang chủ' },
  { path: 'viec-lam', component: JobsPageComponent, title: 'FJob | Tìm việc làm' },
  { path: 'cv', component: CvPageComponent, title: 'FJob | CV của tôi' },
  { path: 'dang-nhap', component: LoginPageComponent, title: 'FJob | Đăng nhập' },
  { path: 'dang-ky', component: RegisterPageComponent, title: 'FJob | Đăng ký' },
  { path: '**', redirectTo: '' }
];
