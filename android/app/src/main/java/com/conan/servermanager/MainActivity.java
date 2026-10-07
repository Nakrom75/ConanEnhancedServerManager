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
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;
import android.app.Dialog;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.FileProvider;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;

import java.io.File;
import java.net.InetAddress;
import java.net.Inet4Address;
import java.net.NetworkInterface;
import java.util.Enumeration;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class MainActivity extends AppCompatActivity {

    private WebView webView;
    private SwipeRefreshLayout swipeRefresh;
    private SharedPreferences prefs;
    private static final String PREF_NAME = "conan_manager_prefs";
    private static final String KEY_SERVER_URL = "last_server_url";
    private long downloadId = -1;

    private Dialog workshopBrowserDialog;
    private WebView workshopWebView;
    private TextView txtBrowserUrl;
    private View modActionBar;
    private TextView txtDetectedMod;
    private Button btnInstallDetectedMod;
    private String detectedModId = null;

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
                if (u.startsWith("file:///android_asset/")) {
                    return false;
                }
                try {
                    Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(u));
                    intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
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

    private void showWorkshopBrowser(String initialUrl) {
        if (isFinishing() || isDestroyed()) return;

        if (workshopBrowserDialog != null && workshopBrowserDialog.isShowing()) {
            if (workshopWebView != null && initialUrl != null && !initialUrl.isEmpty()) {
                workshopWebView.loadUrl(initialUrl);
            }
            return;
        }

        workshopBrowserDialog = new Dialog(this, android.R.style.Theme_DeviceDefault_NoActionBar_Fullscreen);
        workshopBrowserDialog.requestWindowFeature(Window.FEATURE_NO_TITLE);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(0xFF0F172A);

        // --- TOP NAVIGATION TOOLBAR ---
        LinearLayout toolbar = new LinearLayout(this);
        toolbar.setOrientation(LinearLayout.HORIZONTAL);
        toolbar.setBackgroundColor(0xFF1E293B);
        toolbar.setGravity(Gravity.CENTER_VERTICAL);
        int pad = (int) (8 * getResources().getDisplayMetrics().density);
        toolbar.setPadding(pad, pad, pad, pad);

        Button btnClose = new Button(this);
        btnClose.setText("✕");
        btnClose.setTextColor(0xFFF87171);
        btnClose.setBackgroundColor(0x00000000);
        btnClose.setTextSize(18);
        btnClose.setPadding(pad, 0, pad, 0);
        btnClose.setOnClickListener(v -> {
            if (workshopBrowserDialog != null) workshopBrowserDialog.dismiss();
        });
        toolbar.addView(btnClose);

        Button btnBack = new Button(this);
        btnBack.setText("◀");
        btnBack.setTextColor(0xFF38BDF8);
        btnBack.setBackgroundColor(0x00000000);
        btnBack.setTextSize(16);
        btnBack.setPadding(pad, 0, pad, 0);
        btnBack.setOnClickListener(v -> {
            if (workshopWebView != null && workshopWebView.canGoBack()) {
                workshopWebView.goBack();
            }
        });
        toolbar.addView(btnBack);

        Button btnFwd = new Button(this);
        btnFwd.setText("▶");
        btnFwd.setTextColor(0xFF38BDF8);
        btnFwd.setBackgroundColor(0x00000000);
        btnFwd.setTextSize(16);
        btnFwd.setPadding(pad, 0, pad, 0);
        btnFwd.setOnClickListener(v -> {
            if (workshopWebView != null && workshopWebView.canGoForward()) {
                workshopWebView.goForward();
            }
        });
        toolbar.addView(btnFwd);

        txtBrowserUrl = new TextView(this);
        txtBrowserUrl.setText(initialUrl);
        txtBrowserUrl.setTextColor(0xFFE2E8F0);
        txtBrowserUrl.setTextSize(12);
        txtBrowserUrl.setSingleLine(true);
        txtBrowserUrl.setEllipsize(TextUtils.TruncateAt.END);
        LinearLayout.LayoutParams urlLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.0f);
        urlLp.setMargins(pad, 0, pad, 0);
        txtBrowserUrl.setLayoutParams(urlLp);
        toolbar.addView(txtBrowserUrl);

        Button btnReload = new Button(this);
        btnReload.setText("🔄");
        btnReload.setTextColor(0xFFE2E8F0);
        btnReload.setBackgroundColor(0x00000000);
        btnReload.setTextSize(14);
        btnReload.setPadding(pad, 0, pad, 0);
        btnReload.setOnClickListener(v -> {
            if (workshopWebView != null) workshopWebView.reload();
        });
        toolbar.addView(btnReload);

        Button btnExt = new Button(this);
        btnExt.setText("🌐");
        btnExt.setTextColor(0xFFA5B4FC);
        btnExt.setBackgroundColor(0x00000000);
        btnExt.setTextSize(14);
        btnExt.setPadding(pad, 0, pad, 0);
        btnExt.setOnClickListener(v -> {
            if (workshopWebView != null) {
                String u = workshopWebView.getUrl();
                if (u != null && !u.isEmpty()) {
                    try {
                        Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(u));
                        startActivity(intent);
                    } catch (Exception ignored) {}
                }
            }
        });
        toolbar.addView(btnExt);

        root.addView(toolbar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        // --- BROWSER WEBVIEW ---
        workshopWebView = new WebView(this);
        WebSettings ws = workshopWebView.getSettings();
        ws.setJavaScriptEnabled(true);
        ws.setDomStorageEnabled(true);
        ws.setDatabaseEnabled(true);
        ws.setSupportZoom(true);
        ws.setBuiltInZoomControls(true);
        ws.setDisplayZoomControls(false);
        ws.setUseWideViewPort(true);
        ws.setLoadWithOverviewMode(true);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            ws.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        }

        workshopWebView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, android.graphics.Bitmap favicon) {
                if (txtBrowserUrl != null) txtBrowserUrl.setText(url);
                checkModUrl(url);
            }

            @Override
            public void onPageFinished(WebView view, String url) {
                if (txtBrowserUrl != null) {
                    String title = view.getTitle();
                    txtBrowserUrl.setText(title != null && !title.isEmpty() ? title : url);
                }
                checkModUrl(url);
            }

            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
                String u = request.getUrl().toString();
                if (u.startsWith("http://") || u.startsWith("https://")) {
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

        root.addView(workshopWebView, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1.0f));

        // --- SMART MOD DETECTION BANNER ---
        modActionBar = new LinearLayout(this);
        ((LinearLayout) modActionBar).setOrientation(LinearLayout.HORIZONTAL);
        modActionBar.setBackgroundColor(0xFF1E1B4B);
        ((LinearLayout) modActionBar).setGravity(Gravity.CENTER_VERTICAL);
        int padBottom = (int) (12 * getResources().getDisplayMetrics().density);
        modActionBar.setPadding(padBottom, padBottom, padBottom, padBottom);

        txtDetectedMod = new TextView(this);
        txtDetectedMod.setText("🧩 Mod Detected");
        txtDetectedMod.setTextColor(0xFF38BDF8);
        txtDetectedMod.setTextSize(13);
        txtDetectedMod.setTypeface(null, android.graphics.Typeface.BOLD);
        LinearLayout.LayoutParams detLp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1.0f);
        txtDetectedMod.setLayoutParams(detLp);
        ((LinearLayout) modActionBar).addView(txtDetectedMod);

        btnInstallDetectedMod = new Button(this);
        btnInstallDetectedMod.setText("➕ Add to Server");
        btnInstallDetectedMod.setTextColor(0xFFFFFFFF);
        btnInstallDetectedMod.setBackgroundColor(0xFF10B981);
        btnInstallDetectedMod.setTextSize(13);
        btnInstallDetectedMod.setTypeface(null, android.graphics.Typeface.BOLD);
        int padBtnH = (int) (16 * getResources().getDisplayMetrics().density);
        int padBtnV = (int) (8 * getResources().getDisplayMetrics().density);
        btnInstallDetectedMod.setPadding(padBtnH, padBtnV, padBtnH, padBtnV);
        btnInstallDetectedMod.setOnClickListener(v -> {
            if (detectedModId != null && !detectedModId.isEmpty()) {
                webView.evaluateJavascript("if (typeof addModToServer === 'function') addModToServer('" + detectedModId + "');", null);
                Toast.makeText(MainActivity.this, "Adding Mod #" + detectedModId + " to server...", Toast.LENGTH_SHORT).show();
                btnInstallDetectedMod.setText("✅ Adding...");
                btnInstallDetectedMod.setEnabled(false);
                btnInstallDetectedMod.postDelayed(() -> {
                    btnInstallDetectedMod.setText("➕ Add to Server");
                    btnInstallDetectedMod.setEnabled(true);
                }, 3000);
            }
        });
        ((LinearLayout) modActionBar).addView(btnInstallDetectedMod);

        modActionBar.setVisibility(View.GONE);
        root.addView(modActionBar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        workshopBrowserDialog.setContentView(root);

        workshopBrowserDialog.setOnKeyListener((dialog, keyCode, event) -> {
            if (keyCode == KeyEvent.KEYCODE_BACK && event.getAction() == KeyEvent.ACTION_UP) {
                if (workshopWebView != null && workshopWebView.canGoBack()) {
                    workshopWebView.goBack();
                    return true;
                }
                workshopBrowserDialog.dismiss();
                return true;
            }
            return false;
        });

        workshopBrowserDialog.setOnDismissListener(d -> {
            if (workshopWebView != null) {
                workshopWebView.loadUrl("about:blank");
            }
        });

        workshopBrowserDialog.show();
        workshopWebView.loadUrl(initialUrl != null && !initialUrl.isEmpty() ? initialUrl : "https://steamcommunity.com/app/440900/workshop/");
    }

    private void checkModUrl(String url) {
        if (url == null) return;
        Pattern pattern = Pattern.compile("steamcommunity\\.com/sharedfiles/filedetails/\\?id=(\\d+)");
        Matcher matcher = pattern.matcher(url);
        if (matcher.find()) {
            detectedModId = matcher.group(1);
            if (txtDetectedMod != null) {
                txtDetectedMod.setText("🧩 Mod ID: " + detectedModId);
            }
            if (modActionBar != null) {
                modActionBar.setVisibility(View.VISIBLE);
            }
        } else {
            detectedModId = null;
            if (modActionBar != null) {
                modActionBar.setVisibility(View.GONE);
            }
        }
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        try {
            if (workshopBrowserDialog != null && workshopBrowserDialog.isShowing()) {
                workshopBrowserDialog.dismiss();
            }
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
        public String getLocalIpAddress() {
            try {
                for (Enumeration<NetworkInterface> en = NetworkInterface.getNetworkInterfaces(); en.hasMoreElements();) {
                    NetworkInterface intf = en.nextElement();
                    for (Enumeration<InetAddress> enumIpAddr = intf.getInetAddresses(); enumIpAddr.hasMoreElements();) {
                        InetAddress inetAddress = enumIpAddr.nextElement();
                        if (!inetAddress.isLoopbackAddress() && inetAddress instanceof Inet4Address) {
                            return inetAddress.getHostAddress();
                        }
                    }
                }
            } catch (Exception ignored) {}
            return "";
        }

        @JavascriptInterface
        public void openExternalUrl(String url) {
            runOnUiThread(() -> {
                try {
                    Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
                    intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    startActivity(intent);
                } catch (Exception e) {
                    Toast.makeText(MainActivity.this, "Could not open link: " + e.getMessage(), Toast.LENGTH_SHORT).show();
                }
            });
        }

        @JavascriptInterface
        public void openWorkshopBrowser(String url) {
            runOnUiThread(() -> showWorkshopBrowser(url != null && !url.isEmpty() ? url : "https://steamcommunity.com/app/440900/workshop/"));
        }

        @JavascriptInterface
        public String getAppVersion() {
            try {
                return getPackageManager().getPackageInfo(getPackageName(), 0).versionName;
            } catch (Exception ignored) {
                return "1.2.0";
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
                return 10200;
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
