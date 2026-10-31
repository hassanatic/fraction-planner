export type SessionStatus = "planned" | "delivered" | "missed";

export interface SessionView {
  number: number;
  date: string;
  status: SessionStatus;
}

export interface CourseView {
  id: string;
  patientRef: string;
  prescribed: number;
  delivered: number;
  missed: number;
  status: string;
  sessions: SessionView[];
}

async function send<T>(method: string, url: string, body?: unknown): Promise<T> {
  const response = await fetch(url, {
    method,
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!response.ok) {
    const detail = await response.json().catch(() => null);
    throw new Error(detail?.error ?? `${method} ${url} failed with ${response.status}`);
  }
  return (await response.json()) as T;
}

export const api = {
  list: () => send<CourseView[]>("GET", "/api/courses"),
  create: (patientRef: string, prescribedFractions: number, startDate: string) =>
    send<CourseView>("POST", "/api/courses", { patientRef, prescribedFractions, startDate }),
  deliver: (id: string, number: number) =>
    send<CourseView>("POST", `/api/courses/${id}/sessions/${number}/deliver`, { date: null }),
  miss: (id: string, number: number) =>
    send<CourseView>("POST", `/api/courses/${id}/sessions/${number}/miss`, {}),
};
