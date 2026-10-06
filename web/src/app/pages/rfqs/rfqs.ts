import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { Rfq, RfqService, ShippingDocumentRecord } from '../../core/services/rfq.service';

@Component({
  selector: 'tb-rfqs',
  standalone: true,
  imports: [FormsModule, DatePipe, DecimalPipe],
  templateUrl: './rfqs.html',
  styleUrl: './rfqs.css'
})
export class RfqsComponent implements OnInit {
  private readonly rfqService = inject(RfqService);
  private readonly auth = inject(AuthService);
  readonly role = this.auth.currentRole();
  readonly rfqs = signal<Rfq[]>([]);
  readonly error = signal('');
  readonly notice = signal('');
  readonly selectedRfq = signal<Rfq | null>(null);
  readonly showCreate = signal(false);
  readonly reviewRfq = signal<Rfq | null>(null);
  readonly bids = signal<{ id: string; status: string; submittedAtUtc: string }[]>([]);
  readonly bidDetail = signal<{ bidId: string; price: number; currency: string; leadTimeDays: number; shippingTerms: string; sampleCoaObjectKey?: string } | null>(null);
  readonly messageRfq = signal<Rfq | null>(null);
  readonly messages = signal<{ id: string; senderUserId: string; status: string; createdAtUtc: string; body: string }[]>([]);
  readonly documentRfq = signal<Rfq | null>(null);
  readonly documents = signal<ShippingDocumentRecord[]>([]);
  messageBody = '';
  documentType = 'Bill of lading';
  private selectedDocumentFile: File | null = null;

