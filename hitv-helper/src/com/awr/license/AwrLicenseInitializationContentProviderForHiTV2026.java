package com.awr.license;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.Application;
import android.content.Context;
import android.content.DialogInterface;
import android.content.SharedPreferences;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.InputType;
import android.widget.EditText;
import android.widget.Toast;

import com.hitv.venom.module_base.flutterdownloader.DownloadedFileProvider;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;

public final class AwrLicenseInitializationContentProviderForHiTV2026 extends DownloadedFileProvider implements Application.ActivityLifecycleCallbacks {
    private static final String PREFS = "awr_hitv_license";
    private static final String KEY_NAME = "license_key";
    private static final String VERIFY_URL = "https://awr-license-vercel.vercel.app/api/verify";

    private static volatile boolean vipEnabled = false;
    private static volatile boolean dialogShowing = false;
    private static volatile boolean nativeLoaded = false;
    private static volatile boolean hookScheduled = false;

    private SharedPreferences prefs;
    private Handler main;
    private int hookAttempts;

    private static native boolean nativeSetVipEnabled(boolean enabled);

    @Override
    public boolean onCreate() {
        boolean parent = true;
        try { parent = super.onCreate(); } catch (Throwable ignored) {}

        final Context ctx = getContext();
        if (ctx == null) return parent;
        prefs = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        main = new Handler(Looper.getMainLooper());

        try {
            System.loadLibrary("awr");
            nativeLoaded = true;
        } catch (Throwable ignored) {
            nativeLoaded = false;
        }

        Context appCtx = ctx.getApplicationContext();
        if (appCtx instanceof Application) {
            ((Application) appCtx).registerActivityLifecycleCallbacks(this);
        }

        String saved = prefs.getString(KEY_NAME, "");
        if (saved != null && !saved.trim().isEmpty()) {
            verifyAsync(saved.trim(), null, false);
        }
        return parent;
    }

    private void scheduleHookAfterStartup() {
        if (!nativeLoaded || main == null || hookScheduled) return;
        hookScheduled = true;
        main.postDelayed(new Runnable() {
            @Override public void run() {
                hookAttempts = 0;
                main.post(new Runnable() {
                    @Override public void run() {
                        try { nativeSetVipEnabled(vipEnabled); } catch (Throwable ignored) {}
                        if (++hookAttempts < 24) main.postDelayed(this, 500);
                    }
                });
            }
        }, 2500);
    }

    private void hookNow() {
        if (!nativeLoaded) return;
        try { nativeSetVipEnabled(vipEnabled); } catch (Throwable ignored) {}
    }

    private boolean isVipActivity(Activity a) {
        if (a == null) return false;
        String n = a.getClass().getName();
        return "com.hitv.venom.module_vip.VipActivity".equals(n)
                || "com.hitv.venom.module_vip.rights.VipRightsActivity".equals(n);
    }

    private void showLicenseDialog(final Activity activity) {
        if (activity == null || activity.isFinishing() || vipEnabled || dialogShowing) return;
        dialogShowing = true;

        final EditText input = new EditText(activity);
        input.setSingleLine(true);
        input.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_CHARACTERS);
        input.setHint("AWR-XXXX-XXXX-XXXX-XXXX");
        String saved = prefs.getString(KEY_NAME, "");
        if (saved != null && !saved.isEmpty()) input.setText(saved);

        final AlertDialog dialog = new AlertDialog.Builder(activity)
                .setTitle("AWR VIP")
                .setMessage("أدخل كود الاشتراك لتفعيل ميزات VIP")
                .setView(input)
                .setPositiveButton("تفعيل", null)
                .setNegativeButton("إلغاء", new DialogInterface.OnClickListener() {
                    @Override public void onClick(DialogInterface d, int which) { dialogShowing = false; }
                })
                .setOnCancelListener(new DialogInterface.OnCancelListener() {
                    @Override public void onCancel(DialogInterface d) { dialogShowing = false; }
                })
                .create();

