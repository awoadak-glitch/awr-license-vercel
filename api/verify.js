import {
  env, envInt, json, clientIp, licenseId, getLicenseById,
  activateDevice, deviceCount, expired, rateLimit
} from "../lib/core.js";

const fail = (code, status = 200, extra = {}) => json({ success:false, code, ...extra }, status);

async function readVerifyBody(request) {
  let text = "";
  try { text = await request.text(); } catch { return null; }
  if (!text) return null;

  // Preferred format: JSON.
  try {
    const parsed = JSON.parse(text);
    if (parsed && typeof parsed === "object") return parsed;
  } catch {}

  // Native/libcurl clients commonly send POSTFIELDS using the default
  // application/x-www-form-urlencoded content type. Accept that too.
  try {
    const params = new URLSearchParams(text);
    const body = Object.fromEntries(params.entries());
    return Object.keys(body).length ? body : null;
  } catch {
    return null;
  }
}

export default {
  async fetch(request) {
    if (request.method !== "POST") return fail("METHOD_NOT_ALLOWED", 405);
    try {
      const body = await readVerifyBody(request);
      if (!body) return fail("INVALID_BODY", 400);

      const key = String(body.key || "").trim();
      const deviceId = String(body.device_id || "").trim();
      const packageName = String(body.package_name || "").trim();
      const cert = String(body.cert_sha256 || "").replace(/:/g, "").toUpperCase();
      const appVersion = String(body.app_version || "").trim();

      if (!key || key.length > 128) return fail("KEY_REQUIRED", 400);
      if (!deviceId || deviceId.length > 512) return fail("DEVICE_ID_REQUIRED", 400);

      const ipRL = await rateLimit("verify-ip", clientIp(request), envInt("VERIFY_RATE_LIMIT_PER_MINUTE", 30));
      if (!ipRL.allowed) return fail("RATE_LIMIT", 429, { retry_after:ipRL.retry_after });
      const devRL = await rateLimit("verify-device", deviceId, envInt("VERIFY_DEVICE_RATE_LIMIT_PER_MINUTE", 12));
      if (!devRL.allowed) return fail("RATE_LIMIT", 429, { retry_after:devRL.retry_after });

      const allowedPackage = env("ALLOWED_PACKAGE", "");
      if (allowedPackage && packageName !== allowedPackage) return fail("PACKAGE_MISMATCH");
      const allowedCert = env("ALLOWED_CERT_SHA256", "").replace(/:/g, "").toUpperCase();
      if (allowedCert && cert !== allowedCert) return fail("CERT_MISMATCH");
      const allowedVersion = env("ALLOWED_APP_VERSION", "");
      if (allowedVersion && appVersion !== allowedVersion) return fail("VERSION_MISMATCH");

      const id = licenseId(key);
      const license = await getLicenseById(id);
      if (!license) return fail("INVALID_KEY");
      if (license.revoked) return fail("REVOKED");
      if (expired(license)) return fail("EXPIRED", 200, { expires_at:license.expires_at });

      const activation = await activateDevice(id, deviceId, license.max_devices || 1);
      if (!activation.allowed) {
        return fail("DEVICE_LIMIT", 200, {
          max_devices:license.max_devices || 1,
          devices:await deviceCount(id)
        });
      }

      // Keep the success response intentionally compact for the native client.
      // AWR_OK_2026 is the exact marker searched for inside the APK.
      return json({
        auth:"AWR_OK_2026",
        success:true,
        expires_at:license.expires_at || null
      });
    } catch (error) {
      console.error("verify error", error);
      return fail("SERVER_ERROR", 500);
    }
  }
};
