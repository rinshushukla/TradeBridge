import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'tb-signup', standalone: true, imports: [FormsModule, RouterLink],
  templateUrl: './signup.html', styleUrl: './auth.css'
})
export class SignupComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  name = '';
  organization = '';
  email = '';
  phoneNumber = '';
  password = '';
  role: 'Importer' | 'Exporter' | 'CHA' = 'Importer';
  error = signal('');

  submit(): void {
    if (this.password.length < 12) {
      this.error.set('Use a password with at least 12 characters.');
      return;
    }
    if (!this.email.trim() && !this.phoneNumber.trim()) {
      this.error.set('Enter an email address or phone number.');
      return;
    }
    this.error.set('');
    this.auth.signup({
      name: this.name.trim(),
      organization: this.organization.trim(),
      email: this.email.trim().toLowerCase(),
      phoneNumber: this.phoneNumber.trim(),
      password: this.password,
      role: this.role
    }).subscribe({
      next: () => void this.router.navigateByUrl('/overview'),
      error: (error: { error?: { detail?: string; title?: string } }) =>
        this.error.set(error.error?.detail ?? error.error?.title ?? 'Could not create your account. Check the API and try again.')
    });
  }
}
