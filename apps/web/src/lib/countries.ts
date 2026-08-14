/**
 * Countries offered on the booking form, each with its dialling code and primary IANA time zone.
 *
 * The time zone is a sensible default, not a claim: several of these countries span more than one,
 * so the form always shows the zone it is going to send and lets the visitor change it. Guessing
 * silently is how a meeting gets booked at the wrong hour.
 */
export type Country = {
  code: string;
  name: string;
  dial: string;
  tz: string;
};

export const COUNTRIES: Country[] = [
  { code: 'AE', name: 'United Arab Emirates', dial: '+971', tz: 'Asia/Dubai' },
  { code: 'AR', name: 'Argentina', dial: '+54', tz: 'America/Argentina/Buenos_Aires' },
  { code: 'AT', name: 'Austria', dial: '+43', tz: 'Europe/Vienna' },
  { code: 'AU', name: 'Australia', dial: '+61', tz: 'Australia/Sydney' },
  { code: 'BE', name: 'Belgium', dial: '+32', tz: 'Europe/Brussels' },
  { code: 'BD', name: 'Bangladesh', dial: '+880', tz: 'Asia/Dhaka' },
  { code: 'BR', name: 'Brazil', dial: '+55', tz: 'America/Sao_Paulo' },
  { code: 'CA', name: 'Canada', dial: '+1', tz: 'America/Toronto' },
  { code: 'CH', name: 'Switzerland', dial: '+41', tz: 'Europe/Zurich' },
  { code: 'CL', name: 'Chile', dial: '+56', tz: 'America/Santiago' },
  { code: 'CN', name: 'China', dial: '+86', tz: 'Asia/Shanghai' },
  { code: 'CO', name: 'Colombia', dial: '+57', tz: 'America/Bogota' },
  { code: 'CZ', name: 'Czechia', dial: '+420', tz: 'Europe/Prague' },
  { code: 'DE', name: 'Germany', dial: '+49', tz: 'Europe/Berlin' },
  { code: 'DK', name: 'Denmark', dial: '+45', tz: 'Europe/Copenhagen' },
  { code: 'EG', name: 'Egypt', dial: '+20', tz: 'Africa/Cairo' },
  { code: 'ES', name: 'Spain', dial: '+34', tz: 'Europe/Madrid' },
  { code: 'FI', name: 'Finland', dial: '+358', tz: 'Europe/Helsinki' },
  { code: 'FR', name: 'France', dial: '+33', tz: 'Europe/Paris' },
  { code: 'GB', name: 'United Kingdom', dial: '+44', tz: 'Europe/London' },
  { code: 'GR', name: 'Greece', dial: '+30', tz: 'Europe/Athens' },
  { code: 'HK', name: 'Hong Kong', dial: '+852', tz: 'Asia/Hong_Kong' },
  { code: 'HU', name: 'Hungary', dial: '+36', tz: 'Europe/Budapest' },
  { code: 'ID', name: 'Indonesia', dial: '+62', tz: 'Asia/Jakarta' },
  { code: 'IE', name: 'Ireland', dial: '+353', tz: 'Europe/Dublin' },
  { code: 'IL', name: 'Israel', dial: '+972', tz: 'Asia/Jerusalem' },
  { code: 'IN', name: 'India', dial: '+91', tz: 'Asia/Kolkata' },
  { code: 'IT', name: 'Italy', dial: '+39', tz: 'Europe/Rome' },
  { code: 'JP', name: 'Japan', dial: '+81', tz: 'Asia/Tokyo' },
  { code: 'KE', name: 'Kenya', dial: '+254', tz: 'Africa/Nairobi' },
  { code: 'KR', name: 'South Korea', dial: '+82', tz: 'Asia/Seoul' },
  { code: 'LK', name: 'Sri Lanka', dial: '+94', tz: 'Asia/Colombo' },
  { code: 'LU', name: 'Luxembourg', dial: '+352', tz: 'Europe/Luxembourg' },
  { code: 'MA', name: 'Morocco', dial: '+212', tz: 'Africa/Casablanca' },
  { code: 'MX', name: 'Mexico', dial: '+52', tz: 'America/Mexico_City' },
  { code: 'MY', name: 'Malaysia', dial: '+60', tz: 'Asia/Kuala_Lumpur' },
  { code: 'NG', name: 'Nigeria', dial: '+234', tz: 'Africa/Lagos' },
  { code: 'NL', name: 'Netherlands', dial: '+31', tz: 'Europe/Amsterdam' },
  { code: 'NO', name: 'Norway', dial: '+47', tz: 'Europe/Oslo' },
  { code: 'NZ', name: 'New Zealand', dial: '+64', tz: 'Pacific/Auckland' },
  { code: 'PH', name: 'Philippines', dial: '+63', tz: 'Asia/Manila' },
  { code: 'PK', name: 'Pakistan', dial: '+92', tz: 'Asia/Karachi' },
  { code: 'PL', name: 'Poland', dial: '+48', tz: 'Europe/Warsaw' },
  { code: 'PT', name: 'Portugal', dial: '+351', tz: 'Europe/Lisbon' },
  { code: 'QA', name: 'Qatar', dial: '+974', tz: 'Asia/Qatar' },
  { code: 'RO', name: 'Romania', dial: '+40', tz: 'Europe/Bucharest' },
  { code: 'SA', name: 'Saudi Arabia', dial: '+966', tz: 'Asia/Riyadh' },
  { code: 'SE', name: 'Sweden', dial: '+46', tz: 'Europe/Stockholm' },
  { code: 'SG', name: 'Singapore', dial: '+65', tz: 'Asia/Singapore' },
  { code: 'TH', name: 'Thailand', dial: '+66', tz: 'Asia/Bangkok' },
  { code: 'TR', name: 'Türkiye', dial: '+90', tz: 'Europe/Istanbul' },
  { code: 'UA', name: 'Ukraine', dial: '+380', tz: 'Europe/Kyiv' },
  { code: 'US', name: 'United States', dial: '+1', tz: 'America/New_York' },
  { code: 'VN', name: 'Vietnam', dial: '+84', tz: 'Asia/Ho_Chi_Minh' },
  { code: 'ZA', name: 'South Africa', dial: '+27', tz: 'Africa/Johannesburg' },
];

