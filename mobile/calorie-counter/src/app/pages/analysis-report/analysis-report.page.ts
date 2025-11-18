import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Clipboard, ClipboardModule } from '@angular/cdk/clipboard';
import { HttpResponse } from '@angular/common/http';
import { AnalysisService } from '../../services/analysis.service';
import { MarkdownPipe } from '../../pipes/markdown.pipe';
import { App } from '@capacitor/app';
import { PluginListenerHandle } from '@capacitor/core';

@Component({
  selector: 'app-analysis-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatSnackBarModule, ClipboardModule, MarkdownPipe],
  templateUrl: './analysis-report.page.html',
  styleUrls: ['./analysis-report.page.scss']
})
export class AnalysisReportPage implements OnInit, OnDestroy {
  markdown: string | null = null;
  name = '';
  processing = false;
  loading = false;
  id = 0;
  private backButtonListener?: PluginListenerHandle;

  constructor(
    private route: ActivatedRoute,
    private api: AnalysisService,
    private sb: MatSnackBar,
    private clipboard: Clipboard,
    private location: Location,
    private router: Router
  ) {}

  async ngOnInit() {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.loading = true;
    this.backButtonListener = await App.addListener('backButton', () => this.goBack());
    try {
      const res = await this.api.getById(this.id);
      if (res.status === 'processing') {
        this.processing = true;
      } else {
        this.markdown = res.markdown ?? '';
        this.name = res.name ?? '';
      }
    } finally {
      this.loading = false;
    }
  }

  downloadPdf() {
    if (!this.id) return;
    this.api.createPdfJob(this.id).subscribe({
      next: () => this.sb.open('Отчёт поставлен в очередь. Готовый PDF придёт в Telegram.', 'OK', { duration: 3000 }),
      error: () => this.sb.open('Не удалось поставить отчёт в очередь.', 'Закрыть', { duration: 4000 })
    });
  }

  downloadDocx() {
    if (!this.id) return;
    this.api.downloadDocx(this.id).subscribe({
      next: res => this.saveFile(res, `analysis-${this.id}.docx`, 'Word-файл скачан.'),
      error: () => this.sb.open('Не удалось скачать Word-файл.', 'Закрыть', { duration: 4000 })
    });
  }

  copyMarkdown() {
    if (!this.markdown) return;
    const ok = this.clipboard.copy(this.markdown);
    if (ok) {
      this.sb.open('Markdown скопирован в буфер обмена.', 'OK', { duration: 2000 });
    } else {
      this.sb.open('Не удалось скопировать Markdown.', 'Закрыть', { duration: 4000 });
    }
  }

  goBack() {
    if (history.length > 1) {
      this.location.back();
    } else {
      this.router.navigateByUrl('/analysis');
    }
  }

  ngOnDestroy() {
    this.backButtonListener?.remove();
  }

  private saveFile(res: HttpResponse<Blob>, fallbackName: string, successMessage: string) {
    const blob = res.body;
    if (!blob) {
      this.sb.open('Пустой ответ сервера.', 'Закрыть', { duration: 3000 });
      return;
    }

    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = this.extractFileName(res) ?? fallbackName;
    link.click();
    URL.revokeObjectURL(url);

    this.sb.open(successMessage, 'OK', { duration: 3000 });
  }

  private extractFileName(res: HttpResponse<Blob>): string | null {
    const disposition = res.headers.get('Content-Disposition') ?? res.headers.get('content-disposition');
    if (!disposition) return null;

    const match = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(disposition);
    if (match) {
      const encoded = match[1] ?? match[2];
      try {
        return decodeURIComponent(encoded);
      } catch {
        return encoded;
      }
    }

    return null;
  }
}

