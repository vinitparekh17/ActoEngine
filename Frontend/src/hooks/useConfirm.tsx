// ============================================
// hooks/useConfirm.tsx
// ============================================
import { create } from "zustand";

interface ConfirmOptions {
  title: string;
  description: string;
  confirmText?: string;
  cancelText?: string;
  variant?: "default" | "destructive";
}

interface ConfirmState {
  isOpen: boolean;
  options: ConfirmOptions | null;
  resolve: ((value: boolean) => void) | null;
}

const useConfirmStore = create<ConfirmState>(() => ({
  isOpen: false,
  options: null,
  resolve: null,
}));

export function useConfirm() {
  const confirm = (options: ConfirmOptions): Promise<boolean> => {
    return new Promise((resolve) => {
      useConfirmStore.setState({
        isOpen: true,
        options,
        resolve,
      });
    });
  };

  return { confirm };
}

// Internal hook for the dialog component
export function useConfirmDialog() {
  const { isOpen, options, resolve } = useConfirmStore();

  const handleConfirm = () => {
    resolve?.(true);
    useConfirmStore.setState({ isOpen: false, options: null, resolve: null });
  };

  const handleCancel = () => {
    resolve?.(false);
    useConfirmStore.setState({ isOpen: false, options: null, resolve: null });
  };

  return {
    isOpen,
    options,
    handleConfirm,
    handleCancel,
  };
}
