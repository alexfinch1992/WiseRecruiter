import { createRoot } from "react-dom/client";
import { JobAlertToggle } from "./components/JobAlertToggle";

const el = document.getElementById("job-alert-toggle-root");

if (el) {
  const jobId = Number(el.getAttribute("data-job-id"));
  const csrfToken = el.getAttribute("data-csrf-token") ?? '';
  createRoot(el).render(<JobAlertToggle jobId={jobId} csrfToken={csrfToken} />);
}
