import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'Desk Portal',
  description: 'Multi-tenant PSA ticket portal',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className="min-h-screen antialiased">{children}</body>
    </html>
  );
}
