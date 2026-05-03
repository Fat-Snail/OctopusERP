/**
 * 把 ISO 时间字符串格式化为北京时间（UTC+8）`yyyy-MM-dd HH:mm:ss`
 */
export function formatBeijingTime(iso: string | null | undefined): string {
  if (!iso) return '-'
  // SQLite / EF Core returns Kind=Unspecified → serialized without Z.
  // Treat all no-timezone strings as UTC to avoid double +8h offset.
  const normalized = /Z|[+-]\d{2}:\d{2}$/.test(iso) ? iso : iso + 'Z'
  const d = new Date(normalized)
  if (isNaN(d.getTime())) return String(iso)

  // 以北京时间（UTC+8）手动解析，避免浏览器时区差异
  const beijing = new Date(d.getTime() + 8 * 60 * 60 * 1000)
  const y = beijing.getUTCFullYear()
  const M = String(beijing.getUTCMonth() + 1).padStart(2, '0')
  const D = String(beijing.getUTCDate()).padStart(2, '0')
  const h = String(beijing.getUTCHours()).padStart(2, '0')
  const m = String(beijing.getUTCMinutes()).padStart(2, '0')
  const s = String(beijing.getUTCSeconds()).padStart(2, '0')
  return `${y}-${M}-${D} ${h}:${m}:${s}`
}

/**
 * 北京时间短格式：MM-dd HH:mm
 */
export function formatBeijingShort(iso: string | null | undefined): string {
  const full = formatBeijingTime(iso)
  if (full === '-') return '-'
  // 从 yyyy-MM-dd HH:mm:ss 截取 MM-dd HH:mm
  return full.slice(5, 16)
}
