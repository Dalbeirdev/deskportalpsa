import type { MetadataRoute } from 'next';
import { PSA_PLATFORMS, platformHref } from '@/lib/psaPlatforms';

/**
 * Where the site lives. Configurable because the same build runs on a staging host, and a sitemap
 * that advertises production URLs from staging is worse than no sitemap at all.
 */
const BASE = (process.env.NEXT_PUBLIC_SITE_URL ?? 'https://piomanage.com').replace(/\/$/, '');

/** Marketing routes only. Nothing behind sign-in belongs in a public sitemap. */
const PAGES: { path: string; priority: number }[] = [
  { path: '/', priority: 1 },
  { path: '/platform', priority: 0.9 },
  { path: '/integrations', priority: 0.9 },
  { path: '/security', priority: 0.8 },
  { path: '/about', priority: 0.6 },
  { path: '/faq', priority: 0.6 },
  { path: '/contact', priority: 0.6 },
  { path: '/book', priority: 0.7 },
  { path: '/privacy', priority: 0.3 },
  { path: '/terms', priority: 0.3 },
];

export default function sitemap(): MetadataRoute.Sitemap {
  const lastModified = new Date();
  return [
    ...PAGES.map((p) => ({ url: `${BASE}${p.path}`, lastModified, priority: p.priority })),
    ...PSA_PLATFORMS.map((p) => ({
      url: `${BASE}${platformHref(p)}`,
      lastModified,
      priority: 0.7,
    })),
  ];
}
