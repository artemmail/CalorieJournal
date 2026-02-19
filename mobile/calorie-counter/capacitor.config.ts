import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.space.healthymeals',
  appName: 'HealthyMeals',
  webDir: '../../FoodBot/wwwroot',
  bundledWebRuntime: false,
  android: { allowMixedContent: true }
};

export default config;
