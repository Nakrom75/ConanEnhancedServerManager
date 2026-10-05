package com.conan.servermanager;

import android.annotation.SuppressLint;
import android.app.DownloadManager;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.SharedPreferences;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.os.Vibrator;
import android.provider.Settings;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.FileProvider;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;

import java.io.File;

public class MainActivity extends AppCompatActivity {

    private WebView webView;
    private SwipeRefreshLayout swipeRefresh;
    private SharedPreferences prefs;
    private static final String PREF_NAME = "conan_manager_prefs";
    private static final String KEY_SERVER_URL = "last_server_url";
    private long downloadId = -1;

    @SuppressLint("SetJavaScriptEnabled")
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // Window styling for immersive dark theme
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            getWindow().setStatusBarColor(0xFF0F172A);
            getWindow().setNavigationBarColor(0xFF0F172A);
        }

        prefs = getSharedPreferences(PREF_NAME, MODE_PRIVATE);

        swipeRefresh = new SwipeRefreshLayout(this);
        swipeRefresh.setProgressBackgroundColorSchemeColor(0xFF1E293B);
        swipeRefresh.setColorSchemeColors(0xFF38BDF8, 0xFF6366F1, 0xFF10B981);

        webView = new WebView(this);
        swipeRefresh.addView(webView);
        setContentView(swipeRefresh);

        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setAllowFileAccess(true);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        }

        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onConsoleMessage(android.webkit.ConsoleMessage consoleMessage) {
                android.util.Log.d("ConanWebView", consoleMessage.message() + " -- Line " + consoleMessage.lineNumber() + " of " + consoleMessage.sourceId());
                return true;
            }
        });
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageFinished(WebView view, String url) {
                swipeRefresh.setRefreshing(false);
            }

            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                String u = request.getUrl().toString();
                if (u.startsWith("http://") || u.startsWith("https://") || u.startsWith("file://")) {
                    return false;
                }
                try {
                    Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(u));
                    startActivity(intent);
                    return true;
                } catch (Exception e) {
                    return false;
                }
            }
        });

        swipeRefresh.setOnRefreshListener(() -> {
            webView.evaluateJavascript("if (typeof pollServer === 'function') pollServer();", null);
            swipeRefresh.postDelayed(() -> swipeRefresh.setRefreshing(false), 1200);
        });

        webView.addJavascriptInterface(new AndroidBridge(), "Android");

        webView.loadUrl("file:///android_asset/index.html");

        registerDownloadReceiver();
    }

    private void registerDownloadReceiver() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            registerReceiver(onDownloadComplete, new IntentFilter(DownloadManager.ACTION_DOWNLOAD_COMPLETE), Context.RECEIVER_EXPORTED);
        } else {
            registerReceiver(onDownloadComplete, new IntentFilter(DownloadManager.ACTION_DOWNLOAD_COMPLETE));
        }
    }

    private final BroadcastReceiver onDownloadComplete = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            long id = intent.getLongExtra(DownloadManager.EXTRA_DOWNLOAD_ID, -1);
            if (id == downloadId && downloadId != -1) {
                installDownloadedApk();
            }
        }
    };

    private void installDownloadedApk() {
        try {
            File file = new File(getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), "ConanServerManager-Update.apk");
            if (!file.exists()) return;

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                if (!getPackageManager().canRequestPackageInstalls()) {
                    Intent permIntent = new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES);
                    permIntent.setData(Uri.parse("package:" + getPackageName()));
                    startActivity(permIntent);
                    Toast.makeText(this, "Please enable unknown sources for update, then reopen app.", Toast.LENGTH_LONG).show();
                    return;
                }
            }

            Uri apkUri = FileProvider.getUriForFile(this, getPackageName() + ".provider", file);
            Intent install = new Intent(Intent.ACTION_VIEW);
            install.setDataAndType(apkUri, "application/vnd.android.package-archive");
            install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            install.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            startActivity(install);
        } catch (Exception e) {
            Toast.makeText(this, "Install failed: " + e.getMessage(), Toast.LENGTH_LONG).show();
        }
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        try {
            unregisterReceiver(onDownloadComplete);
        } catch (Exception ignored) {}
    }

    @Override
    public void onBackPressed() {
        webView.evaluateJavascript("if (typeof handleBackPressed === 'function') handleBackPressed(); else false;", value -> {
            if ("\"handled\"".equals(value) || "true".equals(value)) {
                // Handled in JS (e.g. closed a modal or switched back to dashboard)
            } else if (webView.canGoBack()) {
                webView.goBack();
            } else {
                super.onBackPressed();
            }
        });
    }

    public class AndroidBridge {
        @JavascriptInterface
        public void showToast(String message) {
            runOnUiThread(() -> Toast.makeText(MainActivity.this, message, Toast.LENGTH_SHORT).show());
        }

        @JavascriptInterface
        public void vibrate(int durationMs) {
            try {
                Vibrator v = (Vibrator) getSystemService(Context.VIBRATOR_SERVICE);
                if (v != null) v.vibrate(Math.min(durationMs, 500));
            } catch (Exception ignored) {}
        }

        @JavascriptInterface
        public String getSavedServerUrl() {
            return prefs.getString(KEY_SERVER_URL, "http://192.168.1.100:8088");
        }

        @JavascriptInterface
        public void saveServerUrl(String url) {
            prefs.edit().putString(KEY_SERVER_URL, url).apply();
        }

        @JavascriptInterface
        public String getAppVersion() {
            try {
                return getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
            } catch (Exception ignored) {
                return "1.1.3";
            }
        }

        @JavascriptInterface
        public int getAppVersionCode() {
            try {
                if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.P) {
                    return (int) getPackageManager().getPackageInfo(getPackageName(), 0).getLongVersionCode();
                } else {
                    return getPackageManager().getPackageInfo(getPackageName(), 0).versionCode;
                }
            } catch (Exception ignored) {
                return 10103;
            }
        }

        @JavascriptInterface
        public void downloadAndInstallApk(String url, String version) {
            runOnUiThread(() -> {
                try {
                    Toast.makeText(MainActivity.this, "Downloading v" + version + " update...", Toast.LENGTH_SHORT).show();

                    File file = new File(getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), "ConanServerManager-Update.apk");
                    if (file.exists()) file.delete();

                    DownloadManager.Request request = new DownloadManager.Request(Uri.parse(url));
                    request.setTitle("Conan Server Manager v" + version);
                    request.setDescription("Downloading application update...");
                    request.setDestinationInExternalFilesDir(MainActivity.this, Environment.DIRECTORY_DOWNLOADS, "ConanServerManager-Update.apk");
                    request.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);

                    DownloadManager dm = (DownloadManager) getSystemService(Context.DOWNLOAD_SERVICE);
                    if (dm != null) {
                        downloadId = dm.enqueue(request);
                    }
                } catch (Exception e) {
                    Toast.makeText(MainActivity.this, "Update error: " + e.getMessage(), Toast.LENGTH_LONG).show();
                }
            });
        }
    }
}
