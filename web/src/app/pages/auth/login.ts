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
  email = this.auth.demoEmail;
  password = this.auth.demoPassword;
  error = signal('');

  submit(): void {
    if (!this.auth.login(this.email.trim().toLowerCase(), this.password)) {
      this.error.set('Email or password is incorrect. Check the demo credentials below or sign up.');
      return;
    }
    void this.router.navigateByUrl('/overview');
  }
}
