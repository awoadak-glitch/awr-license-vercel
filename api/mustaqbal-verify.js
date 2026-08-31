import {
  createECDH, createHash, createHmac, createPrivateKey, sign as cryptoSign
} from "node:crypto";
import {
  json, readJson, clientIp, licenseId, getLicenseById, expired,
  activateDevice, rateLimit, requiredEnv
} from "../lib/core.js";

function b64url(buf) {
  return Buffer.from(buf).toString("base64")
    .replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_");
}

function deriveSigningMaterial() {
  const pepper = requiredEnv("LICENSE_PEPPER");
  for (let counter = 0; counter < 32; counter++) {
    const d = createHmac("sha256", pepper)
      .update(`mustaqbal-signing-v1:${counter}`)
      .digest();
    const ecdh = createECDH("prime256v1");
    try {
      ecdh.setPrivateKey(d);
      const pub = ecdh.getPublicKey(null, "uncompressed");
      const x = pub.subarray(1, 33);
      const y = pub.subarray(33, 65);
      const jwk = { kty:"EC", crv:"P-256", x:b64url(x), y:b64url(y), d:b64url(d) };
      const key = createPrivateKey({ key:jwk, format:"jwk" });
      const kid = createHash("sha256").update(pub).digest("hex").slice(0, 20);
      return { key, kid };
    } catch {}
  }
  throw new Error("Unable to derive signing key");
}

function deviceFingerprint(raw) {
  return createHash("sha256").update(String(raw || ""), "utf8").digest("hex");
}

function canonicalPayload(p) {
  return JSON.stringify([
    "mustaqbal-license-v1",
    p.status,
    p.license_id,
    p.device_fingerprint,
    p.nonce,
    p.app_version,
    p.client_hash,
    p.issued_at,
    p.token_expires_at,
    p.license_expires_at || "",
    p.remaining_seconds == null ? -1 : p.remaining_seconds
  ]);
}

function fail(code, status = 200, extra = {}) {
  return json({ success:false, code, ...extra }, status);
}

export default {
  async fetch(request) {
    if (request.method !== "POST") return fail("METHOD_NOT_ALLOWED", 405);

    try {
      const ip = clientIp(request);
      const rl = await rateLimit("mustaqbal-verify", ip, 30, 60);
      if (!rl.allowed) return fail("RATE_LIMITED", 429, { retry_after:rl.retry_after });

      const body = (await readJson(request)) || {};
      const key = String(body.key || "").trim();
      const deviceId = String(body.device_id || "").trim();
      const nonce = String(body.nonce || "").trim();
      const appVersion = String(body.app_version || "1.0.0").trim();
      const clientHash = String(body.client_hash || "").trim().toLowerCase();

      if (!key || key.length > 128) return fail("KEY_REQUIRED", 400);
      if (!deviceId || deviceId.length < 16 || deviceId.length > 256) return fail("DEVICE_REQUIRED", 400);
      if (!/^[A-Za-z0-9_-]{20,128}$/.test(nonce)) return fail("NONCE_REQUIRED", 400);
      if (!/^[A-Za-z0-9._-]{1,32}$/.test(appVersion)) return fail("INVALID_APP_VERSION", 400);
      if (!/^[a-f0-9]{64}$/.test(clientHash)) return fail("CLIENT_HASH_REQUIRED", 400);

      const id = licenseId(key);
      const license = await getLicenseById(id);
      if (!license) return fail("INVALID_KEY", 200);
      if (license.revoked) return fail("REVOKED", 200);
      if (expired(license)) return fail("EXPIRED", 200, { expires_at:license.expires_at || null });

      const activation = await activateDevice(id, deviceId, license.max_devices || 1);
      if (!activation.allowed) return fail("DEVICE_LIMIT", 200, { max_devices:license.max_devices || 1 });

      const nowMs = Date.now();
      const issuedAt = Math.floor(nowMs / 1000);
      const tokenExpiresAt = issuedAt + 120;
      const expMs = license.expires_at ? Date.parse(license.expires_at) : NaN;
      const remaining = Number.isFinite(expMs)
        ? Math.max(0, Math.floor((expMs - nowMs) / 1000))
        : null;

      const payload = {
        status:"VALID",
        license_id:id,
        device_fingerprint:deviceFingerprint(deviceId),
        nonce,
        app_version:appVersion,
        client_hash:clientHash,
        issued_at:issuedAt,
        token_expires_at:tokenExpiresAt,
        license_expires_at:license.expires_at || null,
        remaining_seconds:remaining
      };

      const signing = deriveSigningMaterial();
      const signature = cryptoSign(
        "sha256",
        Buffer.from(canonicalPayload(payload), "utf8"),
        { key:signing.key, dsaEncoding:"ieee-p1363" }
      );

      return json({
        success:true,
        code:"VALID",
        protocol:"mustaqbal-license-v1",
        alg:"ES256-P1363",
        kid:signing.kid,
        ...payload,
        signature:b64url(signature)
      });
    } catch (error) {
      console.error("mustaqbal-verify error", error);
      return fail("SERVER_ERROR", 500);
    }
  }
};
