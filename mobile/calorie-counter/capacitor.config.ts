import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.yourscriptor.calorie',
  appName: 'Calorie Counter',
  webDir: 'dist/calorie-counter/browser',
  bundledWebRuntime: false,
  android: { allowMixedContent: true }
};

export default config;
