/**
 * The PSA platforms the site talks about — one list, used by the hero rotator, the ecosystem
 * diagram, the integration grid and the footer. Adding a ninth platform is an entry here and
 * nothing else.
 *
 * `status` must track what the backend can actually do, not what we would like to say. Today
 * `packages/connectors` holds ConnectWise and Autotask, and only those two factories are
 * registered in DependencyInjection — everything else is a reserved slot in ProviderType with no
 * implementation behind it. Marking a planned platform "available" would be the single fastest way
 * to lose an MSP who books a demo expecting it.
 */
export type PsaStatus = 'available' | 'planned';

export type PsaPlatform = {
  id: string;
  name: string;
  /** Two letters for the tile mark. We do not ship third-party logos we have no licence for. */
  initials: string;
  status: PsaStatus;
  blurb: string;
};

export const PSA_PLATFORMS: PsaPlatform[] = [
  { id: 'connectwise', name: 'ConnectWise PSA', initials: 'CW', status: 'available', blurb: 'Service boards, members and time.' },
  { id: 'autotask', name: 'Autotask PSA', initials: 'AT', status: 'available', blurb: 'Queues, work types and resources.' },
  { id: 'halo', name: 'HaloPSA', initials: 'HA', status: 'planned', blurb: 'Tickets, actions and time entries.' },
  { id: 'kaseya-bms', name: 'Kaseya BMS', initials: 'KB', status: 'planned', blurb: 'Service desk and time tracking.' },
  { id: 'syncro', name: 'Syncro', initials: 'SY', status: 'planned', blurb: 'Tickets, comments and attachments.' },
  { id: 'superops', name: 'SuperOps', initials: 'SO', status: 'planned', blurb: 'Requests, conversations and status.' },
  { id: 'n-able', name: 'N-able MSP Manager', initials: 'NA', status: 'planned', blurb: 'Tickets and technician assignment.' },
  { id: 'atera', name: 'Atera', initials: 'AE', status: 'planned', blurb: 'Tickets, replies and status.' },
];

export const AVAILABLE_PLATFORMS = PSA_PLATFORMS.filter((p) => p.status === 'available');
export const PLANNED_PLATFORMS = PSA_PLATFORMS.filter((p) => p.status === 'planned');

export const STATUS_LABEL: Record<PsaStatus, string> = {
  available: 'Available now',
  planned: 'Coming soon',
};
