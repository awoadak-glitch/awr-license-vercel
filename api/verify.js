import { json, licenseId, getLicenseById, expired } from "../lib/core.js";

const fail = (code, status = 200, extra = {}) =>
  json({ success:false, code, ...extra }, status);

async function readVerifyBody(request) {
  let text = "";
  try { text = await request.text(); } catch { return null; }
  if (!text) return null;

  try {
    const parsed = JSON.parse(text);
    if (parsed && typeof parsed === "object") return parsed;
  } catch {}

  try {
    const params = new URLSearchParams(text);
    const body = Object.fromEntries(params.entries());
    return Object.keys(body).length ? body : null;
  } catch {
    return null;
  }
}

function hitvExpiryEpochSeconds(license) {
  // HiTV's existing suspend API returns an Integer. Keep the timestamp inside
  // signed 32-bit range; no-expiry licenses use the maximum value (2038-01-19).
  if (!license?.expires_at) return 2147483647;
  const t = Date.parse(license.expires_at);
  if (!Number.isFinite(t)) return 2147483647;
  return Math.max(1, Math.min(2147483647, Math.floor(t / 1000)));
}

export default {
  async fetch(request) {
    try {
      if (request.method === "GET") {
        const url = new URL(request.url);
        const key = String(url.searchParams.get("key") || "").trim();
        if (key === "AWR-2026") {
          return new Response("OK", {
            status: 200,
            headers: { "content-type": "text/plain; charset=utf-8" }
          });
        }
        return new Response("INVALID", {
          status: 200,
          headers: { "content-type": "text/plain; charset=utf-8" }
        });
      }

      if (request.method !== "POST") return fail("METHOD_NOT_ALLOWED", 405);

      const body = await readVerifyBody(request);
      if (!body) return fail("INVALID_BODY", 400);

      // HiTV's existing VIP-code screen sends { code, userId } and expects
      // an integer payload. Normal AWR clients keep using { key } and the
      // existing JSON response format below.
      const hitvMode = body.key == null && body.code != null;
      const key = String(body.key ?? body.code ?? "").trim();
      if (!key || key.length > 128) return fail("KEY_REQUIRED", 400);

      const id = licenseId(key);
      const license = await getLicenseById(id);

      if (!license) return hitvMode ? fail("INVALID_KEY", 401) : fail("INVALID_KEY");
      if (license.revoked) return hitvMode ? fail("REVOKED", 403) : fail("REVOKED");
      if (expired(license)) {
        return hitvMode
          ? fail("EXPIRED", 403, { expires_at:license.expires_at || null })
          : fail("EXPIRED", 200, { expires_at:license.expires_at || null });
      }

      if (hitvMode) return json(hitvExpiryEpochSeconds(license));

      return json({
        auth:"AWR_OK_2026",
        success:true,
        code:"VALID",
        expires_at:license.expires_at || null
      });
    } catch (error) {
      console.error("verify error", error);
      return fail("SERVER_ERROR", 500);
    }
  }
};
