export interface NotificationDto {
  notificationId: number;
  userId: number;
  projectId?: number;
  type: string;
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  readAt?: string;
}

export interface UnreadCountResponse {
  unreadCount: number;
}
