import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrlService } from './api-url.service';

export interface Rfq {
  id: string;
  reference: string;
  product: string;
  quantity: string;
  incoterms: string;
  targetShipmentDate: string;
  originPort: string;
  destinationPort: string;
  requiredCertifications: string;
  status: string;
  hasSubmittedBid: boolean;
  isSelectedExporter: boolean;
  createdAtUtc: string;
}

export interface RfqCreate {
  product: string;
  quantity: string;
  incoterms: string;
  targetShipmentDate: string;
  originPort: string;
  destinationPort: string;
  requiredCertifications: string;
  privateDetails: { contactDetails: string; targetPrice: number | null; internalNotes: string };
}

export interface ShippingDocumentRecord {
  id: string;
  documentType: string;
  contentType: string;
  status: string;
  createdAtUtc: string;
  uploadedByMe: boolean;
}

@Injectable({ providedIn: 'root' })
export class RfqService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiUrlService);

  list(): Observable<Rfq[]> {
    return this.http.get<Rfq[]>(this.api.url('/api/rfqs'));
  }

  create(request: RfqCreate): Observable<Rfq> {
    return this.http.post<Rfq>(this.api.url('/api/rfqs'), request);
  }

  submitBid(id: string, bid: { price: number; currency: string; leadTimeDays: number; shippingTerms: string; sampleCoaObjectKey?: string }): Observable<unknown> {
    return this.http.post(this.api.url(`/api/rfqs/${id}/bids`), bid);
  }

  listBids(id: string): Observable<{ id: string; status: string; submittedAtUtc: string }[]> {
    return this.http.get<{ id: string; status: string; submittedAtUtc: string }[]>(this.api.url(`/api/rfqs/${id}/bids`));
  }

  unsealBids(id: string): Observable<void> {
    return this.http.post<void>(this.api.url(`/api/rfqs/${id}/unseal-bids`), {});
  }

  bidDetails(id: string, bidId: string): Observable<{ price: number; currency: string; leadTimeDays: number; shippingTerms: string; sampleCoaObjectKey?: string }> {
    return this.http.get<{ price: number; currency: string; leadTimeDays: number; shippingTerms: string; sampleCoaObjectKey?: string }>(this.api.url(`/api/rfqs/${id}/bids/${bidId}`));
  }

  selectBid(id: string, bidId: string, terms: { incoterms: string; deliveryTerms: string; penalties: string; currency: string }): Observable<unknown> {
    return this.http.post(this.api.url(`/api/rfqs/${id}/select-bid/${bidId}`), terms);
  }

  messages(id: string): Observable<{ id: string; senderUserId: string; status: string; createdAtUtc: string; body: string }[]> {
    return this.http.get<{ id: string; senderUserId: string; status: string; createdAtUtc: string; body: string }[]>(this.api.url(`/api/rfqs/${id}/messages`));
  }

  sendMessage(id: string, body: string): Observable<unknown> {
    return this.http.post(this.api.url(`/api/rfqs/${id}/messages`), { body });
  }

  releaseMessage(id: string, messageId: string): Observable<void> {
    return this.http.post<void>(this.api.url(`/api/rfqs/${id}/messages/${messageId}/release`), {});
  }

  documents(id: string): Observable<ShippingDocumentRecord[]> {
    return this.http.get<ShippingDocumentRecord[]>(this.api.url(`/api/rfqs/${id}/documents`));
  }

  uploadDocument(id: string, documentType: string, contentType: string, contentBase64: string): Observable<unknown> {
    return this.http.post(this.api.url(`/api/rfqs/${id}/documents`), { documentType, contentType, contentBase64 });
  }

  reviewDocument(id: string, documentId: string, decision: 'Verified' | 'Rejected'): Observable<unknown> {
    return this.http.post(this.api.url(`/api/rfqs/${id}/documents/${documentId}/review`), { decision });
  }

  documentContent(id: string, documentId: string): Observable<{ documentType: string; contentType: string; contentBase64: string }> {
    return this.http.get<{ documentType: string; contentType: string; contentBase64: string }>(this.api.url(`/api/rfqs/${id}/documents/${documentId}/content`));
  }
}
