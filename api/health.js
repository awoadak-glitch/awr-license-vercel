import { json } from "../lib/core.js";
export default {
  async fetch(request) {
    if (request.method !== "GET") return json({ ok:false, code:"METHOD_NOT_ALLOWED" }, 405);
    return json({
      ok:true,
      service:"AWR License API",
      build:"apk-key-fix-2026-08-21",
      marker:"AWR_OK_2026",
      time:new Date().toISOString()
    });
  }
};
