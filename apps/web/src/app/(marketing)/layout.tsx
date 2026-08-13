import { MarketingHeader } from '@/components/marketing/MarketingHeader';
import { MarketingFooter } from '@/components/marketing/MarketingFooter';
import { ThemeToggle } from '@/components/ThemeToggle';
// The enquiry forms submit through React Query, which needs a client provider on this branch of
// the tree too — the dashboard's does not reach the public pages.
import { QueryProvider } from '@/components/QueryProvider';

/**
 * Chrome for every public page. Sign-in lives inside this group deliberately: it is the last page
 * a prospect sees before deciding, and stranding it without a way back to the site was the gap.
 */
export default function MarketingLayout({ children }: { children: React.ReactNode }) {
  return (
    <QueryProvider>
      <div className="flex min-h-screen flex-col">
        <MarketingHeader />
        <div className="fixed bottom-5 right-5 z-30">
          <ThemeToggle />
        </div>
        <main className="flex-1">{children}</main>
        <MarketingFooter />
      </div>
    </QueryProvider>
  );
}
