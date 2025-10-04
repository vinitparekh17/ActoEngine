import { toast } from 'sonner';

type ToastType = 'success' | 'error' | 'info' | 'warning';

interface ToastOptions {
  description?: string;
  duration?: number;
  action?: {
    label: string;
    onClick: () => void;
  };
}

interface ToastPayload extends ToastOptions {
  title: string;
  type?: ToastType;
}

export function useToast() {
  const showToast = (
    arg: string | ToastPayload,
    type: ToastType = 'info',
    options?: ToastOptions
  ) => {
    if (typeof arg === 'string') {
      // old style: (message, type, options)
      toast[type](arg, options);
    } else {
      // new style: ({ title, description, ... })
      const { title, type: argType = 'info', ...rest } = arg;
      toast[argType](title, rest);
    }
  };

  return { showToast };
}
