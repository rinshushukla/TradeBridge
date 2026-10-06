import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiUrlService } from './api-url.service';

import {
  Shipment,
  ShipmentApiRecord,
  ShipmentStatus
} from '../../models/shipment.model';

@Injectable({
  providedIn: 'root'
})
export class ShipmentService {
  private readonly http = inject(HttpClient);
  private readonly api = inject(ApiUrlService);

  private readonly sampleShipments: Shipment[] = [
    {
      id: 'TB-2408',
      buyer: 'Greenfield Imports',
      route: 'Chennai, IN → Hamburg, DE',
      commodity: 'Organic turmeric · 2,400 kg',
      updated: '12 min ago',
      status: 'Documents needed',
      initials: 'GI',
      color: '#e5eee8',
      progress: 62,
      statusSource: 'partner'
    },
    {
      id: 'TB-2406',
      buyer: 'Maison du Marché',
      route: 'Mumbai, IN → Marseille, FR',
      commodity: 'Cotton textiles · 860 pcs',
      updated: '1 hr ago',
      status: 'At customs',
      initials: 'MM',
      color: '#f1e9df',
      progress: 74,
      statusSource: 'partner'
    },
    {
      id: 'TB-2402',
      buyer: 'Nairobi Fresh Co.',
      route: 'Kochi, IN → Mombasa, KE',
      commodity: 'Dried ginger · 1,200 kg',
      updated: '3 hrs ago',
      status: 'In transit',
      initials: 'NF',
      color: '#e8e8f4',
      progress: 48,
      statusSource: 'partner'
    },
    {
      id: 'TB-2398',
      buyer: 'Pacific Pantry Ltd.',
      route: 'Nhava Sheva, IN → Singapore, SG',
      commodity: 'Basmati rice · 18 MT',
      updated: 'Yesterday',
      status: 'Delivered',
      initials: 'PP',
      color: '#e5ebed',
      progress: 100,
      statusSource: 'partner'
    },
    {
      id: 'TB-2391',
      buyer: 'Cedar & Coast Trading',
      route: 'Chennai, IN → Dubai, AE',
      commodity: 'Handwoven rugs · 120 pcs',
      updated: 'Yesterday',
      status: 'In transit',
      initials: 'CC',
      color: '#f0e7e6',
      progress: 58,
      statusSource: 'partner'
    }
  ];

  getShipments(): Observable<ShipmentApiRecord[]> {
    return this.http.get<ShipmentApiRecord[]>(this.api.url('/api/shipments'));
  }

  getSampleShipments(): Shipment[] {
    return this.sampleShipments;
  }

  mapApiShipment(row: ShipmentApiRecord, index: number): Shipment {
    return {
      id: row.reference,
      buyer: row.buyer,
      route: `${row.origin} → ${row.destination}`,
      commodity: row.commodity,
      updated: new Date(row.updatedAtUtc).toLocaleDateString(),
      status: this.apiStatus(row.status),
      initials: row.buyer
        .split(/\s+/)
        .map(part => part[0] ?? '')
        .join('')
        .slice(0, 2)
        .toUpperCase(),
      color: [
        '#e5eee8',
        '#f1e9df',
        '#e8e8f4',
        '#e5ebed',
        '#f0e7e6'
      ][index % 5],
      progress:
        row.status.toLowerCase() === 'delivered'
          ? 100
          : 60,
      statusSource: row.statusSource
    };
  }

  private apiStatus(status: string): ShipmentStatus {
    const value = status.toLowerCase();

    if (value.includes('custom')) {
      return 'At customs';
    }

    if (value.includes('document')) {
      return 'Documents needed';
    }

    if (value === 'delivered') {
      return 'Delivered';
    }

    return 'In transit';
  }
}
