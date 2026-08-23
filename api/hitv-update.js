import { json } from "../lib/core.js";

export default {
  async fetch(request) {
    if (request.method !== "GET") {
      return json({ ok:false, code:"METHOD_NOT_ALLOWED" }, 405);
    }

    return json({
      versionCode: 0,
      versionName: "3.1.2",
      Msg: "",
      downloadLink: "https://awr-license-vercel.vercel.app"
    });
  }
};
