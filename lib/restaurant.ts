export type Product={id:string;name:string;description:string;price:number;category:string;image:string;available:boolean};
export type Table={id:string;name:string;zone:string;seats:number};
export type Line={id:string;name:string;price:number;quantity:number;image:string;note?:string};
export type Order={id:string;number:number;tableId:string;tableName:string;status:'open'|'paid'|'cancelled';serviceStatus?:'received'|'preparing'|'ready'|'delivered';paymentMethod?:string;shares?:number[];created:string;closed?:string;items:Line[];discountType:'percent'|'fixed';discount:number;note:string;reason?:string;cashCloseId?:string};
export type CashTotals={gross:number;discount:number;net:number;paidCount:number;cancelledCount:number;cancelledAmount:number;units:number};
export type CashClose={id:string;number:number;date:string;created:string;orders:Order[];summary:CashTotals;openOrders:{id:string;number:number;tableName:string}[]};
export type State={menuRevision?:number;tables:Table[];products:Product[];orders:Order[];nextNumber:number;cashCloses?:CashClose[]};
export const serviceStates={received:'Recibido',preparing:'En preparación',ready:'Listo',delivered:'Entregado'};
export function splitBill(total:number,people:number):number[]{if(!Number.isSafeInteger(total)||total<0||!Number.isSafeInteger(people)||people<1||people>20)throw Error('Elegí entre 1 y 20 personas.');const base=Math.floor(total/people),rest=total%people;return Array.from({length:people},(_,i)=>base+(i<rest?1:0));}
export const categories=['Hamburguesas','Pizzas','Acompañamientos','Bebidas','Postres','Vegetariano','Vegano','Sin TACC'];
export const money=(cents:number)=>new Intl.NumberFormat('es-AR',{style:'currency',currency:'ARS',minimumFractionDigits:0,maximumFractionDigits:2}).format(cents/100);
export function totals(o:Pick<Order,'items'|'discount'|'discountType'>){const subtotal=o.items.reduce((s,l)=>s+l.price*l.quantity,0);const discount=o.discountType==='percent'?Math.round(subtotal*o.discount/10000):o.discount;return {subtotal,discount,total:subtotal-discount,units:o.items.reduce((s,l)=>s+l.quantity,0)}}
export function businessDate(value:string):string{
 const parts=new Intl.DateTimeFormat('en-CA',{timeZone:'America/Argentina/Buenos_Aires',year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(new Date(value));
 return ['year','month','day'].map(key=>parts.find(p=>p.type===key)!.value).join('-');
}
export function cashTotals(orders:Order[]):CashTotals{
 return orders.reduce((r,o)=>{const t=totals(o);if(o.status==='paid'){r.gross+=t.subtotal;r.discount+=t.discount;r.net+=t.total;r.paidCount++;r.units+=t.units}else if(o.status==='cancelled'){r.cancelledCount++;r.cancelledAmount+=t.total}return r},{gross:0,discount:0,net:0,paidCount:0,cancelledCount:0,cancelledAmount:0,units:0});
}
export function cashDay(s:State,date:string){
 const pending=s.orders.filter(o=>o.status!=='open'&&o.closed&&businessDate(o.closed)===date&&!o.cashCloseId);
 const closes=(s.cashCloses||[]).filter(c=>c.date===date);
 const open=s.orders.filter(o=>o.status==='open'&&businessDate(o.created)<=date);
 return {pending,closes,open,summary:cashTotals(pending),canClose:pending.length>0||closes.length===0};
}
export function initialState():State{return {tables:Array.from({length:12},(_,i)=>({id:'table-'+(i+1),name:'Mesa '+String(i+1).padStart(2,'0'),zone:i<8?'Salón':'Terraza',seats:i%3===0?2:4})),orders:[],nextNumber:1,cashCloses:[],products:[
{id:'classic',name:'Clásica de la casa',description:'Carne, cheddar, lechuga y nuestra salsa.',price:850000,category:'Hamburguesas',image:'burger',available:true},
{id:'double',name:'Doble cheddar',description:'Doble carne, doble cheddar y cebolla.',price:1100000,category:'Hamburguesas',image:'burger',available:true},
{id:'pizza',name:'Pizza margarita',description:'Mozzarella, tomate y albahaca fresca.',price:1200000,category:'Pizzas',image:'pizza',available:true},
{id:'fries',name:'Papas crocantes',description:'Porción de papas doradas con dip.',price:450000,category:'Acompañamientos',image:'fries',available:true},
{id:'salad',name:'Ensalada de estación',description:'Hojas frescas, vegetales y vinagreta.',price:650000,category:'Acompañamientos',image:'salad',available:true},
{id:'lemon',name:'Limonada fresca',description:'Limón, menta y jengibre. Vaso de 500 ml.',price:350000,category:'Bebidas',image:'drink',available:true},
{id:'soda',name:'Gaseosa',description:'Vaso de 500 ml. Consultá los sabores.',price:280000,category:'Bebidas',image:'soda',available:true},
{id:'cake',name:'Torta de chocolate',description:'Porción de chocolate con cobertura.',price:520000,category:'Postres',image:'dessert',available:true}]}}
const fail=(m:string):never=>{throw new Error(m)};
const str=(v:unknown,max=200)=>typeof v==='string'&&v.trim().length>0&&v.trim().length<=max?v.trim():fail('Completá los campos con un texto válido.');
const int=(v:unknown,min:number,max:number)=>typeof v==='number'&&Number.isSafeInteger(v)&&v>=min&&v<=max?v:fail('El importe o la cantidad está fuera del rango permitido.');
export function mutate(input:State,a:any,now=new Date().toISOString()):State{
 const s=structuredClone(input);if(!a||typeof a.type!=='string')fail('La acción no es válida.');
 if(a.type==='cashClose'){
  const date=a.date;
  if(typeof date!=='string'||!/^\d{4}-\d{2}-\d{2}$/.test(date)||!Number.isFinite(Date.parse(date+'T12:00:00Z'))||new Date(date+'T12:00:00Z').toISOString().slice(0,10)!==date)fail('Elegí una fecha válida para el cierre.');
  if(date>businessDate(now))fail('No se puede cerrar una fecha futura.');
  const day=cashDay(s,date);
  if(!day.canClose)fail('Esta fecha ya está cerrada y no tiene nuevos movimientos.');
  if(day.open.length&&a.acknowledgeOpen!==true)fail('Hay mesas con pedidos abiertos. Confirmá que querés cerrar solo los pedidos finalizados.');
  const close:CashClose={id:crypto.randomUUID(),number:Math.max(0,...(s.cashCloses||[]).map(c=>c.number))+1,date,created:now,orders:structuredClone(day.pending),summary:day.summary,openOrders:day.open.map(o=>({id:o.id,number:o.number,tableName:o.tableName}))};
  s.cashCloses??=[];s.cashCloses.push(close);
  const included=new Set(day.pending.map(o=>o.id));for(const o of s.orders)if(included.has(o.id))o.cashCloseId=close.id;
  return s;
 }
 if(a.type==='open'){
 const t=s.tables.find(t=>t.id===a.tableId)||fail('La mesa ya no existe.');
 if(s.orders.some(o=>o.tableId===t.id&&o.status==='open'))fail('Esta mesa ya tiene un pedido abierto.');
 s.orders.push({id:crypto.randomUUID(),number:s.nextNumber++,tableId:t.id,tableName:t.name,status:'open',created:now,serviceStatus:'received',items:[],discountType:'percent',discount:0,note:''});return s;}
 if(a.type==='productSave'){
 const name=str(a.name,80);if(s.products.some(p=>p.id!==a.id&&p.name.toLocaleLowerCase()===name.toLocaleLowerCase()))fail('Ya hay un producto con ese nombre.');
 const p:Product={id:a.id||crypto.randomUUID(),name,description:typeof a.description==='string'?a.description.trim().slice(0,200):'',price:int(a.price,100,100000000),category:categories.includes(a.category)?a.category:fail('Elegí una categoría.'),image:['burger','pizza','salad','fries','drink','soda','dessert'].includes(a.image)?a.image:'burger',available:a.available!==false};
 const i=s.products.findIndex(p=>p.id===a.id);if(a.id&&i<0)fail('El producto ya no existe.');if(i>=0)s.products[i]=p;else{if(s.products.length>=100)fail('La carta admite hasta 100 productos.');s.products.push(p)}return s;}
 if(a.type==='productDelete'){if(!s.products.some(p=>p.id===a.id))fail('El producto ya no existe.');s.products=s.products.filter(p=>p.id!==a.id);return s;}
 if(a.type==='tableSave'){
 const name=str(a.name,40);if(s.tables.some(t=>t.id!==a.id&&t.name.toLocaleLowerCase()===name.toLocaleLowerCase()))fail('Ya hay una mesa con ese nombre.');
 const t:Table={id:a.id||crypto.randomUUID(),name,zone:str(a.zone,40),seats:int(a.seats,1,20)};
 const i=s.tables.findIndex(t=>t.id===a.id);if(a.id&&i<0)fail('La mesa ya no existe.');if(i>=0)s.tables[i]=t;else{if(s.tables.length>=50)fail('Podés tener hasta 50 mesas.');s.tables.push(t)}return s;}
 if(a.type==='tableDelete'){if(s.orders.some(o=>o.tableId===a.id&&o.status==='open'))fail('Primero cobrá o anulá el pedido de esta mesa.');s.tables=s.tables.filter(t=>t.id!==a.id);return s;}
 const o=s.orders.find(o=>o.id===a.orderId)||fail('No encontramos el pedido.');if(o.status!=='open')fail('Este pedido ya está cerrado.');
 if(a.type==='add'){
 const p=s.products.find(p=>p.id===a.productId&&p.available)||fail('Este producto no está disponible.');const l=o.items.find(l=>l.id===p.id);if(l){if(l.quantity>=99)fail('El máximo es de 99 unidades por producto.');l.quantity++}else{o.items.push({id:p.id,name:p.name,price:p.price,quantity:1,image:p.image})}}
 else if(a.type==='quantity'){
 const q=int(a.quantity,0,99);const l=o.items.find(l=>l.id===a.productId)||fail('El producto no está en este pedido.');if(q===0)o.items=o.items.filter(l=>l!==l);else l.quantity=q;
 if(o.discountType==='fixed'&&o.discount>totals({...o,discount:0}).subtotal)o.discount=0;}
 else if(a.type==='discount'){
 o.discountType=['percent','fixed'].includes(a.discountType)?a.discountType:fail('El tipo de descuento no es válido.');o.discount=int(a.discount,0,o.discountType==='percent'?10000:totals({...o,discount:0}).subtotal);if(!o.items.length&&o.discount)fail('Agregá productos antes de aplicar un descuento.');}
 else if(a.type==='service'){if(!Object.hasOwn(serviceStates,a.serviceStatus))fail('Elegí un estado de preparación válido.');o.serviceStatus=a.serviceStatus;}
 else if(a.type==='lineNote'){const line=o.items.find(l=>l.id===a.productId)||fail('El producto no está en este pedido.');if(typeof a.note!=='string'||a.note.length>200)fail('La indicación admite hasta 200 caracteres.');line.note=a.note.trim();}
 else if(a.type==='note'){o.note=typeof a.note==='string'?a.note.trim().slice(0,400):''}
 else if(a.type==='cancel'){o.reason=str(a.reason,200);o.status='cancelled';o.closed=now;}
 else if(a.type==='pay'){if(!o.items.length)fail('El pedido está vacío.');if(totals(o).total<0)fail('Revisá el descuento.');const method=a.paymentMethod||'QR simbólico';if(!['QR simbólico','Efectivo','Transferencia'].includes(method))fail('Elegí un medio de pago válido.');o.shares=splitBill(totals(o).total,a.people??1);o.paymentMethod=method;o.status='paid';o.closed=now;}
 else fail('La acción no es válida.');
 return s;
}
