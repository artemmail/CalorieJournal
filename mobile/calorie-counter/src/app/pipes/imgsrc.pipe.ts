import { Pipe, PipeTransform } from '@angular/core';
import { Capacitor } from '@capacitor/core';

@Pipe({ name: 'imgsrc', standalone: true })
export class ImgSrcPipe implements PipeTransform {
  transform(uri?: string): string { return uri ? Capacitor.convertFileSrc(uri) : ''; }
}
