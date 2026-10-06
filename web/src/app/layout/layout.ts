import { Component, inject, output, OnInit, OnDestroy, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet, Router } from '@angular/router';
import { AuthService } from '../core/services/auth.service';

@Component({
  selector: 'tb-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './layout.html',
  styleUrl: './layout.css'
})
export class LayoutComponent implements OnInit, OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  toast = output<string>();
  toastMessage = signal('');
  timerId: ReturnType<typeof setInterval> | undefined;
  toastTimerId: ReturnType<typeof setTimeout> | undefined;
  currentDateTime = '';

  get currentPageTitle(): string {
    const segment = this.router.url.split('?')[0].split('/')[1] ?? 'overview';
    const titles: Record<string, string> = {
      overview: 'Overview', rfqs: 'RFQs & bids', shipments: 'Shipments',
      documents: 'Documents', payments: 'Payments', team: 'Team & access', settings: 'Settings'
    };
    return titles[segment] ?? 'Overview';
  }

  announce(message: string): void {
    this.toast.emit(message);
    this.toastMessage.set(message);

    if (this.toastTimerId) {
      clearTimeout(this.toastTimerId);
    }

    this.toastTimerId = setTimeout(() => this.toastMessage.set(''), 2800);
  }

  ngOnInit(): void {
    // Run immediately on component init
    this.updateSystemDateTime();

    // Update every second
    this.timerId = setInterval(() => {
      this.updateSystemDateTime();
    }, 1000);
  }

  ngOnDestroy(): void {
    if (this.timerId) {
      clearInterval(this.timerId);
    }
    if (this.toastTimerId) {
      clearTimeout(this.toastTimerId);
    }
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }

  updateSystemDateTime(): void {
    const options: Intl.DateTimeFormatOptions = {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: true
    };

    this.currentDateTime = new Intl.DateTimeFormat('en-GB', options).format(new Date());
  }
}
