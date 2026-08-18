# AWR License API — GitHub + Vercel

مشروع كامل ترفعه إلى GitHub ثم تستورده في Vercel.

## المحتويات

- `index.html` لوحة إدارة عربية.
- `api/verify.js` للتحقق من المفتاح من التطبيق.
- `api/admin.js` لإنشاء/عرض/إيقاف/تفعيل/تصفير أجهزة/حذف المفاتيح.
- `api/health.js` لاختبار السيرفر.
- `lib/core.js` التشفير والتعامل مع Upstash Redis.
- `tools/generate-secrets.mjs` لتوليد أسرار قوية.

## 1. GitHub

فك الضغط وارفع محتويات المجلد كلها إلى Repository جديد. لا ترفع ملف `.env` حقيقي.

## 2. Upstash Redis

أنشئ Redis Database وخذ:

```text
UPSTASH_REDIS_REST_URL
UPSTASH_REDIS_REST_TOKEN
```

## 3. ولّد أسرار الإدارة

```bash
node tools/generate-secrets.mjs
```

سيعطيك:

```text
ADMIN_TOKEN=...
LICENSE_PEPPER=...
```

لا تضع هذه القيم في GitHub.

## 4. Vercel

اعمل Import لمستودع GitHub ثم أضف Environment Variables:

```text
UPSTASH_REDIS_REST_URL=...
UPSTASH_REDIS_REST_TOKEN=...
ADMIN_TOKEN=...
LICENSE_PEPPER=...
LICENSE_PREFIX=AWR
ALLOWED_PACKAGE=com.tencent.ig
ALLOWED_CERT_SHA256=
ALLOWED_APP_VERSION=
VERIFY_RATE_LIMIT_PER_MINUTE=30
VERIFY_DEVICE_RATE_LIMIT_PER_MINUTE=12
```

المتغيرات الأربعة الأولى مطلوبة. البقية إعدادات.

- اترك `ALLOWED_CERT_SHA256` فارغاً إذا لا تريد فحص بصمة توقيع التطبيق.
- اترك `ALLOWED_APP_VERSION` فارغاً إذا لا تريد فرض إصدار محدد.
- اترك `ALLOWED_PACKAGE` فارغاً إذا لا تريد فرض Package Name.

بعدها اعمل Deploy.

## 5. الاختبار

إذا كان رابط Vercel مثلاً:

```text
https://example.vercel.app
```

اختبر:

```text
https://example.vercel.app/api/health
```

ثم افتح الصفحة الرئيسية:

```text
https://example.vercel.app/
```

أدخل `ADMIN_TOKEN` وأنشئ أول مفتاح.

## API التطبيق

### POST `/api/verify`

```json
{
  "key": "AWR-ABCD-EFGH-JKLM-NPQR",
  "device_id": "UNIQUE_DEVICE_ID",
  "package_name": "com.tencent.ig",
  "app_version": "4.5.0",
  "cert_sha256": ""
}
```

### نجاح

```json
{
  "success": true,
  "code": "VALID",
  "expires_at": "2026-09-17T12:00:00.000Z",
  "max_devices": 1,
  "devices": 1
}
```

### حالات الرفض

```text
INVALID_KEY
EXPIRED
REVOKED
DEVICE_LIMIT
PACKAGE_MISMATCH
CERT_MISMATCH
VERSION_MISMATCH
RATE_LIMIT
```

## API الإدارة

كل طلب إلى `/api/admin` يحتاج:

```http
Authorization: Bearer YOUR_ADMIN_TOKEN
```

### إنشاء

```json
{
  "action": "create",
  "days": 30,
  "max_devices": 1,
  "note": "customer 001"
}
```

`days: 0` = بدون انتهاء.

### عرض المفاتيح

```json
{ "action": "list" }
```

### إيقاف مفتاح

```json
{
  "action": "update",
  "key": "AWR-...",
  "revoked": true
}
```

### إعادة تفعيله

```json
{
  "action": "update",
  "key": "AWR-...",
  "revoked": false
}
```

### تصفير الأجهزة

```json
{
  "action": "reset_devices",
  "key": "AWR-..."
}
```

### حذف

```json
{
  "action": "delete",
  "key": "AWR-..."
}
```

## ملاحظات أمنية

- لا تضع `ADMIN_TOKEN` أو `LICENSE_PEPPER` داخل التطبيق.
- لا تضع بيانات Upstash داخل التطبيق.
- التطبيق يستخدم فقط `/api/verify`.
- المفتاح الكامل يظهر عند الإنشاء فقط؛ السيرفر يخزن HMAC وPreview ولا يخزن المفتاح الخام.
- ربط الأجهزة يتم داخل Redis وبحد الأجهزة المحدد لكل مفتاح.
