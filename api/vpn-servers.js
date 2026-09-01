import { json, licenseId, getLicenseById, expired, clientIp, rateLimit } from "../lib/core.js";

const MASTER_KEYS = new Set(["AWRVIP", "AWR_2026", "AWR-2026"]);
const SOURCE = "https://www.vpngate.net/api/iphone/";
const MAX_LIST = 90;

let cache = { at: 0, rows: [] };

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

function cfgProto(config) {
  const m = String(config || "").match(/^\s*proto\s+(tcp(?:-client)?|udp)\s*$/mi);
  if (!m) return "auto";
  return m[1].toLowerCase().startsWith("tcp") ? "tcp" : "udp";
}

function safeInt(v) {
  const n = Number.parseInt(String(v || "0"), 10);
  return Number.isFinite(n) ? n : 0;
}

function decodeCfg(v) {
  try { return Buffer.from(String(v || ""), "base64").toString("utf8"); }
  catch { return ""; }
}

async function fetchRows() {
  if (cache.rows.length && Date.now() - cache.at < 90_000) return cache.rows;
  const r = await fetch(SOURCE, {
    headers: {
      "User-Agent": "AWR-VPN/1.0 (+https://awr-license-vercel.vercel.app)",
      "Accept": "text/plain,text/csv,*/*"
    },
    signal: AbortSignal.timeout(15000)
  });
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
    if (!config || !/client/i.test(config)) continue;
    const id = `${x.HostName}|${x.IP}`;
    rows.push({
      id,
      host: x.HostName || x.IP,
      ip: x.IP,
      score: safeInt(x.Score),
      ping: safeInt(x.Ping),
      speed: safeInt(x.Speed),
      country: x.CountryLong || x.CountryShort || "Unknown",
      code: (x.CountryShort || "--").toUpperCase(),
      sessions: safeInt(x.NumVpnSessions),
      proto: cfgProto(config),
      config
    });
  }
  rows.sort((a, b) => (b.score - a.score) || (b.speed - a.speed) || (a.ping - b.ping));
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

function flag(code) {
  if (!/^[A-Z]{2}$/.test(code)) return "🌐";
  return String.fromCodePoint(...[...code].map(c => 127397 + c.charCodeAt(0)));
}

export default {
  async fetch(request) {
    try {
      if (request.method !== "GET") return json({ success: false, code: "METHOD_NOT_ALLOWED" }, 405);

      const rl = await rateLimit("vpn_repo", clientIp(request), 90, 60);
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
        const servers = filtered.slice(0, MAX_LIST).map((x, index) => ({
          id: x.id,
          name: `${x.country} ${index + 1}`,
          country: x.country,
          country_code: x.code,
          flag: flag(x.code),
          city: "",
          ping: x.ping,
          speed_bps: x.speed,
          sessions: x.sessions,
          protocol: x.proto,
          premium: true
        }));
        return json({ success: true, vip: true, source: "AWR Secure Repository", count: servers.length, servers });
      }

      if (action === "get") {
        const id = String(url.searchParams.get("id") || "");
        let item = rows.find(x => x.id === id);
        if (!item) return json({ success: false, code: "SERVER_NOT_FOUND" }, 404);
        if ((protocol === "udp" || protocol === "tcp") && item.proto !== protocol) {
          const alt = rows.find(x => x.code === item.code && x.proto === protocol);
          if (alt) item = alt;
        }
        return json({
          success: true,
          vip: true,
          server: {
            id: item.id,
            name: item.country,
            country: item.country,
            country_code: item.code,
            flag: flag(item.code),
            protocol: item.proto,
            ping: item.ping,
            speed_bps: item.speed
          },
          ovpn: item.config
        });
      }

      return json({ success: false, code: "BAD_ACTION" }, 400);
    } catch (error) {
      console.error("vpn repository error", error);
      return json({ success: false, code: "SERVER_ERROR", message: "VPN repository is temporarily unavailable" }, 500);
    }
  }
};
