import {test} from 'node:test';
import assert from 'node:assert/strict';
import {initialState,mutate} from '../lib/restaurant.ts';
import {expandMenu,extraProducts} from '../lib/menu-expansion.ts';
test('amplía la carta a veintiséis productos sin alterar pedidos ni precios existentes',()=>{
 let old=mutate(initialState(),{type:'open',tableId:'table-1'});old=mutate(old,{type:'add',orderId:old.orders[0].id,productId:'classic'});old.products[0].price=999900;
 const before=structuredClone(old),next=expandMenu(old);assert.equal(next.products.length,26);assert.equal(next.products[0].price,999900);assert.deepEqual(next.orders,before.orders);assert.deepEqual(next.tables,before.tables);assert.deepEqual(old,before);
});
test('la ampliación no duplica productos ni restaura los borrados después',()=>{const next=expandMenu(initialState());next.products=next.products.filter(p=>p.id!==extraProducts[0].id);assert.equal(expandMenu(next).products.length,25);assert.equal(expandMenu(next),next)});
test('respeta productos con el mismo nombre y admite agregar las nuevas opciones al pedido',()=>{const old=initialState();old.products.push({...extraProducts[0],id:'custom',price:123400});let next=expandMenu(old);assert.equal(next.products.filter(p=>p.name===extraProducts[0].name).length,1);assert.equal(next.products.find(p=>p.id==='custom').price,123400);next=mutate(next,{type:'open',tableId:'table-1'});next=mutate(next,{type:'add',orderId:next.orders[0].id,productId:extraProducts[1].id});assert.equal(next.orders[0].items[0].name,extraProducts[1].name)});

test('amplía una carta existente sin restaurar productos eliminados de la primera ampliación',()=>{const old=initialState();old.menuRevision=1;const next=expandMenu(old);assert.equal(next.products.length,14);assert.ok(!next.products.some(p=>p.id===extraProducts[0].id));assert.equal(next.products.filter(p=>['Vegetariano','Vegano','Sin TACC'].includes(p.category)).length,6);assert.equal(expandMenu(next),next)});
