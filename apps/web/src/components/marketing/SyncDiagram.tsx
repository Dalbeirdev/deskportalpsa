/**
 * The hero visual: the portal on one side, the PSA on the other, work moving continuously between
 * them. The animation carries the product's single most important claim — that sync runs in both
 * directions — so it earns its place rather than decorating the page.
 *
 * Colours come from theme variables so it inverts correctly; --brand-line is forest on light and
 * mint on dark, which keeps the flowing strokes legible either way.
 */
export function SyncDiagram() {
  const rows = [0, 1, 2, 3];
  return (
    <svg
      viewBox="0 0 560 360"
      className="h-auto w-full"
      role="img"
      aria-label="Tickets, replies and time moving continuously in both directions between the client portal and your PSA."
    >
      <title>Two-way sync between the client portal and your PSA</title>

      <g>
        <rect x="222" y="18" width="116" height="28" rx="14" fill="var(--surface)" stroke="var(--border)" />
        <text x="280" y="36" textAnchor="middle" fontSize="12" fontWeight="600" fill="var(--brand-line)">
          Two-way sync
        </text>
      </g>

      <g>
        <rect x="6" y="76" width="196" height="216" rx="16" fill="var(--surface)" stroke="var(--border)" />
        <text x="24" y="104" fontSize="13" fontWeight="600" fill="var(--fg)">Client portal</text>
        {rows.map((i) => (
          <g key={`l${i}`}>
            <rect x="24" y={122 + i * 38} width="160" height="28" rx="8" fill="var(--bg)" />
            <circle
              cx="38"
              cy={136 + i * 38}
              r="4"
              fill="var(--brand-line)"
              className={i === 0 ? 'dp-pulse' : undefined}
            />
            <rect x="50" y={131 + i * 38} width={104 - i * 16} height="5" rx="2.5" fill="var(--border)" />
          </g>
        ))}
      </g>

      <g>
        <rect x="358" y="76" width="196" height="216" rx="16" fill="var(--surface)" stroke="var(--border)" />
        <text x="376" y="104" fontSize="13" fontWeight="600" fill="var(--fg)">Autotask · ConnectWise</text>
        {rows.map((i) => (
          <g key={`r${i}`}>
            <rect x="376" y={122 + i * 38} width="160" height="28" rx="8" fill="var(--bg)" />
            <circle
              cx="390"
              cy={136 + i * 38}
              r="4"
              fill="var(--brand-line)"
              className={i === 1 ? 'dp-pulse' : undefined}
            />
            <rect x="402" y={131 + i * 38} width={96 - i * 14} height="5" rx="2.5" fill="var(--border)" />
          </g>
        ))}
      </g>

      <g>
        <text x="280" y="122" textAnchor="middle" fontSize="11" fill="var(--muted)">
          tickets · replies · files
        </text>
        <path
          d="M206 158C252 132 308 132 354 158"
          fill="none"
          stroke="var(--brand-line)"
          strokeWidth="2"
          strokeLinecap="round"
          className="dp-flow"
        />
        <path d="M348 152L356 158L348 164Z" fill="var(--brand-line)" />

        <path
          d="M354 216C308 242 252 242 206 216"
          fill="none"
          stroke="var(--brand-line)"
          strokeWidth="2"
          strokeLinecap="round"
          className="dp-flow"
          style={{ animationDirection: 'reverse', animationDuration: '3.6s' }}
        />
        <path d="M212 210L204 216L212 222Z" fill="var(--brand-line)" />
        <text x="280" y="264" textAnchor="middle" fontSize="11" fill="var(--muted)">
          notes · status · time
        </text>
      </g>

      <text x="280" y="330" textAnchor="middle" fontSize="11" fill="var(--muted)">
        Your PSA stays the system of record
      </text>
    </svg>
  );
}
