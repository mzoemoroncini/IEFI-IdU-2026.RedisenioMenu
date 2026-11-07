import {test} from 'node:test';
import assert from 'node:assert/strict';
import {initialState,mutate,cashDay,cashTotals,businessDate} from '../lib/restaurant.ts';
const day='2026-11-05',now='2026-11-05T22:00:00.000Z';
function sale(s=initialState(),{table='table-1',at=now,discount=0,type='percent',cancel=false}={}){
 s=mutate(s,{type:'open',tableId:table},at);const id=s.orders.at(-1).id;
 s=mutate(s,{type:'add',orderId:id,productId:'classic'},at);
 if(discount)s=mutate(s,{type:'discount',orderId:id,discountType:type,discount},at);
 return mutate(s,cancel?{type:'cancel',orderId:id,reason:'Cancelación de prueba'}:{type:'pay',orderId:id},at);
}
const close=(s,date=day,extra={})=>mutate(s,{type:'cashClose',date,...extra},now);
test('cierre calcula bruto, descuentos, neto y unidades sin sumar anulaciones',()=>{
 let s=sale(initialState(),{discount:1000});s=sale(s,{table:'table-2',discount:150000,type:'fixed'});s=sale(s,{table:'table-3',cancel:true});
 s=close(s);const c=s.cashCloses[0];assert.deepEqual(c.summary,{gross:1700000,discount:235000,net:1465000,paidCount:2,cancelledCount:1,cancelledAmount:850000,units:2});assert.equal(c.orders.length,3);assert.ok(s.orders.every(o=>o.cashCloseId===c.id));
});
test('no permite repetir un cierre sin movimientos nuevos',()=>assert.throws(()=>close(close(sale())),/no tiene nuevos/));
test('un segundo cierre solo incluye los nuevos pedidos de esa fecha',()=>{
 let s=close(sale());const first=structuredClone(s.cashCloses[0]);s=sale(s);s=close(s);assert.equal(s.cashCloses[1].number,2);assert.equal(s.cashCloses[1].orders.length,1);assert.equal(s.cashCloses[1].summary.net,850000);assert.notEqual(s.cashCloses[0].orders[0].id,s.cashCloses[1].orders[0].id);assert.deepEqual(s.cashCloses[0],first);
});
test('si hay mesas abiertas requiere confirmación explícita y las conserva abiertas',()=>{
 let s=sale();s=mutate(s,{type:'open',tableId:'table-2'},now);assert.throws(()=>close(s),/pedidos abiertos/);assert.throws(()=>close(s,day,{acknowledgeOpen:'true'}),/pedidos abiertos/);
 s=close(s,day,{acknowledgeOpen:true});assert.equal(s.cashCloses[0].orders.length,1);assert.equal(s.cashCloses[0].openOrders.length,1);assert.equal(s.orders[1].status,'open');assert.equal(s.orders[1].cashCloseId,undefined);
});
test('el cierre pendiente se actualiza al cobrar un pedido abierto que quedó fuera',()=>{
 let s=mutate(initialState(),{type:'open',tableId:'table-1'},now);s=close(s,day,{acknowledgeOpen:true});const id=s.orders[0].id;s=mutate(s,{type:'add',orderId:id,productId:'classic'},now);s=mutate(s,{type:'pay',orderId:id},now);assert.equal(cashDay(s,day).summary.net,850000);s=close(s);assert.equal(s.cashCloses[1].summary.paidCount,1);
});
test('asigna por fecha de cobro en Argentina, incluso si el pedido abrió el día anterior',()=>{
 let s=mutate(initialState(),{type:'open',tableId:'table-1'},'2026-11-05T02:30:00Z');const id=s.orders[0].id;s=mutate(s,{type:'add',orderId:id,productId:'classic'},now);s=mutate(s,{type:'pay',orderId:id},'2026-11-06T02:30:00Z');assert.equal(businessDate('2026-11-05T02:30:00Z'),'2026-11-04');assert.equal(cashDay(s,day).pending.length,1);assert.equal(cashDay(s,'2026-11-06').pending.length,0);
});
test('el cierre de una fecha no incluye pedidos de otra',()=>{
 let s=sale(initialState(),{at:'2026-11-04T18:00:00Z'});s=sale(s,{table:'table-2'});s=close(s);assert.equal(s.cashCloses[0].orders.length,1);assert.equal(s.orders[0].cashCloseId,undefined);assert.equal(cashDay(s,'2026-11-04').summary.net,850000);
});
test('permite guardar una fecha sin ventas una sola vez',()=>{let s=close(initialState());assert.equal(s.cashCloses[0].summary.net,0);assert.equal(s.cashCloses[0].orders.length,0);assert.throws(()=>close(s))});
test('un día con solo anulaciones guarda neto cero y no se repiten los anulados',()=>{const s=close(sale(initialState(),{cancel:true}));assert.equal(s.cashCloses[0].summary.net,0);assert.equal(s.cashCloses[0].summary.cancelledCount,1);assert.equal(cashDay(s,day).pending.length,0);assert.throws(()=>close(s))});
test('rechaza fechas inexistentes, incompletas y futuras',()=>{for(const date of ['2026-02-30','2026-11','ayer','2027-01-01','2026-11-06',null])assert.throws(()=>close(initialState(),date))});
test('la copia del cierre conserva nombres, precios, descuentos, motivo y notas',()=>{
 let s=sale(initialState(),{discount:1500});s=close(s);const snapshot=structuredClone(s.cashCloses[0]);s=mutate(s,{type:'productSave',...s.products[0],price:999999,name:'Nombre nuevo'},now);s=mutate(s,{type:'productDelete',id:'classic'},now);s=mutate(s,{type:'tableSave',...s.tables[0],name:'Mesa renombrada'},now);assert.deepEqual(s.cashCloses[0],snapshot);
});
test('admite datos previos sin cashCloses y no altera pedidos previos',()=>{
 let s=sale();delete s.cashCloses;const before=structuredClone(s);const updated=close(s);assert.equal(updated.cashCloses.length,1);assert.deepEqual(s,before);assert.equal(updated.orders[0].items[0].name,s.orders[0].items[0].name);
});
test('pedidos de 100% de descuento también se incluyen una sola vez',()=>{const s=close(sale(initialState(),{discount:10000}));assert.equal(s.cashCloses[0].summary.paidCount,1);assert.equal(s.cashCloses[0].summary.net,0);assert.equal(s.cashCloses[0].summary.gross,850000);assert.equal(cashDay(s,day).canClose,false)});
test('un pedido abierto posterior a la fecha elegida no se cierra ni genera advertencia retroactiva',()=>{const s=mutate(initialState(),{type:'open',tableId:'table-1'},now);assert.equal(cashDay(s,'2026-11-04').open.length,0);assert.equal(close(s,'2026-11-04').orders[0].status,'open')});
