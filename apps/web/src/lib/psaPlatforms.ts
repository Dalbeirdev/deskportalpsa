/**
 * The PSA platforms shown across the public site — one list, used by the hero rotator, the
 * ecosystem diagram, the integration grid and the footer. Adding a platform is an entry here and
 * nothing else.
 *
 * PRESENTATION ONLY. This list says nothing about what the backend can do, and deliberately
 * carries no status field: connector capability lives in `ProviderType` and `packages/connectors`,
 * and the marketing site does not surface it. Do not add a status flag here — a field that exists
 * only to be hidden is how implementation detail leaks back into a public page.
 */
export type PsaPlatform = {
  id: string;
  name: string;
  /** Two letters for the tile mark. We ship no third-party logos we have no licence to use. */
  initials: string;
};

export const PSA_PLATFORMS: PsaPlatform[] = [
  { id: 'connectwise', name: 'ConnectWise PSA', initials: 'CW' },
  { id: 'autotask', name: 'Autotask PSA', initials: 'AT' },
  { id: 'halo', name: 'HaloPSA', initials: 'HA' },
  { id: 'kaseya-bms', name: 'Kaseya BMS', initials: 'KB' },
  { id: 'syncro', name: 'Syncro', initials: 'SY' },
  { id: 'superops', name: 'SuperOps', initials: 'SO' },
  { id: 'n-able', name: 'N-able MSP Manager', initials: 'NA' },
  { id: 'atera', name: 'Atera', initials: 'AE' },
];

/** Shown on every card, so no platform reads as more or less established than another. */
export const PLATFORM_DESCRIPTOR = 'Service management integration';
