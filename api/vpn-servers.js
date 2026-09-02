import { json, licenseId, getLicenseById, expired, clientIp, rateLimit } from "../lib/core.js";

const MASTER_KEYS = new Set(["AWRVIP", "AWR_2026", "AWR-2026"]);
const VPNGATE = "https://www.vpngate.net/api/iphone/";
const PUBLIC_HEALTH = "https://publicvpnlist.com/api/v1/servers?protocol=openvpn&status=online&fresh_within=1800&sort=score&order=desc&per_page=200";
const MAX_LIST = 500;
const CACHE_MS = 75_000;

let cache = { at: 0, rows: [] };
let healthCache = { at: 0, byEndpoint: new Map(), byIp: new Map() };

function csvLine(line) {
  const out = [];
  let cur = "";
  let q = false;
  for (let i = 0; i < line.length; i++) {
    const c = line[i];
    if (c === '"') {
      if (q && line[i + 1] === '"') { cur += '"'; i++; }
      else q = !q;
    } else if (c === "," && !q) {
      out.push(cur); cur = "";
    } else cur += c;
  }
  out.push(cur);
  return out;
}

function safeInt(v) {
  const n = Number.parseInt(String(v || "0"), 10);
  return Number.isFinite(n) ? n : 0;
}

function decodeCfg(v) {
  try { return Buffer.from(String(v || ""), "base64").toString("utf8"); }
  catch { return ""; }
}

function parseRemote(config, fallbackIp) {
  const lines = String(config || "").split(/\r?\n/);
  for (const line of lines) {
    const m = line.match(/^\s*remote\s+([^\s]+)\s+(\d+)(?:\s+([^\s]+))?/i);
    if (!m) continue;
    const port = safeInt(m[2]);
    const hint = String(m[3] || "").toLowerCase();
    let proto = "auto";
    if (hint.includes("tcp")) proto = "tcp";
    else if (hint.includes("udp")) proto = "udp";
    return { host: m[1] || fallbackIp, port, proto };
  }
  return { host: fallbackIp, port: 0, proto: "auto" };
}

function cfgProto(config, remote) {
  const m = String(config || "").match(/^\s*proto\s+(tcp(?:-client)?|udp)\s*$/mi);
  if (m) return m[1].toLowerCase().startsWith("tcp") ? "tcp" : "udp";
  return remote?.proto || "auto";
}

function flag(code) {
  if (!/^[A-Z]{2}$/.test(code)) return "🌐";
  return String.fromCodePoint(...[...code].map(c => 127397 + c.charCodeAt(0)));
}

function locatorFor(x) {
  return Buffer.from(JSON.stringify({ v: 2, ip: x.ip, p: x.proto, o: x.port, c: x.code }), "utf8").toString("base64url");
}

function readLocator(id) {
  try {
    const x = JSON.parse(Buffer.from(String(id || ""), "base64url").toString("utf8"));
    if (x && x.v === 2 && x.ip) return x;
  } catch {}
  return null;
}

function endpointKey(ip, proto, port) {
  return `${String(ip || "").trim()}|${String(proto || "").toLowerCase()}|${safeInt(port)}`;
}

async function fetchHealth() {
  if (healthCache.byEndpoint.size && Date.now() - healthCache.at < CACHE_MS) return healthCache;
  try {
    const r = await fetch(PUBLIC_HEALTH, {
      headers: { "Accept": "application/json", "User-Agent": "AWR-VPN/2.0" },
      signal: AbortSignal.timeout(9000)
    });
    if (!r.ok) throw new Error(`health HTTP ${r.status}`);
    const payload = await r.json();
    const data = Array.isArray(payload?.data) ? payload.data : [];
    const byEndpoint = new Map();
    const byIp = new Map();
    for (const s of data) {
      const proto = String(s.transport || "").toLowerCase();
      const item = {
        score: Number(s.technical_quality_score || 0),
        latency: Number(s.latency_ms || 0),
        speedMbps: Number(s.speed_mbps || 0),
        checked: String(s.last_checked_at || ""),
        source: String(s.source_name || "PublicVPNList")
      };
      byEndpoint.set(endpointKey(s.ip, proto, s.port), item);
      if (s.ip && !byIp.has(String(s.ip))) byIp.set(String(s.ip), item);
    }
    healthCache = { at: Date.now(), byEndpoint, byIp };
  } catch (e) {
    console.warn("public health unavailable", e?.message || e);
  }
  return healthCache;
}

