import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { AnalysisService } from '../../services/analysis.service';
import { MarkdownPipe } from '../../pipes/markdown.pipe';

@Component({
  selector: 'app-analysis-report',
  standalone: true,
  imports: [CommonModule, MatCardModule, MarkdownPipe],
  templateUrl: './analysis-report.page.html',
  styleUrls: ['./analysis-report.page.scss']
})
export class AnalysisReportPage implements OnInit {
  markdown: string | null = null;
  name = '';
  processing = false;
  loading = false;

  constructor(private route: ActivatedRoute, private api: AnalysisService) {}

  async ngOnInit() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    this.loading = true;
    try {
      const res = await this.api.getById(id);
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
}

