import {
  Component,
  computed,
  inject,
  signal
} from '@angular/core';

import { FormsModule } from '@angular/forms';

import {
  Shipment,
  ShipmentStatus
} from '../../models/shipment.model';

import { ShipmentService } from '../../core/services/shipment.service';

@Component({
  selector: 'tb-overview',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './overview.html',
  styleUrl: './overview.css'
})
export class OverviewComponent {
  private readonly shipmentService = inject(ShipmentService);

  query = '';

  statusFilter = signal<string>('All');

  toast = signal('');

  selectedShipment = signal<Shipment | null>(null);

  showNewShipment = signal(false);

  shipments = signal<Shipment[]>(
    this.shipmentService.getSampleShipments()
  );

  filteredShipments = computed(() => {
    const q = this.query.trim().toLowerCase();

    return this.shipments().filter(shipment => {
      const matchesStatus =
        this.statusFilter() === 'All' ||
        shipment.status === this.statusFilter();

      const searchableText = [
        shipment.id,
        shipment.buyer,
        shipment.route,
        shipment.commodity
      ]
        .join(' ')
        .toLowerCase();

      return matchesStatus &&
        (!q || searchableText.includes(q));
    });
  });

  constructor() {
    this.shipmentService.getShipments().subscribe({
      next: rows => {
        if (!rows.length) {
          return;
        }

        this.shipments.set(
          rows.map((row, index) =>
            this.shipmentService.mapApiShipment(row, index)
          )
        );
      },

      error: () => {
        this.announce(
          'API unavailable — showing sample shipments'
        );
      }
    });
  }

  statusClass(status: ShipmentStatus): string {
    return status
      .toLowerCase()
      .replaceAll(' ', '-');
  }

  selectShipment(id: string): void {
    const shipment = this.shipments()
      .find(item => item.id === id);

    if (shipment) {
      this.selectedShipment.set(shipment);
    } else {
      this.announce(
        'Open the shipment from the list to view live details'
      );
    }
  }

  announce(message: string): void {
    this.toast.set(message);

    window.setTimeout(() => {
      this.toast.set('');
    }, 2800);
  }
}