function qualityOf(x, h) {
  const gateScore = Math.min(38, Math.log10(Math.max(1, x.score)) * 7.5);
  const speedMbps = Math.max(0, x.speed / 1_000_000);
  const speed = Math.min(27, Math.log10(1 + speedMbps) * 11);
  const ping = x.ping > 0 ? Math.max(0, 18 - Math.min(18, x.ping / 20)) : 6;
  const sessions = Math.max(0, 8 - Math.min(8, x.sessions / 80));
  const verified = h ? 18 : 0;
  const hScore = h ? Math.min(12, Math.max(0, Number(h.score || 0)) / 8) : 0;
  return Math.max(1, Math.min(100, Math.round(gateScore + speed + ping + sessions + verified + hScore)));
}

async function fetchRows() {
  if (cache.rows.length && Date.now() - cache.at < CACHE_MS) return cache.rows;
  const [r, health] = await Promise.all([
    fetch(VPNGATE, {
      headers: {
        "User-Agent": "AWR-VPN/2.0 (+https://awr-license-vercel.vercel.app)",
        "Accept": "text/plain,text/csv,*/*"
      },
      signal: AbortSignal.timeout(15000)
    }),
    fetchHealth()
  ]);
  if (!r.ok) throw new Error(`VPN source HTTP ${r.status}`);
  const text = await r.text();
  const lines = text.split(/\r?\n/).filter(Boolean);
  const headerLine = lines.findIndex(x => x.startsWith("#HostName,"));
  if (headerLine < 0) throw new Error("VPN source format changed");
  const headers = csvLine(lines[headerLine]).map(x => x.replace(/^#/, ""));
  const rows = [];
  for (let i = headerLine + 1; i < lines.length; i++) {
    const line = lines[i];
    if (!line || line.startsWith("*")) continue;
    const values = csvLine(line);
    if (values.length < headers.length) continue;
    const x = Object.fromEntries(headers.map((h, idx) => [h, values[idx] ?? ""]));
    const config = decodeCfg(x.OpenVPN_ConfigData_Base64);
    if (!config || !/\bclient\b/i.test(config)) continue;
    const ip = String(x.IP || "").trim();
    if (!ip) continue;
    const remote = parseRemote(config, ip);
    const proto = cfgProto(config, remote);
    const port = remote.port || (proto === "tcp" ? 443 : 1194);
    const code = (x.CountryShort || "--").toUpperCase();
    const row = {
      host: String(x.HostName || ip),
      remoteHost: ip,
      ip,
      port,
      score: safeInt(x.Score),
      ping: safeInt(x.Ping),
      speed: safeInt(x.Speed),
      country: x.CountryLong || x.CountryShort || "Unknown",
      code,
      sessions: safeInt(x.NumVpnSessions),
      proto,
      config
    };
    const h = health.byEndpoint.get(endpointKey(ip, proto, port)) || health.byIp.get(ip) || null;
    row.verified = !!h;
    row.health = h;
    row.quality = qualityOf(row, h);
    row.id = locatorFor(row);
    rows.push(row);
  }
  rows.sort((a, b) =>
    (Number(b.verified) - Number(a.verified)) ||
    (b.quality - a.quality) ||
    (b.score - a.score) ||
    (b.speed - a.speed) ||
    ((a.ping || 9999) - (b.ping || 9999))
  );
  cache = { at: Date.now(), rows };
  return rows;
}

async function validVip(key) {
  const raw = String(key || "").trim();
  if (!raw) return { ok: false, code: "VIP_REQUIRED" };
  if (MASTER_KEYS.has(raw)) return { ok: true, master: true };
  const license = await getLicenseById(licenseId(raw));
  if (!license) return { ok: false, code: "INVALID_KEY" };
  if (license.revoked) return { ok: false, code: "REVOKED" };
  if (expired(license)) return { ok: false, code: "EXPIRED", expires_at: license.expires_at || null };
  return { ok: true, license };
}

function normalizeConfig(item) {
  let cfg = String(item.config || "").trim();
  cfg = cfg.replace(/^\s*remote\s+[^\s]+\s+\d+(?:\s+[^\s]+)?\s*$/gmi, `remote ${item.ip} ${item.port}`);
  if (!/^\s*connect-retry\s+/mi.test(cfg)) cfg += "\nconnect-retry 2 4";
  if (!/^\s*connect-timeout\s+/mi.test(cfg)) cfg += "\nconnect-timeout 8";
  if (!/^\s*resolv-retry\s+/mi.test(cfg)) cfg += "\nresolv-retry 3";
  if (!/^\s*auth-nocache\s*$/mi.test(cfg)) cfg += "\nauth-nocache";
  if (!/^\s*persist-key\s*$/mi.test(cfg)) cfg += "\npersist-key";
  if (!/^\s*persist-tun\s*$/mi.test(cfg)) cfg += "\npersist-tun";
  return cfg + "\n";
}

function publicServer(x, index) {
  return {
    id: x.id,
    name: `${x.country} ${index + 1}`,
    country: x.country,
    country_code: x.code,
    flag: flag(x.code),
    city: "",
    host: x.remoteHost,
    port: x.port,
    ping: x.health?.latency > 0 ? Math.round(x.health.latency) : x.ping,
    speed_bps: x.health?.speedMbps > 0 ? Math.round(x.health.speedMbps * 1_000_000) : x.speed,
    sessions: x.sessions,
    protocol: x.proto,
    premium: true,
    verified: x.verified,
    quality_score: x.quality,
    source: x.verified ? "VPN Gate + PublicVPNList" : "VPN Gate"
  };
}

function choose(rows, locator, country, protocol) {
  const wantsProto = protocol === "udp" || protocol === "tcp" ? protocol : null;
  let item = null;
  let fallback = false;
  if (locator) {
    item = rows.find(x => x.ip === locator.ip && x.proto === locator.p && x.port === safeInt(locator.o));
    if (!item) item = rows.find(x => x.ip === locator.ip && (!wantsProto || x.proto === wantsProto));
  }
  if (!item && country) {
    item = rows.find(x => x.code === country && (!wantsProto || x.proto === wantsProto));
    fallback = !!item;
  }
  if (!item && wantsProto) {
    item = rows.find(x => x.proto === wantsProto);
    fallback = !!item;
  }
  if (!item) {
    item = rows[0] || null;
    fallback = !!item;
  }
  return { item, fallback };
}

export default {
  async fetch(request) {
    try {
      if (request.method !== "GET") return json({ success: false, code: "METHOD_NOT_ALLOWED" }, 405);
      const rl = await rateLimit("vpn_repo", clientIp(request), 120, 60);
      if (!rl.allowed) return json({ success: false, code: "RATE_LIMITED", retry_after: rl.retry_after }, 429);

      const vipKey = request.headers.get("x-awr-vip") || "";
      const auth = await validVip(vipKey);
      if (!auth.ok) return json({ success: false, code: auth.code, message: "AWR-VIP is required to access the VPN repository", expires_at: auth.expires_at || null }, 401);

      const url = new URL(request.url);
      const action = String(url.searchParams.get("action") || "list").toLowerCase();
      const protocol = String(url.searchParams.get("protocol") || "auto").toLowerCase();
      const country = String(url.searchParams.get("country") || "").toUpperCase();
      const rows = await fetchRows();

      if (action === "list") {
        let filtered = rows;
        if (country && country !== "AUTO") filtered = filtered.filter(x => x.code === country);
        if (protocol === "udp" || protocol === "tcp") filtered = filtered.filter(x => x.proto === protocol);
        const servers = filtered.slice(0, MAX_LIST).map(publicServer);
        return json({ success: true, vip: true, source: "AWR Secure Multi-Source Repository", total_live: filtered.length, count: servers.length, servers });
      }

      if (action === "get" || action === "best") {
        const locator = action === "best" ? null : readLocator(url.searchParams.get("id"));
        const selected = choose(rows, locator, country, protocol);
        if (!selected.item) return json({ success: false, code: "NO_LIVE_SERVER" }, 503);
        const item = selected.item;
        return json({ success: true, vip: true, fallback: selected.fallback, server: publicServer(item, 0), ovpn: normalizeConfig(item) });
      }

      return json({ success: false, code: "BAD_ACTION" }, 400);
    } catch (error) {
      console.error("vpn repository error", error);
      return json({ success: false, code: "SERVER_ERROR", message: "VPN repository is temporarily unavailable" }, 500);
    }
  }
};
