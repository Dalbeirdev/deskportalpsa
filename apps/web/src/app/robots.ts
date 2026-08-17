import type { MetadataRoute } from 'next';

const BASE = (process.env.NEXT_PUBLIC_SITE_URL ?? 'https://piomanage.com').replace(/\/$/, '');

/**
 * The dashboard, the control panel and the BFF are all behind authentication, so a crawler can
 * only ever collect redirects there. Saying so keeps them out of the index and out of the logs.
 */
export default function robots(): MetadataRoute.Robots {
  return {
    rules: [{ userAgent: '*', allow: '/', disallow: ['/api/', '/dashboard/', '/control-panel/'] }],
    sitemap: `${BASE}/sitemap.xml`,
  };
}
