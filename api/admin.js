import {
  json, readJson, isAdmin, generateKey, licenseId, previewKey,
  saveLicense, listLicenses, resolveLicense, resetDevices, deleteLicense, deviceCount
} from "../lib/core.js";

const reply = (success, code, extra = {}, status = 200) => json({ success, code, ...extra }, status);

export default {
  async fetch(request) {
    if (request.method !== "POST") return reply(false, "METHOD_NOT_ALLOWED", {}, 405);
    if (!isAdmin(request)) return reply(false, "UNAUTHORIZED", {}, 401);

    try {
      const body = (await readJson(request)) || {};
      const action = String(body.action || "").trim().toLowerCase();

      if (action === "create") {
        const days = Math.max(0, Math.min(3650, Number(body.days ?? 30) || 0));
        const maxDevices = Math.max(1, Math.min(50, Number(body.max_devices ?? 1) || 1));
        const note = String(body.note || "").slice(0, 300);
        for (let attempt = 0; attempt < 8; attempt++) {
          const key = generateKey();
          const id = licenseId(key);
          const now = new Date();
          const record = {
            key_preview:previewKey(key),
            created_at:now.toISOString(),
            expires_at:days === 0 ? null : new Date(now.getTime() + days * 86400000).toISOString(),
            max_devices:maxDevices,
            revoked:false,
            note
          };
          if (await saveLicense(id, record, true)) return reply(true, "CREATED", { key, id, license:record });
        }
        return reply(false, "CREATE_FAILED", {}, 500);
      }

      if (action === "list") {
        return reply(true, "OK", { licenses:await listLicenses() });
      }

      if (["status","update","reset_devices","delete"].includes(action)) {
        const { id, license } = await resolveLicense(body);
        if (!license) return reply(false, "NOT_FOUND", {}, 404);

        if (action === "status") {
          return reply(true, "OK", { id, license:{ ...license, devices:await deviceCount(id) } });
        }

        if (action === "reset_devices") {
          await resetDevices(id);
          return reply(true, "DEVICES_RESET", { id });
        }

        if (action === "delete") {
          await deleteLicense(id);
          return reply(true, "DELETED", { id });
        }

        const updated = { ...license };
        if (typeof body.revoked === "boolean") updated.revoked = body.revoked;
        if (body.max_devices != null) updated.max_devices = Math.max(1, Math.min(50, Number(body.max_devices) || 1));
        if (body.note != null) updated.note = String(body.note || "").slice(0, 300);
        if (body.expires_at !== undefined) {
          if (body.expires_at === null || body.expires_at === "") updated.expires_at = null;
          else {
            const d = new Date(String(body.expires_at));
            if (Number.isNaN(d.getTime())) return reply(false, "INVALID_EXPIRES_AT", {}, 400);
            updated.expires_at = d.toISOString();
          }
        }
        if (body.add_days != null) {
          const add = Math.max(-3650, Math.min(3650, Number(body.add_days) || 0));
          const base = updated.expires_at && Date.parse(updated.expires_at) > Date.now() ? Date.parse(updated.expires_at) : Date.now();
          updated.expires_at = new Date(base + add * 86400000).toISOString();
        }
        await saveLicense(id, updated, false);
        return reply(true, "UPDATED", { id, license:updated });
      }

      return reply(false, "UNKNOWN_ACTION", {}, 400);
    } catch (error) {
      console.error("admin error", error);
      return reply(false, "SERVER_ERROR", {}, 500);
    }
  }
};
