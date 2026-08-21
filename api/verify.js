export default {
  async fetch(request) {
    if (request.method !== "POST") {
      return new Response("METHOD_NOT_ALLOWED", {
        status: 405,
        headers: { "Content-Type": "text/plain; charset=utf-8", "Cache-Control": "no-store" }
      });
    }

    // TEMPORARY DIAGNOSTIC MODE:
    // Any POST receives the exact marker expected by the APK client.
    // This intentionally bypasses Redis/license checks only to prove whether
    // the native verifier actually reaches and parses this endpoint.
    return new Response("AWR_OK_2026", {
      status: 200,
      headers: { "Content-Type": "text/plain; charset=utf-8", "Cache-Control": "no-store" }
    });
  }
};
