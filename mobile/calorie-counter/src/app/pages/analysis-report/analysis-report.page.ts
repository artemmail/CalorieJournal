import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AnalysisService } from '../../services/analysis.service';
import { MarkdownPipe } from '../../pipes/markdown.pipe';

@Component({
  selector: 'app-analysis-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MarkdownPipe],
  templateUrl: './analysis-report.page.html',
  styleUrls: ['./analysis-report.page.scss']
})
export class AnalysisReportPage implements OnInit {
  markdown: string | null = null;
  name = '';
  processing = false;
  loading = false;
  id = 0;

  constructor(private route: ActivatedRoute, private api: AnalysisService) {}

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
    this.api.downloadPdf(this.id).subscribe(res => {
      const blob = res.body;
      if (!blob) return;
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      const disposition = res.headers.get('content-disposition') || '';
      const match = disposition.match(/filename="?([^";]+)"?/);
      a.download = match ? match[1] : 'report.pdf';
      a.href = url;
      a.click();
      window.URL.revokeObjectURL(url);
    });
  }
}

