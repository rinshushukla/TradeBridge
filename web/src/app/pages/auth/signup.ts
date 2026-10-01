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
  email = '';
  password = '';
  error = signal('');

  submit(): void {
    if (this.password.length < 8) {
      this.error.set('Use a password with at least 8 characters.');
      return;
    }
    if (this.email.trim().toLowerCase() === this.auth.demoEmail) {
      this.error.set('That email is reserved for the demo account.');
      return;
    }
    this.auth.signup(this.name.trim(), this.email.trim().toLowerCase(), this.password);
    void this.router.navigateByUrl('/overview');
  }
}
