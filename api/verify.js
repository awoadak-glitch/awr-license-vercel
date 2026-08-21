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

export default {
  async fetch(request) {
    if (request.method !== "POST") return fail("METHOD_NOT_ALLOWED", 405);

    try {
      const body = await readVerifyBody(request);
      if (!body) return fail("INVALID_BODY", 400);

      const key = String(body.key || "").trim();
      if (!key || key.length > 128) return fail("KEY_REQUIRED", 400);

      const id = licenseId(key);
      const license = await getLicenseById(id);

      if (!license) return fail("INVALID_KEY");
      if (license.revoked) return fail("REVOKED");
      if (expired(license)) {
        return fail("EXPIRED", 200, { expires_at:license.expires_at || null });
      }

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