/** Fallback zones when the browser cannot enumerate them. */
export const COMMON_TIMEZONES = [...new Set(COUNTRIES.map((c) => c.tz))].sort();

/** Half-hour slots across a normal working day, in the visitor's own zone. */
export const TIME_SLOTS = Array.from({ length: 21 }, (_, i) => {
  const mins = 8 * 60 + i * 30;
  return `${String(Math.floor(mins / 60)).padStart(2, '0')}:${String(mins % 60).padStart(2, '0')}`;
});

export function countryFor(code: string): Country | undefined {
  return COUNTRIES.find((c) => c.code === code);
}

/**
 * Legacy IANA names browsers still report. Without these, a visitor in India whose browser says
 * "Asia/Calcutta" matched no country and was left on the default dial code — a +44 prefix beside an
 * Indian time zone, which is precisely how a meeting gets booked at the wrong hour.
 */
const TZ_ALIASES: Record<string, string> = {
  'Asia/Calcutta': 'Asia/Kolkata',
  'Asia/Saigon': 'Asia/Ho_Chi_Minh',
  'Asia/Rangoon': 'Asia/Yangon',
  'Asia/Katmandu': 'Asia/Kathmandu',
  'Asia/Istanbul': 'Europe/Istanbul',
  'Europe/Kiev': 'Europe/Kyiv',
  'America/Buenos_Aires': 'America/Argentina/Buenos_Aires',
  'Australia/Canberra': 'Australia/Sydney',
  'Asia/Chongqing': 'Asia/Shanghai',
};

/** Canonical IANA name, preferring the browser's own resolution and falling back to the map. */
export function canonicalTimeZone(tz: string): string {
  let resolved = tz;
  try {
    resolved = Intl.DateTimeFormat('en-US', { timeZone: tz }).resolvedOptions().timeZone || tz;
  } catch {
    /* keep what we were given */
  }
  return TZ_ALIASES[resolved] ?? TZ_ALIASES[tz] ?? resolved;
}

/**
 * Best guess at the visitor's country from the zone their browser reports. Only ever a starting
 * value — the select stays editable, and the summary shows what will actually be sent.
 */
export function countryFromTimeZone(tz: string): Country | undefined {
  const canonical = canonicalTimeZone(tz);
  return COUNTRIES.find((c) => c.tz === canonical);
}
