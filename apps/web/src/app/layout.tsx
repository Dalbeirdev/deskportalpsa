import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import './globals.css';

// Self-hosted at build time by next/font: no request to Google at runtime, so it cannot be
// blocked by a CSP and cannot flash unstyled text on a slow network.
const sans = Inter({ subsets: ['latin'], display: 'swap', variable: '--font-sans-ui' });

export const metadata: Metadata = {
  title: 'Desk Portal',
  description: 'Multi-tenant PSA ticket portal',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning className={sans.variable}>
      <body className="min-h-screen antialiased">{children}</body>
    </html>
  );
}
