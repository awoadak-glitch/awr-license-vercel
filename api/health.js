import { json } from "../lib/core.js";
export default {
  async fetch(request) {
    if (request.method !== "GET") return json({ ok:false, code:"METHOD_NOT_ALLOWED" }, 405);
    return json({ ok:true, service:"AWR License API", time:new Date().toISOString() });
  }
};
