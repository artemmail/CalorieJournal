import { Injectable } from '@angular/core';
import { SpeechRecognition } from '@capacitor-community/speech-recognition';

@Injectable({ providedIn: 'root' })
export class VoiceService {
  private listening = false;

  async isAvailable(): Promise<boolean> {
    try {
      const avail = await SpeechRecognition.available();
      return !!(avail as any).available;
    } catch { return false; }
  }

  async ensurePermission() {
    const status: any = await SpeechRecognition.checkPermissions();
    const granted = status?.speechRecognition === 'granted' || status?.permission === 'granted';
    if (!granted) {
      await SpeechRecognition.requestPermissions();
    }
  }

  async listenOnce(lang: string = 'ru-RU'): Promise<string> {
    await this.ensurePermission();
    if (this.listening) await SpeechRecognition.stop();
    this.listening = true;

    return new Promise(async (resolve, reject) => {
      let got = false;
      let h1: any, h2: any;

      const cleanup = async () => {
        try { if (h1?.remove) await h1.remove(); } catch {}
        try { if (h2?.remove) await h2.remove(); } catch {}
        try { await (SpeechRecognition as any).removeAllListeners?.(); } catch {}
        this.listening = false;
      };

      const resultHandler = (data: any) => {
        const matches = data?.matches || data?.value || [];
        const text = (matches[0] || '').toString();
        got = true;
        cleanup();
        resolve(text);
      };

      const stateHandler = (data: any) => {
        if (data?.status === 'stopped' && !got) {
          cleanup();
          resolve('');
        }
      };

      try {
        h1 = await SpeechRecognition.addListener('partialResults', resultHandler);
        h2 = await SpeechRecognition.addListener('listeningState', stateHandler);

        await SpeechRecognition.start({
          language: lang,
          maxResults: 1,
          partialResults: false
        } as any);
      } catch (e) {
        cleanup();
        reject(e);
      }
    });
  }

  /**
   * Парсим голосовой текст на калории/белки/жиры/углеводы.
   * Поддерживает:
   *   - ккал/калории/kcal/cal
   *   - белок/белка/белки/б; жир/жиры/ж; углеводы/у
   *   - оба порядка: "белок 30" и "30 г белка"
   *   - единицы г/гр/g; десятичные запятая/точка
   */
  parseMacros(text: string): { calories?: number; proteins?: number; fats?: number; carbs?: number } {
    // нормализация текста
    let t = (text || '')
      .toLowerCase()
      .replace(/[;,]/g, ' ')
      .replace(/\s+/g, ' ')
      .replace(/ё/g, 'е')
      .trim();

    // аккуратно разворачиваем односимвольные сокращения б/ж/у в слова,
    // чтобы их было проще ловить обычными словами (избегаем lookbehind)
    const letter = '[a-zа-яё]';
    t = t
      .replace(new RegExp(`(^|[^${letter}])б([^${letter}]|$)`, 'giu'), '$1белок$2')
      .replace(new RegExp(`(^|[^${letter}])ж([^${letter}]|$)`, 'giu'), '$1жир$2')
      .replace(new RegExp(`(^|[^${letter}])у([^${letter}]|$)`, 'giu'), '$1углеводы$2');

    const toNum = (s?: string) => {
      if (!s) return undefined;
      const n = Number(s.replace(/\s/g, '').replace(',', '.'));
      return Number.isFinite(n) ? n : undefined;
    };

    // Вспомогалка: берем число из первого сработавшего паттерна (именованная группа n или 1/2)
    const pick = (patterns: RegExp[]): number | undefined => {
      for (const re of patterns) {
        const m = t.match(re);
        if (m) {
          const g: any = (m as any).groups;
          return toNum(g?.n ?? m[1] ?? m[2]);
        }
      }
      return undefined;
    };

    // Число (десятичное)
    const N = String.raw`\d+(?:[.,]\d+)?`;
    // единицы граммов (необязательные)
    const G = String.raw`(?:\s*(?:г|гр|g))?`;
    // разделитель ":", "=" и т.п.
    const SEP = String.raw`\s*(?:[:=])?\s*`;

    // Калории
    const calories = pick([
      new RegExp(String.raw`\b(?:ккал|калори(?:я|и|й|е)|kcal|cal)${SEP}(?<n>${N})\b`, 'iu'),
      new RegExp(String.raw`(?<n>${N})\s*(?:ккал|kcal|cal)\b`, 'iu')
    ]);

    // Белок
    const proteins = pick([
      new RegExp(String.raw`\b(?:белок|белка|белки)${SEP}(?<n>${N})${G}\b`, 'iu'),
      new RegExp(String.raw`(?<n>${N})${G}\s*(?:белка|белок|белки)\b`, 'iu')
    ]);

    // Жиры
    const fats = pick([
      new RegExp(String.raw`\b(?:жир|жиры|жира)${SEP}(?<n>${N})${G}\b`, 'iu'),
      new RegExp(String.raw`(?<n>${N})${G}\s*(?:жир|жиры|жира)\b`, 'iu')
    ]);

    // Углеводы
    const carbs = pick([
      new RegExp(String.raw`\b(?:углевод(?:ы|ов)?|углеводы)${SEP}(?<n>${N})${G}\b`, 'iu'),
      new RegExp(String.raw`(?<n>${N})${G}\s*(?:углевод(?:ы|ов)?|углеводы)\b`, 'iu')
    ]);

    const out: any = { calories, proteins, fats, carbs };

    // Фолбэк: если нашли ≥4 числа подряд, трактуем как [ккал, б, ж, у]
    if ([out.calories, out.proteins, out.fats, out.carbs].some(v => v == null)) {
      const nums = (t.match(new RegExp(N, 'gu')) || []).map(s => Number(s.replace(',', '.')));
      if (nums.length >= 4) {
        out.calories ??= nums[0];
        out.proteins ??= nums[1];
        out.fats     ??= nums[2];
        out.carbs    ??= nums[3];
      }
    }
    return out;
  }
}
