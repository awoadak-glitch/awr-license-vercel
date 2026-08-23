package com.awr.license;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.Application;
import android.content.Context;
import android.content.DialogInterface;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.InputType;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.TextView;
import android.widget.Toast;

import com.captha.didymoi.module_base.flutterdownloader.DownloadedFileProvider;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.util.concurrent.atomic.AtomicBoolean;

public class AwrVipInitializationContentProviderForHiTV326CleanAppXX extends DownloadedFileProvider implements Application.ActivityLifecycleCallbacks {
    private static final String PREFS = "awr_vip_326";
    private static final String KEY_LICENSE = "license_key";
    private static final String KEY_ENABLED = "enabled";
    private static final String API = "https://awr-license-vercel.vercel.app/api/verify?key=";
    private static final int VIEW_ID = 0x4A570326;
    private static final AtomicBoolean HOOK_BUSY = new AtomicBoolean(false);
    private Handler main;
    private SharedPreferences prefs;
    private Application app;

    static {
        try { System.loadLibrary("awr"); } catch (Throwable ignored) {}
    }

    public static native boolean nativeSetVipEnabled(boolean enabled);

    @Override public boolean onCreate() {
        boolean result = true;
        try { result = super.onCreate(); } catch (Throwable ignored) {}
        try {
            Context c = getContext();
            if (c != null) {
                main = new Handler(Looper.getMainLooper());
                prefs = c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
                Context ac = c.getApplicationContext();
                if (ac instanceof Application) {
                    app = (Application) ac;
                    app.registerActivityLifecycleCallbacks(this);
                }
                if (prefs.getBoolean(KEY_ENABLED, false)) scheduleHook();
            }
        } catch (Throwable ignored) {}
        return result;
    }

    private boolean isVipScreen(Activity a) {
        if (a == null) return false;
        String n = a.getClass().getName();
        return n.endsWith(".module_vip.VipActivity") || n.endsWith(".module_vip.rights.VipRightsActivity");
    }

