import type {Product,State} from './restaurant';
// Precios de la carta de ejemplo, expresados en centavos.
export const extraProducts:Product[]=[
{id:'menu26-bacon',name:'Bacon BBQ',description:'Carne, cheddar, panceta crocante, cebolla y salsa barbacoa.',price:1050000,category:'Hamburguesas',image:'burger',available:true},
{id:'menu26-triple',name:'Triple de la casa',description:'Tres medallones de carne, triple cheddar y salsa especial.',price:1450000,category:'Hamburguesas',image:'burger',available:true},
{id:'menu26-criolla',name:'Criolla',description:'Carne, queso, lechuga, tomate y salsa criolla.',price:980000,category:'Hamburguesas',image:'burger',available:true},
{id:'menu26-muzza',name:'Pizza muzzarella',description:'Salsa de tomate, muzzarella, aceitunas y orégano. Ocho porciones.',price:1100000,category:'Pizzas',image:'pizza',available:true},
{id:'menu26-napo',name:'Pizza napolitana',description:'Muzzarella, rodajas de tomate, ajo y aceitunas. Ocho porciones.',price:1350000,category:'Pizzas',image:'pizza',available:true},
{id:'menu26-four',name:'Pizza cuatro quesos',description:'Muzzarella, provolone, queso azul y parmesano. Ocho porciones.',price:1500000,category:'Pizzas',image:'pizza',available:true},
{id:'menu26-fries-large',name:'Papas para compartir',description:'Porción grande de papas crocantes con dos dips de la casa.',price:700000,category:'Acompañamientos',image:'fries',available:true},
{id:'menu26-fries-rustic',name:'Papas con hierbas',description:'Papas doradas con perejil, ajo y un toque de pimentón.',price:500000,category:'Acompañamientos',image:'fries',available:true},
{id:'menu26-mint',name:'Limonada de menta',description:'Limón exprimido, menta fresca y hielo. Jarra de un litro.',price:650000,category:'Bebidas',image:'drink',available:true},
{id:'menu26-soda-large',name:'Gaseosa para compartir',description:'Jarra de un litro con hielo. Consultá los sabores disponibles.',price:480000,category:'Bebidas',image:'soda',available:true},
{id:'menu26-choco',name:'Chocolate intenso',description:'Porción de torta húmeda con ganache de chocolate semiamargo.',price:580000,category:'Postres',image:'dessert',available:true},
{id:'menu26-choco-share',name:'Dulce para dos',description:'Dos porciones de nuestra torta de chocolate para compartir.',price:900000,category:'Postres',image:'dessert',available:true}
];
export const dietaryProducts:Product[]=[
{id:'diet26-caprese',name:'Ensalada caprese',description:'Tomates frescos, mozzarella, albahaca y aceite de oliva. Contiene lácteos.',price:750000,category:'Vegetariano',image:'salad',available:true},
{id:'diet26-garden',name:'Ensalada de la huerta con queso',description:'Hojas verdes, zanahoria, tomate, huevo y queso. Contiene huevo y lácteos.',price:820000,category:'Vegetariano',image:'salad',available:true},
{id:'diet26-lentils',name:'Bowl de lentejas',description:'Lentejas, arroz integral, zanahoria, tomate y hojas verdes con limón y oliva.',price:880000,category:'Vegano',image:'salad',available:true},
{id:'diet26-chickpea',name:'Bowl de garbanzos',description:'Garbanzos, pepino, tomate, zanahoria y hojas verdes con vinagreta de limón.',price:850000,category:'Vegano',image:'salad',available:true},
{id:'diet26-rice',name:'Ensalada de arroz y vegetales',description:'Arroz, tomate, zanahoria y hojas verdes. Consultar preparación sin contaminación cruzada.',price:780000,category:'Sin TACC',image:'salad',available:true},
{id:'diet26-quinoa',name:'Bowl de quinoa',description:'Quinoa, tomate, pepino y hojas verdes con oliva. Confirmar insumos y preparación sin gluten.',price:950000,category:'Sin TACC',image:'salad',available:true}
];
export function expandMenu(input:State):State{
 if((input.menuRevision||0)>=2)return input;
 const state=structuredClone(input);
 const additions=[...((input.menuRevision||0)<1?extraProducts:[]),...dietaryProducts];
 for(const product of additions){
  if(state.products.length>=100)break;
  if(!state.products.some(p=>p.id===product.id||p.name.toLocaleLowerCase()===product.name.toLocaleLowerCase()))state.products.push({...product});
 }
 state.menuRevision=2;
 return state;
}
