import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'tb-login', standalone: true, imports: [FormsModule, RouterLink],
  templateUrl: './login.html', styleUrl: './auth.css'
})
export class LoginComponent {
  readonly auth = inject(AuthService);
  private router = inject(Router);
  identifier = '';
  password = '';
  error = signal('');

  submit(): void {
    this.error.set('');
    this.auth.login(this.identifier.trim().toLowerCase(), this.password).subscribe({
      next: () => void this.router.navigateByUrl('/overview'),
      error: () => this.error.set('Email or password is incorrect, or the API is unavailable.')
    });
  }
}
