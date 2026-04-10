import { useEffect, useState } from "react";

export function JobAlertToggle({ jobId, csrfToken = '' }: { jobId: number; csrfToken?: string }) {
  const [enabled, setEnabled] = useState<boolean | null>(null);

  useEffect(() => {
    fetch(`/api/alerts/job/${jobId}`)
      .then(res => res.json() as Promise<boolean>)
      .then(setEnabled);
  }, [jobId]);

  const toggle = async () => {
    await fetch(`/api/alerts/toggle-job/${jobId}`, {
      method: "POST",
      headers: {
        'RequestVerificationToken': csrfToken,
      },
    });

    setEnabled(prev => !prev);
  };

  if (enabled === null) return null;

  return (
    <label style={{ display: "flex", gap: "8px", alignItems: "center" }}>
      <input
        type="checkbox"
        checked={enabled}
        onChange={toggle}
      />
      Receive alerts for this job
    </label>
  );
}
