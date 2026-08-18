import { createHmac, randomBytes, timingSafeEqual } from "node:crypto";

export function env(name, fallback = "") {
  const v = process.env[name];
  return v == null ? fallback : String(v).trim();
}
export function envInt(name, fallback) {
  const n = Number.parseInt(env(name, ""), 10);
  return Number.isFinite(n) ? n : fallback;
}
export function requiredEnv(name) {
  const v = env(name, "");
  if (!v) throw new Error(`Missing environment variable: ${name}`);
  return v;
}
export function json(data, status = 200, headers = {}) {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-store, max-age=0",
      ...headers
    }
  });
}
export async function readJson(request) {
  try { return await request.json(); } catch { return null; }
}
export function clientIp(request) {
  const xff = request.headers.get("x-forwarded-for");
  return xff ? xff.split(",")[0].trim() : (request.headers.get("x-real-ip") || "unknown");
}
export async function redis(...command) {
  const url = requiredEnv("UPSTASH_REDIS_REST_URL").replace(/\/+$/, "");
  const token = requiredEnv("UPSTASH_REDIS_REST_TOKEN");
  const r = await fetch(url, {
    method: "POST",
    headers: { "Authorization": `Bearer ${token}`, "Content-Type": "application/json" },
    body: JSON.stringify(command)
  });
  const data = await r.json().catch(() => ({}));
  if (!r.ok || data.error) throw new Error(data.error || `Redis HTTP ${r.status}`);
  return data.result;
}
function hmac(namespace, value) {
  return createHmac("sha256", requiredEnv("LICENSE_PEPPER"))
    .update(`${namespace}:${String(value)}`).digest("hex");
}
export function normalizeKey(value) {
  return String(value || "").trim().toUpperCase().replace(/[^A-Z0-9]/g, "");
}
export const licenseId = key => hmac("license", normalizeKey(key));
export const deviceHash = id => hmac("device", String(id || "").trim());
export function generateKey() {
  const prefix = (env("LICENSE_PREFIX", "AWR") || "AWR").toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 8) || "AWR";
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  const bytes = randomBytes(16);
  let s = "";
  for (let i = 0; i < 16; i++) s += alphabet[bytes[i] % alphabet.length];
  return `${prefix}-${s.slice(0,4)}-${s.slice(4,8)}-${s.slice(8,12)}-${s.slice(12,16)}`;
}
export function previewKey(key) {
  const p = String(key).toUpperCase().split("-");
  return `${p[0] || "KEY"}-****-****-****-${p.at(-1) || "????"}`;
}
function safeEqual(a, b) {
  const aa = Buffer.from(String(a || ""));
  const bb = Buffer.from(String(b || ""));
  return aa.length === bb.length && timingSafeEqual(aa, bb);
}
export function isAdmin(request) {
  const auth = request.headers.get("authorization") || "";
  const bearer = auth.toLowerCase().startsWith("bearer ") ? auth.slice(7).trim() : "";
  return safeEqual(bearer || request.headers.get("x-admin-token") || "", requiredEnv("ADMIN_TOKEN"));
}
const indexKey = "awr:licenses";
export const licKey = id => `awr:license:${id}`;
export const devKey = id => `awr:license:${id}:devices`;
export async function getLicenseById(id) {
  if (!/^[a-f0-9]{64}$/i.test(String(id || ""))) return null;
  const raw = await redis("GET", licKey(id));
  if (!raw) return null;
  try { return JSON.parse(raw); } catch { return null; }
}
export async function resolveLicense(body = {}) {
  const id = body.id && /^[a-f0-9]{64}$/i.test(String(body.id)) ? String(body.id).toLowerCase() : (body.key ? licenseId(body.key) : null);
  return { id, license: id ? await getLicenseById(id) : null };
}
export async function saveLicense(id, record, nx = false) {
  const cmd = ["SET", licKey(id), JSON.stringify(record)];
  if (nx) cmd.push("NX");
  const result = await redis(...cmd);
  if (result === "OK") await redis("SADD", indexKey, id);
  return result === "OK";
}
export async function listLicenses() {
  const ids = (await redis("SMEMBERS", indexKey)) || [];
  const out = [];
  for (const id of ids.slice(0, 500)) {
    const l = await getLicenseById(id);
    if (!l) continue;
    const devices = Number(await redis("SCARD", devKey(id))) || 0;
    out.push({ ...l, id, devices });
  }
  return out.sort((a,b) => String(b.created_at).localeCompare(String(a.created_at)));
}
export async function deleteLicense(id) {
  await redis("DEL", licKey(id));
  await redis("DEL", devKey(id));
  await redis("SREM", indexKey, id);
}
export async function resetDevices(id) { await redis("DEL", devKey(id)); }
export async function deviceCount(id) { return Number(await redis("SCARD", devKey(id))) || 0; }
const activateLua = `
local k=KEYS[1]
local d=ARGV[1]
local m=tonumber(ARGV[2])
if redis.call('SISMEMBER',k,d)==1 then return 2 end
if redis.call('SCARD',k)>=m then return 0 end
redis.call('SADD',k,d)
return 1`;
export async function activateDevice(id, rawDeviceId, maxDevices) {
  const r = Number(await redis("EVAL", activateLua, 1, devKey(id), deviceHash(rawDeviceId), Math.max(1, Number(maxDevices) || 1)));
  return { allowed: r === 1 || r === 2, existing: r === 2 };
}
export function expired(record) {
  if (!record?.expires_at) return false;
  const t = Date.parse(record.expires_at);
  return Number.isFinite(t) && Date.now() >= t;
}
export async function rateLimit(scope, identifier, limit, seconds = 60) {
  const key = `awr:rl:${scope}:${hmac(`rl:${scope}`, identifier || "unknown")}`;
  const count = Number(await redis("INCR", key));
  if (count === 1) await redis("EXPIRE", key, seconds);
  return { allowed: count <= limit, count, limit, retry_after: count <= limit ? 0 : seconds };
}
