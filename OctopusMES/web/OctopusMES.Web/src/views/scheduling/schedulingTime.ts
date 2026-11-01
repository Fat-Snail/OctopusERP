export function localDateValue(date = new Date()): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function addLocalDays(date: string, days: number): string {
  const value = new Date(`${date}T00:00:00`);
  value.setDate(value.getDate() + days);
  return localDateValue(value);
}

export function buildSchedulingWindow(
  date: string,
  now = new Date(),
): { from: string; to: string } {
  const today = localDateValue(now);
  const start =
    date === today
      ? new Date(
          now.getFullYear(),
          now.getMonth(),
          now.getDate(),
          now.getHours(),
          now.getMinutes(),
        )
      : new Date(`${date}T00:00:00`);
  const end = new Date(`${date}T00:00:00`);
  end.setDate(end.getDate() + 7);
  return { from: start.toISOString(), to: end.toISOString() };
}

export function buildDayWindow(date: string): { from: string; to: string } {
  const start = new Date(`${date}T00:00:00`);
  const end = new Date(start);
  end.setDate(end.getDate() + 1);
  return { from: start.toISOString(), to: end.toISOString() };
}

export function formatDateTitle(date: string): string {
  const value = new Date(`${date}T00:00:00`);
  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    weekday: "short",
  }).format(value);
}

export function formatLocalTime(value: string): string {
  return new Intl.DateTimeFormat("zh-CN", {
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(new Date(value));
}
