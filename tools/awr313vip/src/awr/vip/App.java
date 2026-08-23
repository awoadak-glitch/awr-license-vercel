package awr.vip;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.Application;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
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

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.util.concurrent.atomic.AtomicBoolean;

public class App extends s.h.e.l.l.S implements Application.ActivityLifecycleCallbacks {
    private static final String PREFS = "awr_vip_3132";
    private static final String KEY_CODE = "code";
    private static final String KEY_ACTIVE = "active";
    private static final String API = "https://awr-license-vercel.vercel.app/api/verify?key=";
    private static final int VIEW_ID = 0x4A575650;
    private static final AtomicBoolean BUSY = new AtomicBoolean(false);

    private SharedPreferences prefs;
    private Handler main;

    @Override public void onCreate() {
        super.onCreate();
        try {
            main = new Handler(Looper.getMainLooper());
            prefs = getSharedPreferences(PREFS, Context.MODE_PRIVATE);
            registerActivityLifecycleCallbacks(this);
        } catch (Throwable ignored) {}
    }

    private int dp(Context c, int v) {
        return (int)(v * c.getResources().getDisplayMetrics().density + 0.5f);
    }

    private boolean active() {
        try { return prefs != null && prefs.getBoolean(KEY_ACTIVE, false); }
        catch (Throwable t) { return false; }
    }

    private boolean target(Activity a) {
        if (a == null) return false;
        String n = a.getClass().getName();
        return "com.hitv.savita.module_home.HomeActivityNew".equals(n)
                || "com.hitv.savita.module_me.mine.activity.MineActivity".equals(n)
                || "com.hitv.savita.module_vip.VipActivity".equals(n)
                || "com.hitv.savita.module_vip.rights.VipRightsActivity".equals(n);
    }

