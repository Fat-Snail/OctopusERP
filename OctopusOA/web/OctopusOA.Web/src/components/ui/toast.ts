// Minimal toast utility — replaces ElMessage
export function toast(message: string, type: 'success' | 'error' | 'warning' | 'info' = 'info') {
  const el = document.createElement('div')
  const colorMap = {
    success: 'oklch(0.62 0.14 155)',
    error: 'oklch(0.6 0.2 25)',
    warning: 'oklch(0.72 0.16 75)',
    info: 'oklch(0.62 0.14 220)',
  }
  el.style.cssText = `
    position:fixed;top:20px;left:50%;transform:translateX(-50%);
    background:white;border:1px solid oklch(0.93 0 0);border-radius:6px;
    padding:10px 16px;font-size:13px;color:oklch(0.09 0 0);
    box-shadow:0 4px 12px rgba(0,0,0,.1);z-index:9999;
    display:flex;align-items:center;gap:8px;max-width:360px;
  `
  const dot = document.createElement('span')
  dot.style.cssText = `width:6px;height:6px;border-radius:50%;background:${colorMap[type]};flex:none;`
  el.appendChild(dot)
  el.appendChild(document.createTextNode(message))
  document.body.appendChild(el)
  setTimeout(() => el.remove(), 3000)
}

export async function confirm(message: string, _title?: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const overlay = document.createElement('div')
    overlay.style.cssText = `
      position:fixed;inset:0;background:rgba(0,0,0,0.4);z-index:9998;
      display:flex;align-items:center;justify-content:center;
    `
    const dialog = document.createElement('div')
    dialog.style.cssText = `
      background:white;border-radius:8px;padding:24px;min-width:320px;
      box-shadow:0 8px 32px rgba(0,0,0,0.15);
    `
    dialog.innerHTML = `
      <p style="margin:0 0 20px;font-size:14px;color:oklch(0.09 0 0)">${message}</p>
      <div style="display:flex;gap:8px;justify-content:flex-end">
        <button id="cancel" style="padding:6px 16px;border:1px solid oklch(0.83 0 0);border-radius:6px;background:white;font-size:12px;cursor:pointer">取消</button>
        <button id="ok" style="padding:6px 16px;border:0;border-radius:6px;background:oklch(0.55 0.18 250);color:white;font-size:12px;cursor:pointer">确认</button>
      </div>
    `
    overlay.appendChild(dialog)
    document.body.appendChild(overlay)

    dialog.querySelector('#ok')!.addEventListener('click', () => { overlay.remove(); resolve() })
    dialog.querySelector('#cancel')!.addEventListener('click', () => { overlay.remove(); reject(new Error('cancelled')) })
    overlay.addEventListener('click', e => { if (e.target === overlay) { overlay.remove(); reject(new Error('cancelled')) } })
  })
}