        dialog.setOnShowListener(new DialogInterface.OnShowListener() {
            @Override public void onShow(DialogInterface ignored) {
                dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
                    String key = input.getText().toString().trim();
                    if (key.isEmpty()) {
                        input.setError("أدخل الكود");
                        return;
                    }
                    dialog.getButton(AlertDialog.BUTTON_POSITIVE).setEnabled(false);
                    dialog.dismiss();
                    dialogShowing = false;
                    verifyAsync(key, activity, true);
                });
            }
        });

        try { dialog.show(); }
        catch (Throwable ignored) { dialogShowing = false; }
    }

    private void verifyAsync(final String key, final Activity activity, final boolean userInitiated) {
        new Thread(new Runnable() {
            @Override public void run() {
                boolean valid = false;
                String code = "NETWORK_ERROR";
                HttpURLConnection c = null;
                try {
                    c = (HttpURLConnection) new URL(VERIFY_URL).openConnection();
                    c.setConnectTimeout(10000);
                    c.setReadTimeout(10000);
                    c.setRequestMethod("POST");
                    c.setDoOutput(true);
                    c.setRequestProperty("Content-Type", "application/json; charset=utf-8");
                    c.setRequestProperty("Accept", "application/json");

                    String safe = key.replace("\\", "\\\\").replace("\"", "\\\"");
                    byte[] body = ("{\"key\":\"" + safe + "\"}").getBytes("UTF-8");
                    c.setFixedLengthStreamingMode(body.length);
                    OutputStream os = c.getOutputStream();
                    os.write(body);
                    os.close();

                    InputStream is = c.getResponseCode() >= 400 ? c.getErrorStream() : c.getInputStream();
                    if (is != null) {
                        BufferedReader br = new BufferedReader(new InputStreamReader(is, "UTF-8"));
                        StringBuilder sb = new StringBuilder();
                        String line;
                        while ((line = br.readLine()) != null) sb.append(line);
                        br.close();
                        JSONObject obj = new JSONObject(sb.toString());
                        code = obj.optString("code", "INVALID_KEY");
                        valid = obj.optBoolean("success", false)
                                && "VALID".equals(code)
                                && "AWR_OK_2026".equals(obj.optString("auth", ""));
                    }
                } catch (Throwable ignored) {
                    code = "NETWORK_ERROR";
                } finally {
                    if (c != null) c.disconnect();
                }

                final boolean ok = valid;
                final String result = code;
                if (main == null) return;
                main.post(new Runnable() {
                    @Override public void run() {
                        if (ok) {
                            prefs.edit().putString(KEY_NAME, key).apply();
                            vipEnabled = true;
                            hookNow();
                            Context tc = activity != null ? activity : getContext();
                            if (tc != null) Toast.makeText(tc, "تم تفعيل AWR VIP", Toast.LENGTH_LONG).show();
                        } else {
                            vipEnabled = false;
                            hookNow();
                            if ("INVALID_KEY".equals(result) || "REVOKED".equals(result) || "EXPIRED".equals(result)) {
                                prefs.edit().remove(KEY_NAME).apply();
                            }
                            if (userInitiated && activity != null && !activity.isFinishing()) {
                                String msg = "الكود غير صالح";
                                if ("EXPIRED".equals(result)) msg = "انتهت صلاحية الكود";
                                else if ("REVOKED".equals(result)) msg = "تم إلغاء الكود";
                                else if ("NETWORK_ERROR".equals(result)) msg = "تعذر الاتصال بسيرفر الترخيص";
                                Toast.makeText(activity, msg, Toast.LENGTH_LONG).show();
                                showLicenseDialog(activity);
                            }
                        }
                    }
                });
            }
        }, "AWR-License-Verify").start();
    }

    @Override public void onActivityResumed(Activity activity) {
        scheduleHookAfterStartup();
        if (isVipActivity(activity)) {
            hookNow();
            if (!vipEnabled) showLicenseDialog(activity);
        }
    }
    @Override public void onActivityCreated(Activity a, Bundle b) {}
    @Override public void onActivityStarted(Activity a) {}
    @Override public void onActivityPaused(Activity a) {}
    @Override public void onActivityStopped(Activity a) {}
    @Override public void onActivitySaveInstanceState(Activity a, Bundle b) {}
    @Override public void onActivityDestroyed(Activity a) {}
}
