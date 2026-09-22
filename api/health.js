import {
  createECDH, createHash, createHmac, createPrivateKey, sign as cryptoSign
} from "node:crypto";
import {
  json, readJson, clientIp, rateLimit, requiredEnv
} from "../lib/core.js";

const OSCAR_PROTOCOL = "oscar-verify-v1";
const OSCAR_PACKAGE = "com.drama.mp4";
const OSCAR_CERT =
  "77f285ea30dd383d1cb7bdf2fc82039c146e378c0346d475f474917dceeef630";

function b64url(buf) {
  return Buffer.from(buf).toString("base64")
    .replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_");
}

function normalizeSha256(value) {
  return String(value || "").toLowerCase().replace(/[^a-f0-9]/g, "");
}

function deriveOscarSigningMaterial() {
  const pepper = requiredEnv("LICENSE_PEPPER");
  for (let counter = 0; counter < 32; counter++) {
    const d = createHmac("sha256", pepper)
      .update(`${OSCAR_PROTOCOL}:signing:${counter}`)
      .digest();
    const ecdh = createECDH("prime256v1");
    try {
      ecdh.setPrivateKey(d);
      const pub = ecdh.getPublicKey(null, "uncompressed");
      const x = pub.subarray(1, 33);
      const y = pub.subarray(33, 65);
      const jwk = {
        kty: "EC", crv: "P-256",
        x: b64url(x), y: b64url(y), d: b64url(d)
      };
      return {
        key: createPrivateKey({ key: jwk, format: "jwk" }),
        kid: createHash("sha256").update(pub).digest("hex").slice(0, 20)
      };
    } catch {}
  }
  throw new Error("Unable to derive Oscar signing key");
}

function oscarCanonical(p) {
  return JSON.stringify([
    OSCAR_PROTOCOL,
    p.status,
    p.package_name,
    p.cert_sha256,
    p.nonce,
    p.app_version,
    p.issued_at,
    p.expires_at
  ]);
}

function oscarFail(code, status = 200) {
  return json({ success:false, code, protocol:OSCAR_PROTOCOL }, status);
}

async function handleOscar(request) {
  if (request.method === "GET") {
    return json({
      success:true,
      service:"Oscar signature verification",
      protocol:OSCAR_PROTOCOL,
      package_name:OSCAR_PACKAGE
    });
  }
  if (request.method !== "POST") return oscarFail("METHOD_NOT_ALLOWED", 405);

  try {
    const ip = clientIp(request);
    const rl = await rateLimit("oscar-verify", ip, 60, 60);
    if (!rl.allowed) return oscarFail("RATE_LIMITED", 429);

    const body = (await readJson(request)) || {};
    const packageName = String(body.package_name || "").trim();
    const certSha256 = normalizeSha256(body.cert_sha256);
    const nonce = String(body.nonce || "").trim();
    const appVersion = String(body.app_version || "").trim().slice(0, 32);

    if (packageName !== OSCAR_PACKAGE) return oscarFail("PACKAGE_NOT_ALLOWED", 403);
    if (!/^[a-f0-9]{64}$/.test(certSha256)) return oscarFail("CERT_REQUIRED", 400);
    if (certSha256 !== OSCAR_CERT) return oscarFail("CERT_NOT_ALLOWED", 403);
    if (!/^[A-Za-z0-9_-]{20,128}$/.test(nonce)) return oscarFail("NONCE_REQUIRED", 400);

    const issuedAt = Math.floor(Date.now() / 1000);
    const payload = {
      status:"VALID",
      package_name:packageName,
      cert_sha256:certSha256,
      nonce,
      app_version:appVersion || "1.1.4",
      issued_at:issuedAt,
      expires_at:issuedAt + 120
    };

    const signing = deriveOscarSigningMaterial();
    const signature = cryptoSign(
      "sha256",
      Buffer.from(oscarCanonical(payload), "utf8"),
      { key:signing.key, dsaEncoding:"ieee-p1363" }
    );

    return json({
      success:true,
      code:"VALID",
      protocol:OSCAR_PROTOCOL,
      alg:"ES256-P1363",
      kid:signing.kid,
      ...payload,
      signature:b64url(signature)
    });
  } catch (error) {
    console.error("oscar-verify error", error);
    return oscarFail("SERVER_ERROR", 500);
  }
}

export default {
  async fetch(request) {
    const url = new URL(request.url);
    if (url.searchParams.get("service") === "oscar") {
      return handleOscar(request);
    }

    if (request.method !== "GET") {
      return json({ ok:false, code:"METHOD_NOT_ALLOWED" }, 405);
    }
    return json({
      ok:true,
      service:"AWR License API",
      build:"apk-key-fix-2026-08-21",
      marker:"AWR_OK_2026",
      time:new Date().toISOString()
    });
  }
};
