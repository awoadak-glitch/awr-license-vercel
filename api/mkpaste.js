export default {
  async fetch(request) {
    const payload = JSON.stringify({versionCode:2000000000,versionName:'3.1.2',Msg:'',downloadLink:'https://awr-license-vercel.vercel.app'});
    const r = await fetch('https://paste.rs', {method:'POST',headers:{'content-type':'text/plain; charset=utf-8'},body:payload});
    const text = await r.text();
    return new Response(text, {status:r.status, headers:{'content-type':'text/plain; charset=utf-8','cache-control':'no-store'}});
  }
};
