import { HttpErrorResponse } from '@angular/common/http';

export function showErrorAlert(err: any, title = 'Ошибка'): void {
  const msg = formatError(err);
  // маленькая задержка, чтобы не мешать текущему change detection/subscribe
  setTimeout(() => {
    // eslint-disable-next-line no-alert
    alert(`${title}\n\n${msg}`);
    // на всякий лог
    // eslint-disable-next-line no-console
    console.error(title, err);
  });
}

export function formatError(err: any): string {
  if (err instanceof HttpErrorResponse) {
    const status = `HTTP ${err.status} ${err.statusText || ''}`.trim();
    const url = err.url ? `\nURL: ${err.url}` : '';
    let body = '';
    if (typeof err.error === 'string') {
      body = `\n\n${truncate(err.error, 600)}`;
    } else if (err.error) {
      try { body = `\n\n${truncate(JSON.stringify(err.error), 600)}`; } catch {}
    }
    return `${status}${url}${body}`;
  }
  if (err?.message) return err.message;
  try { return JSON.stringify(err); } catch { return String(err); }
}

function truncate(s: string, n: number) { return s && s.length > n ? s.slice(0, n) + '…' : s; }
