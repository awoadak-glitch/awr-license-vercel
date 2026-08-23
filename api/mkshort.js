export default {
  async fetch(request) {
    const target = 'https://awr-license-vercel.vercel.app/api/hitv-update';
    const api = 'https://is.gd/create.php?format=simple&url=' + encodeURIComponent(target);
    const r = await fetch(api, { redirect: 'follow' });
    const text = await r.text();
    return new Response(text, { status: r.status, headers: { 'content-type': 'text/plain; charset=utf-8', 'cache-control':'no-store' } });
  }
};