    private GradientDrawable chipBackground(Context c) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(Color.rgb(31, 31, 37));
        g.setCornerRadius(dp(c, 18));
        g.setStroke(dp(c, 1), Color.rgb(86, 74, 42));
        return g;
    }

    private void addChip(final Activity a) {
        try {
            View root = a.findViewById(android.R.id.content);
            if (!(root instanceof ViewGroup)) return;
            ViewGroup vg = (ViewGroup) root;
            if (vg.findViewById(VIEW_ID) != null) return;

            TextView chip = new TextView(a);
            chip.setId(VIEW_ID);
            chip.setText(active() ? "◆  AWRVIP  ✓" : "◆  AWRVIP");
            chip.setTextColor(Color.rgb(255, 213, 79));
            chip.setTextSize(14f);
            chip.setTypeface(Typeface.DEFAULT_BOLD);
            chip.setGravity(Gravity.CENTER);
            chip.setSingleLine(true);
            chip.setPadding(dp(a, 15), dp(a, 9), dp(a, 15), dp(a, 9));
            chip.setBackground(chipBackground(a));
            if (android.os.Build.VERSION.SDK_INT >= 21) chip.setElevation(dp(a, 8));
            chip.setOnClickListener(new View.OnClickListener() {
                @Override public void onClick(View v) {
                    if (active()) openVip(a);
                    else showActivation(a);
                }
            });

            FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT,
                    ViewGroup.LayoutParams.WRAP_CONTENT);
            lp.gravity = Gravity.TOP | Gravity.END;
            lp.topMargin = dp(a, 52);
            lp.rightMargin = dp(a, 14);

            if (vg instanceof FrameLayout) ((FrameLayout) vg).addView(chip, lp);
            else vg.addView(chip);
        } catch (Throwable ignored) {}
    }

    private void showActivation(final Activity a) {
        try {
            final EditText input = new EditText(a);
            input.setSingleLine(true);
            input.setHint("AWR-2026");
            input.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
            try {
                String old = prefs == null ? "" : prefs.getString(KEY_CODE, "");
                if (old != null && old.length() > 0) input.setText(old);
            } catch (Throwable ignored) {}

            int p = dp(a, 20);
            FrameLayout box = new FrameLayout(a);
            box.setPadding(p, dp(a, 8), p, 0);
            box.addView(input, new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.WRAP_CONTENT));

            final AlertDialog dlg = new AlertDialog.Builder(a)
                    .setTitle("AWRVIP")
                    .setMessage("أدخل مفتاح AWRVIP لتفعيل العضوية")
                    .setView(box)
                    .setNegativeButton("إلغاء", null)
                    .setPositiveButton("تفعيل", null)
                    .create();

            dlg.setOnShowListener(new DialogInterface.OnShowListener() {
                @Override public void onShow(DialogInterface dialog) {
                    dlg.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(new View.OnClickListener() {
                        @Override public void onClick(View v) {
                            String code = input.getText().toString().trim();
                            if (code.length() == 0) {
                                input.setError("أدخل المفتاح");
                                return;
                            }
                            verify(a, dlg, code);
                        }
                    });
                }
            });
            dlg.show();
        } catch (Throwable t) {
            try { Toast.makeText(a, "تعذر فتح AWRVIP", Toast.LENGTH_SHORT).show(); } catch (Throwable ignored) {}
        }
    }

    private void verify(final Activity a, final AlertDialog dlg, final String code) {
        if (!BUSY.compareAndSet(false, true)) return;
        try { Toast.makeText(a, "جارٍ التحقق من AWRVIP...", Toast.LENGTH_SHORT).show(); } catch (Throwable ignored) {}

        new Thread(new Runnable() {
            @Override public void run() {
                boolean ok = false;
                HttpURLConnection c = null;
                try {
                    URL u = new URL(API + URLEncoder.encode(code, "UTF-8"));
                    c = (HttpURLConnection) u.openConnection();
                    c.setConnectTimeout(8000);
                    c.setReadTimeout(8000);
                    c.setUseCaches(false);
                    c.setRequestProperty("Accept", "text/plain");
                    int status = c.getResponseCode();
                    if (status >= 200 && status < 300) {
                        BufferedReader br = new BufferedReader(new InputStreamReader(c.getInputStream(), "UTF-8"));
                        String line = br.readLine();
                        br.close();
                        ok = line != null && "OK".equals(line.trim());
                    }
                } catch (Throwable ignored) {
                } finally {
                    try { if (c != null) c.disconnect(); } catch (Throwable ignored) {}
                }

                final boolean success = ok;
                if (main == null) main = new Handler(Looper.getMainLooper());
                main.post(new Runnable() {
                    @Override public void run() {
                        BUSY.set(false);
                        if (success) {
                            try {
                                if (prefs != null) prefs.edit().putString(KEY_CODE, code).putBoolean(KEY_ACTIVE, true).apply();
                                refreshChip(a);
                                dlg.dismiss();
                                Toast.makeText(a, "تم تفعيل AWRVIP بنجاح", Toast.LENGTH_LONG).show();
                                openVip(a);
                            } catch (Throwable ignored) {}
                        } else {
                            try { Toast.makeText(a, "مفتاح AWRVIP غير صالح", Toast.LENGTH_LONG).show(); } catch (Throwable ignored) {}
                        }
                    }
                });
            }
        }, "AWRVIP-VERIFY").start();
    }

    private void refreshChip(Activity a) {
        try {
            View v = a.findViewById(VIEW_ID);
            if (v instanceof TextView) ((TextView) v).setText("◆  AWRVIP  ✓");
        } catch (Throwable ignored) {}
    }

    private void openVip(Activity a) {
        try {
            Intent i = new Intent();
            i.setClassName(a, "com.hitv.savita.module_vip.VipActivity");
            i.putExtra("awr_vip", true);
            i.putExtra("vip", true);
            i.putExtra("isVip", true);
            i.putExtra("vip_expire", Long.MAX_VALUE);
            a.startActivity(i);
        } catch (Throwable first) {
            try {
                Intent i = new Intent();
                i.setClassName(a, "com.hitv.savita.module_vip.rights.VipRightsActivity");
                a.startActivity(i);
            } catch (Throwable ignored) {
                try { Toast.makeText(a, "تعذر فتح صفحة VIP", Toast.LENGTH_SHORT).show(); } catch (Throwable ignored2) {}
            }
        }
    }

    @Override public void onActivityResumed(final Activity activity) {
        try {
            if (!target(activity)) return;
            if (main == null) main = new Handler(Looper.getMainLooper());
            main.postDelayed(new Runnable() {
                @Override public void run() { addChip(activity); }
            }, 450);
        } catch (Throwable ignored) {}
    }

    @Override public void onActivityCreated(Activity a, Bundle b) {}
    @Override public void onActivityStarted(Activity a) {}
    @Override public void onActivityPaused(Activity a) {}
    @Override public void onActivityStopped(Activity a) {}
    @Override public void onActivitySaveInstanceState(Activity a, Bundle b) {}
    @Override public void onActivityDestroyed(Activity a) {}
}
