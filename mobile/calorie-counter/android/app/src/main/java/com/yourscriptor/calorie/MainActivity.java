package com.yourscriptor.calorie;

import android.os.Bundle;
import androidx.core.view.WindowCompat;
import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {
  @Override
  protected void onCreate(Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);
    setTheme(R.style.AppTheme_NoActionBar);
    WindowCompat.setDecorFitsSystemWindows(getWindow(), true);
  }
}
