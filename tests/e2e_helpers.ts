/**
 * Shared helpers for E2E tests.
 *
 * RUN_ID is unique per test-worker invocation.  Using it as a prefix for all
 * test-created records ensures tests never interfere with each other and all
 * artefacts are identifiable for manual or automated cleanup.
 */
export const RUN_ID = `E2E_${Date.now()}`;

export function e2eName(name: string): string {
    return `${RUN_ID}_${name}`;
}
