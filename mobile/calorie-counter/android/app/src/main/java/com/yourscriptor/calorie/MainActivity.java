package com.yourscriptor.calorie;

import android.os.Bundle;
import android.view.View;
import android.webkit.WebSettings;
import android.webkit.WebView;
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
    View rootView = findViewById(android.R.id.content);
    final WebView webView = bridge.getWebView();

    if (webView != null) {
      WebSettings webSettings = webView.getSettings();
      webSettings.setUseWideViewPort(true);
      webSettings.setLoadWithOverviewMode(true);
      webSettings.setTextZoom(100);
      webView.setInitialScale(100);
    }

    final float density = getResources().getDisplayMetrics().density;
    ViewCompat.setOnApplyWindowInsetsListener(rootView, (v, insets) -> {
      int bottom = insets.getInsets(WindowInsetsCompat.Type.systemBars()).bottom;
      float cssBottom = bottom / density;
      if (webView != null) {
        webView.evaluateJavascript(
          "document.body.style.paddingBottom='" + cssBottom + "px';",
          null
        );
      }
      return insets;
    });
    ViewCompat.requestApplyInsets(rootView);
  }
}
