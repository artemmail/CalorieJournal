import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.yourscriptor.calorie',
  appName: 'HealthyMeals',
  webDir: '../../FoodBot/wwwroot',
  bundledWebRuntime: false,
  android: { allowMixedContent: true }
};

export default config;