    private void addAwrSection(final Activity a) {
        try {
            View root = a.getWindow().getDecorView();
            if (!(root instanceof ViewGroup)) return;
            ViewGroup vg = (ViewGroup) root;
            if (vg.findViewById(VIEW_ID) != null) return;

            TextView button = new TextView(a);
            button.setId(VIEW_ID);
            button.setText(prefs != null && prefs.getBoolean(KEY_ENABLED, false) ? "AWR VIP ✓" : "AWR VIP");
            button.setTextColor(Color.WHITE);
            button.setTextSize(15f);
            button.setTypeface(Typeface.DEFAULT_BOLD);
            button.setGravity(Gravity.CENTER);
            button.setPadding(dp(a, 18), dp(a, 10), dp(a, 18), dp(a, 10));
            button.setBackgroundColor(Color.rgb(35, 35, 40));
            if (android.os.Build.VERSION.SDK_INT >= 21) button.setElevation(dp(a, 12));
            button.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) { showDialog(a); }
            });

            FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            lp.gravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
            lp.topMargin = dp(a, 42);
            if (vg instanceof FrameLayout) ((FrameLayout) vg).addView(button, lp);
            else vg.addView(button);
        } catch (Throwable ignored) {}
    }

    private int dp(Context c, int v) {
        return (int)(v * c.getResources().getDisplayMetrics().density + 0.5f);
    }

    private void showDialog(final Activity a) {
        try {
            final EditText input = new EditText(a);
            input.setSingleLine(true);
            input.setHint("AWR_2026");
            input.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
            String old = prefs == null ? "" : prefs.getString(KEY_LICENSE, "");
            if (old != null && old.length() > 0) input.setText(old);
            int pad = dp(a, 20);
            FrameLayout box = new FrameLayout(a);
            box.setPadding(pad, dp(a, 8), pad, 0);
            box.addView(input, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

            final AlertDialog dlg = new AlertDialog.Builder(a)
                    .setTitle("AWR VIP")
                    .setMessage("أدخل كود AWR VIP لتفعيل جميع مزايا VIP")
                    .setView(box)
                    .setNegativeButton("إلغاء", null)
                    .setPositiveButton("تفعيل", null)
                    .create();
            dlg.setOnShowListener(new DialogInterface.OnShowListener() {
                @Override public void onShow(final DialogInterface d) {
                    dlg.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(new View.OnClickListener() {
                        @Override public void onClick(View v) {
                            String code = input.getText().toString().trim();
                            if (code.length() == 0) { input.setError("أدخل الكود"); return; }
                            verify(code, a, dlg);
                        }
                    });
                }
            });
            dlg.show();
        } catch (Throwable t) {
            Toast.makeText(a, "تعذر فتح AWR VIP", Toast.LENGTH_SHORT).show();
        }
    }

    private void verify(final String code, final Activity a, final AlertDialog dlg) {
        if (!HOOK_BUSY.compareAndSet(false, true)) return;
        Toast.makeText(a, "جارٍ التحقق...", Toast.LENGTH_SHORT).show();
        new Thread(new Runnable() {
            @Override public void run() {
                boolean ok = false;
                HttpURLConnection conn = null;
                try {
                    URL u = new URL(API + URLEncoder.encode(code, "UTF-8"));
                    conn = (HttpURLConnection) u.openConnection();
                    conn.setConnectTimeout(8000);
                    conn.setReadTimeout(8000);
                    conn.setUseCaches(false);
                    conn.setRequestProperty("Accept", "text/plain");
                    int status = conn.getResponseCode();
                    if (status >= 200 && status < 300) {
                        BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream(), "UTF-8"));
                        String line = br.readLine();
                        br.close();
                        ok = line != null && "OK".equals(line.trim());
                    }
                } catch (Throwable ignored) {
                } finally {
                    if (conn != null) conn.disconnect();
                }
                final boolean success = ok;
                if (main == null) main = new Handler(Looper.getMainLooper());
                main.post(new Runnable() {
                    @Override public void run() {
                        HOOK_BUSY.set(false);
                        if (success) {
                            if (prefs != null) prefs.edit().putString(KEY_LICENSE, code).putBoolean(KEY_ENABLED, true).apply();
                            scheduleHook();
                            refreshButton(a);
                            try { dlg.dismiss(); } catch (Throwable ignored) {}
                            Toast.makeText(a, "تم تفعيل AWR VIP بنجاح", Toast.LENGTH_LONG).show();
                        } else {
                            Toast.makeText(a, "كود AWR VIP غير صالح", Toast.LENGTH_LONG).show();
                        }
                    }
                });
            }
        }, "AWR-VIP-VERIFY").start();
    }

    private void refreshButton(Activity a) {
        try {
            View v = a.getWindow().getDecorView().findViewById(VIEW_ID);
            if (v instanceof TextView) ((TextView)v).setText("AWR VIP ✓");
        } catch (Throwable ignored) {}
    }

    private void scheduleHook() {
        if (main == null) main = new Handler(Looper.getMainLooper());
        int[] delays = new int[]{0, 500, 1500, 3000, 6000, 10000};
        for (final int d : delays) {
            main.postDelayed(new Runnable() {
                @Override public void run() {
                    try { nativeSetVipEnabled(true); } catch (Throwable ignored) {}
                }
            }, d);
        }
    }

    @Override public void onActivityResumed(final Activity activity) {
        try {
            if (prefs != null && prefs.getBoolean(KEY_ENABLED, false)) scheduleHook();
            if (isVipScreen(activity)) {
                if (main == null) main = new Handler(Looper.getMainLooper());
                main.postDelayed(new Runnable() { @Override public void run() { addAwrSection(activity); } }, 300);
            }
        } catch (Throwable ignored) {}
    }

    @Override public void onActivityCreated(Activity a, Bundle b) {}
    @Override public void onActivityStarted(Activity a) {}
    @Override public void onActivityPaused(Activity a) {}
    @Override public void onActivityStopped(Activity a) {}
    @Override public void onActivitySaveInstanceState(Activity a, Bundle b) {}
    @Override public void onActivityDestroyed(Activity a) {}
}
