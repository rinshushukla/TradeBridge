import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { ApiUrlService } from '../../core/services/api-url.service';

@Component({
  selector: 'tb-documents',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './documents.html',
  styleUrl: './documents.css'
})
export class DocumentsComponent {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly api = inject(ApiUrlService);
  readonly role = this.auth.currentRole();
  readonly status = signal('');
  readonly error = signal('');
  documentType = 'Business registration';
  private selectedFile: File | null = null;

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    this.status.set('');
    this.error.set('');
  }

  uploadKyc(): void {
    const file = this.selectedFile;
    if (!file) { this.error.set('Choose a document to upload.'); return; }
    if (file.size > 5_000_000) { this.error.set('Choose a file smaller than 5 MB.'); return; }
    const reader = new FileReader();
    reader.onload = () => {
      const data = String(reader.result ?? '');
      const contentBase64 = data.slice(data.indexOf(',') + 1);
      this.http.post<{ documentType: string; status: string }>(this.api.url('/api/kyc'), {
        documentType: this.documentType,
        contentBase64
      }).subscribe({
        next: result => { this.status.set(`${result.documentType} uploaded for review.`); this.error.set(''); this.selectedFile = null; },
        error: response => this.error.set(response.status === 401 || response.status === 403
          ? 'Your account is not authorized to submit KYC documents.'
          : 'Upload failed. Check that the API is available and try again.')
      });
    };
    reader.onerror = () => this.error.set('This file could not be read.');
    reader.readAsDataURL(file);
  }
}
