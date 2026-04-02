import React, { useState, useEffect, useRef, useCallback } from 'react';

interface Alert {
  id: number;
  type: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  linkUrl: string | null;
}

export function AlertBell() {
  const [unreadCount, setUnreadCount] = useState(0);
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const fetchUnreadCount = useCallback(async () => {
    try {
      const res = await fetch('/api/alerts/unread-count');
      if (res.ok) {
        const count = await res.json();
        setUnreadCount(count);
      }
    } catch { /* ignore */ }
  }, []);

  const fetchRecent = useCallback(async () => {
    try {
      const res = await fetch('/api/alerts/recent');
      if (res.ok) {
        const data = await res.json();
        setAlerts(data);
        setUnreadCount(0);
      }
    } catch { /* ignore */ }
  }, []);

  // Poll unread count
  useEffect(() => {
    fetchUnreadCount();
    const interval = setInterval(fetchUnreadCount, 45000);
    return () => clearInterval(interval);
  }, [fetchUnreadCount]);

  // Close on outside click
  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('click', handleClick, true);
    return () => document.removeEventListener('click', handleClick, true);
  }, []);

  const handleToggle = () => {
    if (!isOpen) {
      fetchRecent();
    }
    setIsOpen(!isOpen);
  };

  function timeAgo(dateStr: string): string {
    const diff = Date.now() - new Date(dateStr).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return 'just now';
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}h ago`;
    const days = Math.floor(hrs / 24);
    return `${days}d ago`;
  }

  return (
    <div ref={dropdownRef} style={{ position: 'relative', display: 'inline-block' }}>
      <button
        onClick={handleToggle}
        className="btn btn-link nav-link"
        style={{ position: 'relative', padding: '4px 8px', color: '#fff', fontSize: '1.1rem' }}
        title="Alerts"
        aria-label="Alerts"
      >
        <i className="fas fa-bell"></i>
        {unreadCount > 0 && (
          <span style={{
            position: 'absolute',
            top: '0',
            right: '0',
            backgroundColor: '#dc3545',
            color: '#fff',
            borderRadius: '50%',
            width: '18px',
            height: '18px',
            fontSize: '0.65rem',
            fontWeight: 700,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            lineHeight: 1,
          }}>
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div style={{
          position: 'absolute',
          right: 0,
          top: '100%',
          width: '340px',
          backgroundColor: '#fff',
          border: '1px solid #dee2e6',
          borderRadius: '8px',
          boxShadow: '0 4px 16px rgba(0,0,0,0.15)',
          zIndex: 1050,
          maxHeight: '380px',
          overflowY: 'auto',
        }}>
          <div style={{
            padding: '10px 14px',
            borderBottom: '1px solid #eee',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
          }}>
            <strong style={{ fontSize: '0.9rem', color: '#1E1765' }}>Notifications</strong>
            <a href="/Admin/Alerts" style={{ fontSize: '0.78rem', color: '#7B3FF2', textDecoration: 'none' }}>
              View all
            </a>
          </div>

          {alerts.length === 0 ? (
            <div style={{ padding: '24px 14px', textAlign: 'center', color: '#999', fontSize: '0.85rem' }}>
              No recent notifications
            </div>
          ) : (
            alerts.map(alert => (
              <a
                key={alert.id}
                href={alert.linkUrl || '#'}
                style={{
                  display: 'block',
                  padding: '10px 14px',
                  borderBottom: '1px solid #f0f0f0',
                  textDecoration: 'none',
                  color: '#333',
                  fontSize: '0.85rem',
                  transition: 'background 0.15s',
                }}
                onMouseEnter={e => (e.currentTarget.style.backgroundColor = '#f8f9fa')}
                onMouseLeave={e => (e.currentTarget.style.backgroundColor = '')}
              >
                <div style={{ fontWeight: 500, marginBottom: '2px' }}>{alert.message}</div>
                <div style={{ fontSize: '0.72rem', color: '#999' }}>{timeAgo(alert.createdAt)}</div>
              </a>
            ))
          )}
        </div>
      )}
    </div>
  );
}
