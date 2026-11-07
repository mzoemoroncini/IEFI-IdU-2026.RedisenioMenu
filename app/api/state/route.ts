import {env} from 'cloudflare:workers';
import {initialState,mutate} from '../../../lib/restaurant';
import {expandMenu} from '../../../lib/menu-expansion';
export const dynamic='force-dynamic';
const headers={'Cache-Control':'no-store'};
async function read(){
 const db=env.DB!;
 await db.prepare('INSERT OR IGNORE INTO restaurant_state (id,data,version) VALUES (?,?,0)').bind('main',JSON.stringify(expandMenu(initialState()))).run();
 const row=await db.prepare('SELECT data,version FROM restaurant_state WHERE id=?').bind('main').first<{data:string;version:number}>();
 if(!row)throw new Error('No se pudo abrir la base de datos.');
 const previous=JSON.parse(row.data),state=expandMenu(previous);
 if(state!==previous){
  const result=await db.prepare('UPDATE restaurant_state SET data=?,version=version+1 WHERE id=? AND version=?').bind(JSON.stringify(state),'main',row.version).run();
  if(!result.meta.changes)return read();
  return {state,version:row.version+1};
 }
 return {state,version:row.version};
}
export async function GET(){try{return Response.json(await read(),{headers})}catch{return Response.json({error:'No se pudieron cargar los datos. Volvé a intentar.'},{status:503,headers})}}
export async function POST(req:Request){
 if(req.headers.get('origin')&&req.headers.get('origin')!==new URL(req.url).origin)return Response.json({error:'Origen no permitido.'},{status:403});
 try{const a:any=await req.json();const current=await read();if(a.version!==current.version)return Response.json({error:'El pedido cambió en otra ventana. Actualizamos los datos, revisalos y volvé a intentar.',...current},{status:409,headers});
 const state=mutate(current.state,a);const result=await env.DB!.prepare('UPDATE restaurant_state SET data=?,version=version+1 WHERE id=? AND version=?').bind(JSON.stringify(state),'main',current.version).run();
 if(!result.meta.changes)return Response.json({error:'Los datos cambiaron en otra ventana. Actualizá antes de continuar.',...await read()},{status:409,headers});
 return Response.json({state,version:current.version+1},{headers});
 }catch(e){return Response.json({error:e instanceof Error?e.message:'No se pudo guardar. Volvé a intentar.'},{status:400,headers})}
}
