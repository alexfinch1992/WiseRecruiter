import React, { useState, useEffect } from 'react';

interface Alert {
  id: number;
  type: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  linkUrl: string | null;
}

export function AlertsPage() {
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const res = await fetch('/api/alerts');
        if (res.ok) {
          setAlerts(await res.json());
        }
      } catch { /* ignore */ }
      setLoading(false);
    })();
  }, []);

  function formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-AU', {
      year: 'numeric', month: 'short', day: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  function typeIcon(type: string): string {
    if (type.startsWith('Recommendation')) return 'fas fa-clipboard-check';
    if (type === 'InterviewCompleted') return 'fas fa-user-check';
    return 'fas fa-bell';
  }

  if (loading) {
    return <div className="text-center py-5 text-muted">Loading alerts...</div>;
  }

  return (
    <div>
      <h4 className="mb-4"><i className="fas fa-bell me-2"></i>All Notifications</h4>
      {alerts.length === 0 ? (
        <div className="text-center py-5 text-muted">
          <i className="fas fa-bell-slash fa-2x mb-3 d-block"></i>
          No notifications yet
        </div>
      ) : (
        <div className="list-group">
          {alerts.map(alert => (
            <a
              key={alert.id}
              href={alert.linkUrl || '#'}
              className="list-group-item list-group-item-action"
              style={{ borderLeft: '3px solid #7B3FF2' }}
            >
              <div className="d-flex justify-content-between align-items-start">
                <div>
                  <i className={`${typeIcon(alert.type)} me-2`} style={{ color: '#7B3FF2' }}></i>
                  <span style={{ fontWeight: 500 }}>{alert.message}</span>
                </div>
                <small className="text-muted ms-3" style={{ whiteSpace: 'nowrap' }}>
                  {formatDate(alert.createdAt)}
                </small>
              </div>
            </a>
          ))}
        </div>
      )}
    </div>
  );
}
