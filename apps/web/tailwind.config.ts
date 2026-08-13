import type { Config } from 'tailwindcss';
export default {
  darkMode: 'class',
  content: ['./src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Forest green from the Pio mark. Blue is Autotask and ConnectWise territory;
        // matching it made the portal read as their add-on rather than a product.
        brand: { DEFAULT: '#14532D', fg: '#FDF6E3', soft: '#86EFAC', accent: '#EA580C' },
      },
    },
  },
  plugins: [],
} satisfies Config;
