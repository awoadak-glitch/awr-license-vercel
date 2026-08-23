export default {
  async fetch() {
    const payload = JSON.stringify({versionCode:2000000000,versionName:"3.1.2",Msg:"",downloadLink:"https://awr-license-vercel.vercel.app"});
    const fd = new FormData();
    fd.append('file', new Blob([payload], {type:'application/json'}), 'hitv.json');
    const r = await fetch('https://0x0.st', { method:'POST', body:fd });
    return new Response(await r.text(), { status:r.status, headers:{'content-type':'text/plain; charset=utf-8','cache-control':'no-store'} });
  }
};
