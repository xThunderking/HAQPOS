const baseUrl = (import.meta.env.VITE_API_BASE_URL || "/api").replace(/\/$/, "");

export class ApiError extends Error {
  public status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = "ApiError";
  }
}

async function readResponse<T>(response: Response, notifyUnauthorized = true): Promise<T> {
  if (!response.ok) {
    if (response.status === 401 && notifyUnauthorized)
      window.dispatchEvent(new Event("haqpos:unauthorized"));
    let message = `La API respondió con HTTP ${response.status}.`;
    if (response.headers.get("Content-Type")?.includes("application/problem+json")) {
      const problem = (await response.json()) as { title?: string };
      if (typeof problem.title === "string") message = problem.title.slice(0, 500);
    }
    throw new ApiError(response.status, message);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${baseUrl}/${path.replace(/^\//, "")}`, {
    headers: { Accept: "application/json" },
    credentials: "same-origin",
    cache: "no-store",
    signal,
  });
  return readResponse<T>(response);
}

export async function sendJson<T>(
  path: string,
  body?: unknown,
  method = "POST",
  notifyUnauthorized = true,
): Promise<T> {
  // A fresh token follows the current identity, including after login/password changes.
  const { token } = await getJson<{ token: string }>("auth/csrf");
  const response = await fetch(`${baseUrl}/${path.replace(/^\//, "")}`, {
    method,
    credentials: "same-origin",
    cache: "no-store",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      "X-CSRF-Token": token,
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return readResponse<T>(response, notifyUnauthorized);
}