  rfqForm = {
    product: '', quantity: '', incoterms: 'FOB', targetShipmentDate: new Date().toISOString().slice(0, 10),
    originPort: '', destinationPort: '', requiredCertifications: '', contactDetails: '',
    targetPrice: null as number | null, internalNotes: ''
  };
  bidForm = { price: null as number | null, currency: 'USD', leadTimeDays: 30, shippingTerms: '', sampleCoaObjectKey: '' };
  contractForm = { incoterms: 'FOB', deliveryTerms: '', penalties: '', currency: 'USD' };

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.rfqService.list().subscribe({
      next: rows => { this.rfqs.set(rows); this.error.set(''); },
      error: () => this.error.set('Could not load RFQs. Check that the API and database are running.')
    });
  }

  createRfq(): void {
    const { contactDetails, targetPrice, internalNotes, ...publicFields } = this.rfqForm;
    this.rfqService.create({ ...publicFields, privateDetails: { contactDetails, targetPrice, internalNotes } }).subscribe({
      next: rfq => {
        this.rfqs.update(rows => [rfq, ...rows]);
        this.showCreate.set(false);
        this.notice.set(`RFQ ${rfq.reference} created.`);
        this.rfqForm = { product: '', quantity: '', incoterms: 'FOB', targetShipmentDate: new Date().toISOString().slice(0, 10), originPort: '', destinationPort: '', requiredCertifications: '', contactDetails: '', targetPrice: null, internalNotes: '' };
      },
      error: response => this.error.set(response.status === 401 || response.status === 403
        ? 'Your account does not have importer access. CHA access must be approved.'
        : 'RFQ could not be saved. Please check the details and try again.')
    });
  }

  submitBid(): void {
    const rfq = this.selectedRfq();
    if (!rfq || this.bidForm.price === null) return;
    this.rfqService.submitBid(rfq.id, { ...this.bidForm, price: this.bidForm.price }).subscribe({
      next: () => {
        this.notice.set(`Your sealed bid was submitted for ${rfq.reference}.`);
        this.rfqs.update(rows => rows.map(row => row.id === rfq.id ? { ...row, hasSubmittedBid: true } : row));
        this.selectedRfq.set(null);
      },
      error: response => this.error.set(response.status === 401 || response.status === 403
        ? 'Your account does not have exporter access.'
        : 'Bid could not be submitted. Confirm this RFQ is still open.')
    });
  }

  openBidReview(rfq: Rfq): void {
    this.rfqService.listBids(rfq.id).subscribe({
      next: rows => { this.reviewRfq.set(rfq); this.bids.set(rows); this.error.set(''); },
      error: () => this.error.set('Bids are not available yet. CHA must unseal them after review.')
    });
  }

  unsealBids(rfq: Rfq): void {
    this.rfqService.unsealBids(rfq.id).subscribe({
      next: () => { this.notice.set(`Bids unsealed for ${rfq.reference}.`); this.load(); },
      error: () => this.error.set('Could not unseal bids. The RFQ may no longer be open.')
    });
  }

  showBid(rfq: Rfq, bidId: string): void {
    this.rfqService.bidDetails(rfq.id, bidId).subscribe({
      next: detail => { this.bidDetail.set({ ...detail, bidId }); this.error.set(''); },
      error: () => this.error.set('Bid details are not available until the CHA unseals bids.')
    });
  }

  selectBid(rfq: Rfq, bidId: string): void {
    if (!this.contractForm.deliveryTerms.trim() || !this.contractForm.penalties.trim()) {
      this.error.set('Add delivery terms and penalties before selecting a bid.');
      return;
    }
    this.rfqService.selectBid(rfq.id, bidId, this.contractForm).subscribe({
      next: () => { this.notice.set(`Bid selected for ${rfq.reference}; contract terms are saved encrypted.`); this.reviewRfq.set(null); this.load(); },
      error: () => this.error.set('Bid selection failed. Confirm the RFQ was unsealed and the bid belongs to it.')
    });
  }

  openMessages(rfq: Rfq): void {
    this.messageRfq.set(rfq);
    this.loadMessages(rfq);
  }

  loadMessages(rfq: Rfq): void {
    this.rfqService.messages(rfq.id).subscribe({
      next: rows => this.messages.set(rows),
      error: () => this.error.set('Messages could not be loaded for this RFQ.')
    });
  }

  sendMessage(): void {
    const rfq = this.messageRfq();
    if (!rfq || !this.messageBody.trim()) return;
    this.rfqService.sendMessage(rfq.id, this.messageBody).subscribe({
      next: () => { this.messageBody = ''; this.notice.set('Message sent to the CHA mediation queue.'); this.loadMessages(rfq); },
      error: () => this.error.set('Message could not be sent. Check that you are part of this RFQ.')
    });
  }

  releaseMessage(rfq: Rfq, messageId: string): void {
    this.rfqService.releaseMessage(rfq.id, messageId).subscribe({
      next: () => this.loadMessages(rfq),
      error: () => this.error.set('Only an active CHA can release messages.')
    });
  }

  openDocuments(rfq: Rfq): void {
    this.documentRfq.set(rfq);
    this.loadDocuments(rfq);
  }

  loadDocuments(rfq: Rfq): void {
    this.rfqService.documents(rfq.id).subscribe({
      next: rows => this.documents.set(rows),
      error: () => this.error.set('Trade documents could not be loaded.')
    });
  }

  onDocumentSelected(event: Event): void {
    this.selectedDocumentFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  uploadDocument(): void {
    const rfq = this.documentRfq();
    const file = this.selectedDocumentFile;
    if (!rfq || !file) { this.error.set('Choose a document to upload.'); return; }
    if (file.size > 10_000_000) { this.error.set('Choose a file smaller than 10 MB.'); return; }
    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result ?? '');
      this.rfqService.uploadDocument(rfq.id, this.documentType, file.type, result.slice(result.indexOf(',') + 1)).subscribe({
        next: () => { this.notice.set('Document encrypted and sent to CHA review.'); this.selectedDocumentFile = null; this.loadDocuments(rfq); },
        error: () => this.error.set('Document upload failed. Confirm this account is authorized for the awarded RFQ.')
      });
    };
    reader.onerror = () => this.error.set('This file could not be read.');
    reader.readAsDataURL(file);
  }

  reviewDocument(rfq: Rfq, item: ShippingDocumentRecord, decision: 'Verified' | 'Rejected'): void {
    this.rfqService.reviewDocument(rfq.id, item.id, decision).subscribe({
      next: () => { this.notice.set(`Document ${decision.toLowerCase()}.`); this.loadDocuments(rfq); },
      error: () => this.error.set('Only an active CHA can review trade documents.')
    });
  }

  downloadDocument(rfq: Rfq, item: ShippingDocumentRecord): void {
    this.rfqService.documentContent(rfq.id, item.id).subscribe({
      next: file => {
        const bytes = Uint8Array.from(atob(file.contentBase64), char => char.charCodeAt(0));
        const url = URL.createObjectURL(new Blob([bytes], { type: file.contentType }));
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `${item.documentType.toLowerCase().replaceAll(' ', '-')}-${item.id.slice(0, 8)}`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.error.set('You are not authorized to read this document yet.')
    });
  }
}
