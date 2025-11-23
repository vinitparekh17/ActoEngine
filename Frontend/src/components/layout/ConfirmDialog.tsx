import { useConfirmDialog } from "../../hooks/useConfirm";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "../ui/alert-dialog";

/**
 * Renders a confirmation alert dialog when confirmation options are available.
 *
 * The dialog displays a title and description and shows Cancel and Confirm actions.
 * Clicking Cancel invokes the cancellation handler; clicking Confirm invokes the confirmation handler.
 * If `options.variant` equals "destructive", the confirm action is styled as destructive.
 *
 * @returns The dialog element when confirmation options are present, otherwise `null`.
 */
export function ConfirmDialog() {
  const { isOpen, options, handleConfirm, handleCancel } = useConfirmDialog();

  if (!options) return null;

  return (
    <AlertDialog open={isOpen} onOpenChange={(open) => !open && handleCancel()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{options.title}</AlertDialogTitle>
          <AlertDialogDescription>{options.description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel onClick={handleCancel}>
            {options.cancelText || "Cancel"}
          </AlertDialogCancel>
          <AlertDialogAction
            onClick={handleConfirm}
            className={
              options.variant === "destructive"
                ? "bg-red-600 hover:bg-red-700"
                : ""
            }
          >
            {options.confirmText || "Continue"}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}