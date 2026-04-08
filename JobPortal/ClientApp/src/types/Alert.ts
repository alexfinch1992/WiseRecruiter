/**
 * Mirrors the JSON shape returned by the /api/alerts endpoints.
 * Maps from C# Alert entity serialized in DebugAlertsController / AlertsController.
 */
export interface Alert {
  id: number;
  type: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  linkUrl: string | null;
}
