// Helper functions to reduce cognitive complexity

export function getActiveBadgeClass(): string {
  return "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200";
}

export function getInactiveBadgeClass(): string {
  return "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200";
}

export function getButtonText(isEditing: boolean, isPending: boolean): string {
  if (isEditing) {
    return isPending ? "Updating..." : "Update User";
  }
  return isPending ? "Creating..." : "Create User";
}

export function getPasswordChangingText(): string {
  return "Changing...";
}

export function getChangePasswordText(): string {
  return "Change Password";
}
