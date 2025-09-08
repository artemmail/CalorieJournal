package com.yourscriptor.calorie;

import android.os.Bundle;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {
  @Override
  protected void onCreate(Bundle savedInstanceState) {
    super.onCreate(savedInstanceState);
    setTheme(R.style.AppTheme_NoActionBar);
    WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
    ViewCompat.setOnApplyWindowInsetsListener(findViewById(android.R.id.content), (v, insets) -> {
      int bottom = insets.getInsets(WindowInsetsCompat.Type.systemBars()).bottom;
      bridge.getWebView().evaluateJavascript("document.body.style.paddingBottom='" + bottom + "px';", null);
      return insets;
    });
  }
}
