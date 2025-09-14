import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Clipboard, ClipboardModule } from '@angular/cdk/clipboard';
import { AnalysisService } from '../../services/analysis.service';
import { MarkdownPipe } from '../../pipes/markdown.pipe';

@Component({
  selector: 'app-analysis-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatSnackBarModule, ClipboardModule, MarkdownPipe],
  templateUrl: './analysis-report.page.html',
  styleUrls: ['./analysis-report.page.scss']
})
export class AnalysisReportPage implements OnInit {
  markdown: string | null = null;
  name = '';
  processing = false;
  loading = false;
  id = 0;

  constructor(private route: ActivatedRoute, private api: AnalysisService, private sb: MatSnackBar, private clipboard: Clipboard) {}

  async ngOnInit() {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.loading = true;
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

  copyMarkdown() {
    if (!this.markdown) return;
    const ok = this.clipboard.copy(this.markdown);
    if (ok) {
      this.sb.open('Markdown скопирован в буфер обмена.', 'OK', { duration: 2000 });
    } else {
      this.sb.open('Не удалось скопировать Markdown.', 'Закрыть', { duration: 4000 });
    }
  }
}

