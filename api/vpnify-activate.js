import { licenseId, getLicenseById, expired } from "../lib/core.js";

const UNLIMITED_KEYS = new Set(["AWRVIP", "AWR_2026", "AWR-2026"]);
const APP_LINK = "vpnify://offer/AWRVIP2026OK?source=awr";

function esc(value = "") {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function page(message = "", ok = false) {
  const msg = esc(message);
  return new Response(`<!doctype html>
<html lang="ar" dir="rtl">
<head>
<meta charset="utf-8">">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>AWR-VIP</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#101218;color:#fff;font-family:system-ui,-apple-system,Segoe UI,Arial;min-height:100vh;display:grid;place-items:center;padding:20px}.card{width:min(430px,100%);background:#191c24;border:1px solid #2b3040;border-radius:22px;padding:26px;box-shadow:0 18px 60px #0008}.logo{font-size:30px;font-weight:800;text-align:center}.sub{opacity:.72;text-align:center;margin:8px 0 24px}.msg{padding:12px 14px;border-radius:12px;margin:0 0 16px;background:${ok ? "#143c2b" : "#402126"};border:1px solid ${ok ? "#286e51" : "#7b3942"}}input{width:100%;padding:15px;border-radius:13px;border:1px solid #373d4d;background:#11141b;color:#fff;font-size:17px;direction:ltr;text-align:center;outline:none}button,.open{display:block;width:100%;margin-top:14px;padding:15px;border:0;border-radius:13px;background:#fff;color:#111;text-align:center;font-weight:800;font-size:16px;text-decoration:none;cursor:pointer}.hint{font-size:12px;opacity:.55;text-align:center;margin-top:16px}</style>
</head><body><main class="card"><div class="logo">AWR-VIP</div><div class="sub">تفعيل اشتراك VIP بواسطة كود AWR</div>${msg ? `<div class="msg">${msg}</div>` : ""}${ok ? `<a class="open" href="${APP_LINK}">فتح التطبيق وتفعيل VIP</a><div class="hint">إذا لم يفتح التطبيق تلقائياً اضغط الزر أعلاه.</div>` : `<form method="post"><input name="key" autocomplete="off" maxlength="128" placeholder="AWR-XXXX-XXXX-XXXX" required><button type="submit">تحقق وتفعيل</button></form><div class="hint">يتم التحقق من الكود عبر خادم AWR.</div>`}</main></body></html>`, {
    status: 200,
    headers: {
      "content-type": "text/html; charset=utf-8",
      "cache-control": "no-store, max-age=0",
      "x-content-type-options": "nosniff"
    }
  });
}

export default {
  async fetch(request) {
    try {
      if (request.method === "GET") return page();
      if (request.method !== "POST") return new Response("Method Not Allowed", { status: 405 });
      const text = await request.text();
      const form = new URLSearchParams(text);
      const key = String(form.get("key") || "").trim();
      if (!key || key.length > 128) return page("أدخل كود AWR-VIP صحيحاً.");

      if (UNLIMITED_KEYS.has(key)) {
        return page("تم التحقق من الكود بنجاح. اضغط فتح التطبيق لإكمال التفعيل.", true);
      }

      const license = await getLicenseById(licenseId(key));
      if (!license) return page("الكود غير صحيح أو غير موجود.");
      if (license.revoked) return page("تم إيقاف هذا الكود.");
      if (expired(license)) return page("انتهت صلاحية هذا الكود.");

      return page("تم التحقق من الكود بنجاح. اضغط فتح التطبيق لإكمال التفعيل.", true);
    } catch (error) {
      console.error("vpnify activate error", error);
      return page("تعذر الاتصال بخادم التفعيل. حاول مرة أخرى.");
    }
  }
};
