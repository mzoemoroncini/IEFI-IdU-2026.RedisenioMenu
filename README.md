# Mesa · Gestión de restaurante

Proyecto de la IEFI de Interfaz de Usuario · 2026.

**Abrir la aplicación:** https://mesa-restaurante-zoe.glassy-mango-3069.chatgpt.site/

Esta es la versión web actual. Reemplaza el contenido de la antigua aplicación de escritorio de este repositorio.

## Funciones

- Salón con mesas libres y ocupadas, zonas, capacidad y pedidos independientes.
- Preparación del pedido: recibido, en preparación, listo y entregado.
- Carta administrable con 26 productos iniciales y categorías vegetariana, vegana y sin TACC. Los platos sin TACC requieren confirmar insumos y preparación sin contaminación cruzada.
- Totales automáticos, cantidades, indicaciones por producto y notas generales.
- Descuentos por porcentaje o importe fijo y anulación con motivo.
- Registro manual de cobro por QR simbólico, efectivo o transferencia. El QR no procesa dinero real.
- Cuenta dividida en partes iguales con reparto exacto de centavos. El cobro se confirma por el total del pedido.
- Tickets imprimibles con productos, observaciones, descuentos, medio de pago y división.
- Cierres de caja por fecha, historial e importes que no se cuentan dos veces.
- Temas claro y oscuro, búsqueda y adaptación a pantallas pequeñas.

## Tecnología y persistencia

React, TypeScript, Vinext/Vite, Cloudflare Workers y Cloudflare D1 (SQLite). El servidor guarda los datos; este repositorio contiene código y migraciones, sin copias de las ventas publicadas. Los pedidos conservan los nombres y precios del momento de su carga. Las escrituras verifican la versión para evitar sobrescrituras entre ventanas.

## Desarrollo

Se requiere Node.js 24 y pnpm según `packageManager` en `package.json`.

```sh
corepack pnpm install
corepack pnpm dev
```

Antes de usar la base local, configurar el binding D1 `DB` y aplicar las migraciones de `drizzle/` con Wrangler en modo local. La configuración de Vite declara el binding de desarrollo; el alojamiento usa `.openai/hosting.json`. Las herramientas de `scripts/` incluyen el flujo original de ejecución y publicación. No publicar bases locales, `.dev.vars`, variables de entorno ni credenciales.

Para compilar: `corepack pnpm build`. Publicar en un entorno compatible con Workers/D1; GitHub almacena el código, no ejecuta esta aplicación como una página estática.

## Verificación

```sh
node --experimental-strip-types --test tests/*.mjs
node node_modules/typescript/bin/tsc --noEmit
```

39 pruebas automatizadas correctas en la última revisión. Se probaron también pedidos, descuento, cuenta dividida, cobro, anulación, persistencia e historial de cierres desde la interfaz en una base local separada.

## Recursos

Fotografías orientativas de Unsplash. Sus referencias están en el historial del proyecto de origen. Iconos Lucide. QR estático con contenido académico, sin cobro real.

## Recursos visuales

Fotografías de referencia de Unsplash, descargadas para no depender de peticiones externas al utilizar el sitio. Identificadores: photo-1568901346375-23c9450c58cd (hamburguesa), photo-1579751626657-72bc17010498 (pizza), photo-1512621776951-a57141f2eefd (ensalada), photo-1630384060421-cb20d0e0649d (papas), photo-1556679343-c7306c1976bc (bebida), photo-1622483767028-3f66f32aef97 (gaseosa), photo-1578985545062-69928b1d9587 (torta). Fotografías orientativas, seleccionables en la edición del producto.

QR estático generado con QR Code API / goQR.me. Contiene exclusivamente «MESA - QR SIMBOLICO - SIN COBRO REAL».
Iconos: Lucide. Identidad visual y composición propias del proyecto.
