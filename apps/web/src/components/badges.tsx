import clsx from 'clsx';

const statusStyles: Record<string, string> = {
  NEW: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
  IN_PROGRESS: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
  WAITING_CUSTOMER: 'bg-purple-100 text-purple-700 dark:bg-purple-950 dark:text-purple-300',
  RESOLVED: 'bg-green-100 text-green-700 dark:bg-green-950 dark:text-green-300',
  CLOSED: 'bg-slate-200 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
};

const priorityStyles: Record<string, string> = {
  LOW: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
  NORMAL: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300',
  HIGH: 'bg-orange-100 text-orange-700 dark:bg-orange-950 dark:text-orange-300',
  CRITICAL: 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300',
};

function Pill({ label, className }: { label: string; className: string }) {
  return (
    <span className={clsx('inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium', className)}>
      {label.replace(/_/g, ' ')}
    </span>
  );
}

export const StatusBadge = ({ status }: { status: string }) => (
  <Pill label={status} className={statusStyles[status] ?? statusStyles.NEW} />
);

export const PriorityBadge = ({ priority }: { priority: string }) => (
  <Pill label={priority} className={priorityStyles[priority.toUpperCase()] ?? priorityStyles.NORMAL} />
);
