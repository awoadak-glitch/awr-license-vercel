export default {
  async fetch() {
    const r = await fetch('https://paste.rs/ue7MH', {redirect:'manual'});
    const text = await r.text();
    return new Response(JSON.stringify({status:r.status,location:r.headers.get('location'),contentType:r.headers.get('content-type'),body:text}), {headers:{'content-type':'application/json','cache-control':'no-store'}});
  }
};
