import {
  env, envInt, json, readJson, clientIp, licenseId, getLicenseById,
  activateDevice, deviceCount, expired, rateLimit
} from "../lib/core.js";

const fail = (code, status = 200, extra = {}) => json({ success:false, code, ...extra }, status);

export default {
  async fetch(request) {
    if (request.method !== "POST") return fail("METHOD_NOT_ALLOWED", 405);
    try {
      const body = await readJson(request);
      if (!body) return fail("INVALID_JSON", 400);

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

      return json({
        success:true,
        code:"VALID",
        license_id:id,
        expires_at:license.expires_at || null,
        max_devices:license.max_devices || 1,
        devices:await deviceCount(id),
        first_activation_on_this_device:!activation.existing,
        server_time:new Date().toISOString()
      });
    } catch (error) {
      console.error("verify error", error);
      return fail("SERVER_ERROR", 500);
    }
  }
};
