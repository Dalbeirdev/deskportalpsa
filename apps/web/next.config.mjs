/** @type {import('next').NextConfig} */
const nextConfig = {
  // Self-contained server bundle for the Docker image: node_modules are traced in, so the runtime
  // stage copies one folder instead of reinstalling dependencies.
  output: 'standalone',
  reactStrictMode: true,
  poweredByHeader: false,
  async headers() {
    return [{
      source: '/:path*',
      headers: [
        { key: 'X-Content-Type-Options', value: 'nosniff' },
        { key: 'X-Frame-Options', value: 'DENY' },
        { key: 'Referrer-Policy', value: 'no-referrer' },
      ],
    }];
  },
};
export default nextConfig;
