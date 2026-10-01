
export type ShipmentStatus =
  | 'In transit'
  | 'At customs'
  | 'Documents needed'
  | 'Delivered';

export interface Shipment {
  id: string;
  buyer: string;
  route: string;
  commodity: string;
  updated: string;
  status: ShipmentStatus;
  initials: string;
  color: string;
  progress: number;
  statusSource: string;
}

export interface ShipmentApiRecord {
  id: string;
  reference: string;
  buyer: string;
  origin: string;
  destination: string;
  commodity: string;
  status: string;
  updatedAtUtc: string;
  statusSource: string;
}

