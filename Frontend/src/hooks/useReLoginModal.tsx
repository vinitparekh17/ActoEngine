import { useState, useCallback, createContext, useContext, ReactNode } from 'react';
import { toast } from 'sonner';
import { ReLoginModal } from '@/components/auth/ReLoginModal';
import { api } from '@/lib/api';

interface ReLoginModalContextValue {
  showReLoginModal: () => void;
  hideReLoginModal: () => void;
  isOpen: boolean;
  isProcessing: boolean;
}

const ReLoginModalContext = createContext<ReLoginModalContextValue | undefined>(undefined);

/**
 * Provides ReLoginModalContext and renders the re-authentication modal alongside its children.
 *
 * Manages modal visibility and processing state, exposes handlers to show and hide the modal,
 * and coordinates processing or cancelling queued API requests after a successful or cancelled re-authentication.
 *
 * @returns A JSX element that supplies re-login modal controls via context and renders the ReLoginModal and children.
 */
export function ReLoginModalProvider({ children }: { children: ReactNode }) {
  const [isOpen, setIsOpen] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);

  const showReLoginModal = useCallback(() => {
    console.log('[ReLoginModal] Showing re-login modal');
    setIsOpen(true);
  }, []);

  const hideReLoginModal = useCallback(() => {
    console.log('[ReLoginModal] Hiding re-login modal');
    setIsOpen(false);
    setIsProcessing(false);
  }, []);

  /**
   * Fixed: Keep modal open during processing, show loading state and error feedback
   */
  const handleSuccess = useCallback(async () => {
    console.log('[ReLoginModal] Re-authentication successful, processing queued requests');
    setIsProcessing(true);

    // Process all queued requests
    try {
      await api.processQueuedRequests();
      console.log('[ReLoginModal] All queued requests processed successfully');

      // Only hide modal on success
      hideReLoginModal();
      toast.success('Successfully reconnected and processed queued requests');
    } catch (error) {
      console.error('[ReLoginModal] Error processing queued requests:', error);
      setIsProcessing(false);

      // Keep modal open, show error to user
      const errorMessage = error instanceof Error ? error.message : 'Failed to process requests';
      toast.error(`Failed to process queued requests: ${errorMessage}`);
    }
  }, [hideReLoginModal]);

  const handleCancel = useCallback(() => {
    console.log('[ReLoginModal] Re-authentication cancelled by user');
    hideReLoginModal();

    // Clear all queued requests
    api.clearQueue();

    // Redirect to login page
    window.location.href = '/login';
  }, [hideReLoginModal]);

  return (
    <ReLoginModalContext.Provider value={{ showReLoginModal, hideReLoginModal, isOpen, isProcessing }}>
      {children}
      <ReLoginModal isOpen={isOpen} onSuccess={handleSuccess} onCancel={handleCancel} />
    </ReLoginModalContext.Provider>
  );
}

/**
 * Accesses the re-login modal context provided by ReLoginModalProvider.
 *
 * @returns The context object containing `showReLoginModal`, `hideReLoginModal`, `isOpen`, and `isProcessing`.
 * @throws Error if called outside of a ReLoginModalProvider
 */
export function useReLoginModal() {
  const context = useContext(ReLoginModalContext);
  if (!context) {
    throw new Error('useReLoginModal must be used within ReLoginModalProvider');
  }
  return context;
}