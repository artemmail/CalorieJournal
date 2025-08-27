import { Injectable } from '@angular/core';
import { Preferences } from '@capacitor/preferences';
import { Filesystem, Directory } from '@capacitor/filesystem';

const MEALS_KEY = 'meals_v1';

@Injectable({ providedIn: 'root' })
export class StorageService {
  async loadMeals(): Promise<any[]> {
    const { value } = await Preferences.get({ key: MEALS_KEY });
    if (!value) return [];
    try { return JSON.parse(value); } catch { return []; }
  }

  async saveMeals(meals: any[]): Promise<void> {
    await Preferences.set({ key: MEALS_KEY, value: JSON.stringify(meals) });
  }

  async saveImageBase64ToDataDir(base64: string): Promise<string> {
    const fileName = `meal_${Date.now()}.jpeg`;
    await Filesystem.writeFile({ path: fileName, data: base64, directory: Directory.Data });
    const uri = await Filesystem.getUri({ path: fileName, directory: Directory.Data });
    return uri.uri;
  }
}

