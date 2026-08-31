import { createECDH, createHash, createHmac } from "node:crypto";
import { json, requiredEnv } from "../lib/core.js";

function b64url(buf) {
  return Buffer.from(buf).toString("base64")
    .replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_");
}

function derivePublicMaterial() {
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
      const kid = createHash("sha256").update(pub).digest("hex").slice(0, 20);
      return { x:b64url(x), y:b64url(y), kid };
    } catch {}
  }
  throw new Error("Unable to derive signing key");
}

export default {
  async fetch(request) {
    if (request.method !== "GET") return json({ success:false, code:"METHOD_NOT_ALLOWED" }, 405);
    try {
      const k = derivePublicMaterial();
      return json({
        success:true,
        code:"OK",
        protocol:"mustaqbal-license-v1",
        alg:"ES256-P1363",
        curve:"P-256",
        kid:k.kid,
        x:k.x,
        y:k.y
      });
    } catch (error) {
      console.error("mustaqbal-key error", error);
      return json({ success:false, code:"SERVER_ERROR" }, 500);
    }
  }
};
