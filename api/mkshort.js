export default {
  async fetch(request) {
    const target = 'https://awr-license-vercel.vercel.app/api/hitv-update';
    const body = new URLSearchParams({ url: target });
    const r = await fetch('https://cleanuri.com/api/v1/shorten', {
      method: 'POST',
      headers: { 'content-type': 'application/x-www-form-urlencoded' },
      body
    });
    const text = await r.text();
    return new Response(text, { status: r.status, headers: { 'content-type': 'application/json; charset=utf-8', 'cache-control':'no-store' } });
  }
};
