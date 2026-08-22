package com.awr.license;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.Application;
import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.content.DialogInterface;
import android.content.SharedPreferences;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.InputType;
import android.view.Window;
import android.widget.EditText;
import android.widget.Toast;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;

public final class AwrLicenseInitializationContentProviderForHiTV2026 extends ContentProvider implements Application.ActivityLifecycleCallbacks {
    private static final String PREFS = "awr_hitv_license";
    private static final String KEY_NAME = "license_key";
    private static final String VERIFY_URL = "https://awr-license-vercel.vercel.app/api/verify";
    private static volatile boolean vipEnabled = false;
    private static volatile boolean dialogShowing = false;
    private static volatile boolean skipVipDialogForSession = false;
    private static boolean nativeLoaded = false;

    private SharedPreferences prefs;
    private Handler main;
    private int patchAttempts = 0;

    private static native boolean nativeSetVipEnabled(boolean enabled);

    @Override
    public boolean onCreate() {
        Context ctx = getContext();
        if (ctx == null) return true;
        prefs = ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
        main = new Handler(Looper.getMainLooper());
        try {
            System.loadLibrary("awr");
            nativeLoaded = true;
        } catch (Throwable ignored) {
            nativeLoaded = false;
        }

        vipEnabled = false;
        schedulePatch();

        Context appCtx = ctx.getApplicationContext();
        if (appCtx instanceof Application) {
            ((Application) appCtx).registerActivityLifecycleCallbacks(this);
        }

        final String saved = prefs.getString(KEY_NAME, "");
        if (saved != null && !saved.trim().isEmpty()) {
            verifyAsync(saved.trim(), null, false);
        }
        return true;
    }

    private void schedulePatch() {
        if (!nativeLoaded || main == null) return;
        patchAttempts = 0;
        main.post(new Runnable() {
            @Override public void run() {
                boolean ok = false;
                try { ok = nativeSetVipEnabled(vipEnabled); } catch (Throwable ignored) {}
                if (!ok && patchAttempts++ < 40) main.postDelayed(this, 250);
            }
        });
    }

    private void patchNow() {
        if (!nativeLoaded) return;
        try {
            if (!nativeSetVipEnabled(vipEnabled)) schedulePatch();
        } catch (Throwable ignored) {}
    }

    private boolean isVipActivity(Activity a) {
        if (a == null) return false;
        String n = a.getClass().getName();
        return "com.hitv.venom.module_vip.VipActivity".equals(n) || n.contains(".module_vip.");
    }

    private void showLicenseDialog(final Activity activity) {
        if (activity == null || activity.isFinishing() || vipEnabled || dialogShowing || skipVipDialogForSession) return;
        dialogShowing = true;

        final EditText input = new EditText(activity);
        input.setSingleLine(true);
        input.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_CHARACTERS);
        input.setHint("AWR-XXXX-XXXX-XXXX-XXXX");
        String saved = prefs.getString(KEY_NAME, "");
        if (saved != null && !saved.isEmpty()) input.setText(saved);

        final AlertDialog dialog = new AlertDialog.Builder(activity)
                .setTitle("AWR VIP")
                .setMessage("أدخل كود الاشتراك لتفعيل ميزات VIP. يمكنك المتابعة مجانًا بدون تفعيل.")
                .setView(input)
                .setPositiveButton("تفعيل", null)
                .setNegativeButton("متابعة مجانًا", new DialogInterface.OnClickListener() {
                    @Override public void onClick(DialogInterface d, int which) {
                        skipVipDialogForSession = true;
                        dialogShowing = false;
                    }
                })
                .setOnCancelListener(new DialogInterface.OnCancelListener() {
                    @Override public void onCancel(DialogInterface d) { dialogShowing = false; }
                })
                .create();

        dialog.setOnShowListener(new DialogInterface.OnShowListener() {
            @Override public void onShow(DialogInterface ignored) {
                dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
                    final String key = input.getText().toString().trim();
                    if (key.isEmpty()) {
                        input.setError("أدخل الكود");
                        return;
                    }
                    dialog.getButton(AlertDialog.BUTTON_POSITIVE).setEnabled(false);
                    verifyAsync(key, activity, true);
                    dialog.dismiss();
                    dialogShowing = false;
                });
            }
        });
        try {
            Window w = dialog.getWindow();
            dialog.show();
        } catch (Throwable e) {
            dialogShowing = false;
        }
    }

    private void verifyAsync(final String key, final Activity activity, final boolean userInitiated) {
        new Thread(new Runnable() {
            @Override public void run() {
                boolean valid = false;
                String code = "NETWORK_ERROR";
                HttpURLConnection c = null;
                try {
                    URL u = new URL(VERIFY_URL);
                    c = (HttpURLConnection) u.openConnection();
                    c.setConnectTimeout(10000);
                    c.setReadTimeout(10000);
                    c.setRequestMethod("POST");
                    c.setDoOutput(true);
                    c.setRequestProperty("Content-Type", "application/json; charset=utf-8");
                    c.setRequestProperty("Accept", "application/json");
                    String safeKey = key.replace("\\", "\\\\").replace("\"", "\\\"");
                    byte[] body = ("{\"key\":\"" + safeKey + "\"}").getBytes("UTF-8");
                    c.setFixedLengthStreamingMode(body.length);
                    OutputStream os = c.getOutputStream();
                    os.write(body);
                    os.flush();
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
                final String resultCode = code;
                main.post(new Runnable() {
                    @Override public void run() {
                        if (ok) {
                            prefs.edit().putString(KEY_NAME, key).apply();
                            vipEnabled = true;
                            skipVipDialogForSession = false;
                            patchNow();
                            Context toastCtx = activity != null ? activity : getContext();
                            if (toastCtx != null) Toast.makeText(toastCtx, "تم تفعيل AWR VIP", Toast.LENGTH_LONG).show();
                        } else {
                            vipEnabled = false;
                            patchNow();
                            if ("INVALID_KEY".equals(resultCode) || "REVOKED".equals(resultCode) || "EXPIRED".equals(resultCode)) {
                                prefs.edit().remove(KEY_NAME).apply();
                            }
                            if (userInitiated && activity != null && !activity.isFinishing()) {
                                String msg;
                                if ("EXPIRED".equals(resultCode)) msg = "انتهت صلاحية الكود";
                                else if ("REVOKED".equals(resultCode)) msg = "تم إلغاء الكود";
                                else if ("NETWORK_ERROR".equals(resultCode)) msg = "تعذر الاتصال بسيرفر الترخيص";
                                else msg = "الكود غير صالح";
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
        patchNow();
        if (isVipActivity(activity) && !vipEnabled) showLicenseDialog(activity);
    }
    @Override public void onActivityCreated(Activity a, Bundle b) {}
    @Override public void onActivityStarted(Activity a) {}
    @Override public void onActivityPaused(Activity a) {}
    @Override public void onActivityStopped(Activity a) {}
    @Override public void onActivitySaveInstanceState(Activity a, Bundle b) {}
    @Override public void onActivityDestroyed(Activity a) {}

    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder) { return null; }
    @Override public String getType(Uri uri) { return null; }
    @Override public Uri insert(Uri uri, ContentValues values) { return null; }
    @Override public int delete(Uri uri, String selection, String[] selectionArgs) { return 0; }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) { return 0; }
}