import type { Config } from 'tailwindcss';
export default {
  darkMode: 'class',
  content: ['./src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      fontFamily: { sans: ['var(--font-sans-ui)', 'system-ui', 'sans-serif'] },
      maxWidth: { shell: '1240px' },
      colors: {
        // Forest green from the Pio mark. Blue is Autotask and ConnectWise territory;
        // matching it made the portal read as their add-on rather than a product.
        brand: {
          DEFAULT: '#14532D', fg: '#FDF6E3', soft: '#86EFAC', accent: '#EA580C',
          deep: '#0B3B21', mid: '#1E7A45', tint: '#ECFDF3',
        },
        // Deep navy carries the product surfaces: green everywhere reads as a brochure,
        // green against navy reads as software.
        ink: { DEFAULT: '#0B1220', soft: '#16213A', line: '#22304C' },
      },
    },
  },
  plugins: [],
} satisfies Config;
