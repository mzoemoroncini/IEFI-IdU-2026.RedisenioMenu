# Mesa · Gestión de restaurante

Aplicación nativa de escritorio para Windows. Rediseño de la interfaz de cierre de ventas para la IEFI de Interfaz de Usuario, basado en la propuesta visual de Stitch.

## Descargar y abrir la aplicación

1. Entrar a **Actions** y abrir una ejecución correcta de **Compilar Mesa para Windows**.
2. En **Artifacts**, descargar **Mesa-Windows-x64** (requiere iniciar sesión en GitHub).
3. Extraer todo el ZIP y abrir `Mesa.exe` con doble clic.

También podés ejecutar el proyecto desde Visual Studio o una terminal, siguiendo los pasos de abajo.

La versión incluida es para Windows 10/11 de 64 bits (x64). Incluye el runtime: no necesita instalar .NET ni conectarse a Internet. No requiere permisos de administrador.

## Usarla

1. Elegir la fecha del cierre; se inicia con la fecha de la computadora.
2. Ingresar cantidades enteras entre 0 y 999.999. También se pueden usar + y −, o las flechas arriba/abajo cuando se edita una cantidad.
3. Los importes, el total de ventas, el impuesto del 18 % y las unidades se actualizan automáticamente mientras se cargan las cantidades.
4. Revisar los resultados; no hay que presionar un botón de cálculo.
5. **Imprimir resumen** abre una vista previa. Su botón **Imprimir** abre el diálogo de Windows; allí se puede elegir una impresora o **Microsoft Print to PDF**.
6. **Guardar cierre** conserva el cálculo en la base de datos local y, solo después de confirmar el guardado, limpia todas las cantidades y los totales. La fecha queda seleccionada, marcada como registrada. Para una nueva carga, elegir otra fecha.
7. Si la fecha ya está guardada, se muestra **Este día ya está registrado** y se bloquea el guardado: **Ver en historial** lleva a ese registro; **Editar registro** abre sus cantidades originales. Al guardar cambios se pide confirmación y se mantiene un solo registro por día. Los productos, precios y tasa históricos se conservan, aunque hayan cambiado o se hayan quitado del menú.
8. En **Historial**, seleccionar un día para **Ver detalle** o **Imprimir**. También se puede abrir el detalle con doble clic o Enter.
9. **Limpiar cantidades** solicita confirmación y vuelve a cero. Conserva la fecha elegida y los cierres del historial.

Tab y Shift+Tab recorren los campos y acciones. Enter desde una cantidad avanza al siguiente campo. Ctrl+P abre la vista previa si hay ventas y los datos son válidos. En la confirmación, Conservar recibe el foco; Esc cancela y Enter activa el botón enfocado.

El campo de fecha y el calendario usan el estilo de Mesa: bordes redondeados, selección terracota, foco visible, lunes como primer día de la semana y navegación por meses/años. También se puede escribir la fecha con el teclado.

Los resultados se actualizan automáticamente al cambiar cantidades, productos o precios. Las cantidades inválidas muestran su mensaje junto al campo, ocultan el total e impiden guardar e imprimir hasta corregirlas. **Guardar cierre** registra el resultado cuando el usuario lo decide; escribir cantidades o cambiar el menú no modifica por sí solo el historial.

## Administrar el menú

La pestaña **Menú** permite agregar productos, editar nombres y precios y quitar productos con confirmación.

- **Agregar producto** abre el formulario de nombre y precio.
- **Editar producto** modifica el seleccionado. También se puede abrir con doble clic o Enter.
- **Quitar del menú** pide confirmación y elimina el producto del menú actual. Sus registros en el historial permanecen intactos.
- Los precios se escriben sin separadores de miles, con coma decimal si corresponde: `6500` o `6500,50`. Se permiten dos decimales y también productos sin cargo.
- Los nombres repetidos se rechazan, incluso con diferencias de mayúsculas, tildes o espacios.
- Los cambios se guardan en SQLite y permanecen al reiniciar la aplicación.
- Si hay un cierre en carga, se conservan las cantidades de los productos que sigan presentes, usando su identidad. Los productos nuevos empiezan en cero. Los totales se recalculan automáticamente con el menú actualizado. Antes de quitar un producto con una cantidad cargada se informa que esa cantidad también se quitará de la pantalla.
- El menú admite hasta 100 productos. Si queda vacío, se puede seguir consultando el historial; hay que agregar al menos un producto para cargar otro cierre.

El menú comienza con ocho productos de ejemplo solo cuando se crea o se actualiza por primera vez la base. Los productos borrados no reaparecen al reiniciar.

## Base de datos e historial

Mesa utiliza **SQLite**, mediante Microsoft.Data.Sqlite. La base se crea automáticamente en:

```text
%LOCALAPPDATA%\Mesa\cierres.db
```

No necesita un servidor, contraseñas ni Internet. El historial pertenece al usuario de Windows y permanece aunque se actualice o se mueva el ejecutable.

- Cada fecha tiene un único cierre; reemplazarlo requiere confirmación.
- La actualización a esta versión agrega la tabla del menú sin borrar ni alterar los cierres guardados en la versión anterior.
- Se conservan productos, precios, cantidades y tasa del momento del guardado. Un cambio futuro del menú no modifica esos registros.
- El historial se ordena del día más reciente al más antiguo. Los detalles son de solo lectura y se pueden imprimir nuevamente.
- Un día sin ventas también se puede guardar con sus cantidades en cero.
- El guardado de la cabecera y los productos es una única transacción. Si falla, no queda un cierre parcial; si era un reemplazo, se conserva el anterior.
- No se eliminan registros al usar Limpiar cantidades. Al salir, la aplicación avisa si hay cantidades que todavía no se guardaron.

Para hacer una copia de seguridad, cerrar Mesa y copiar el archivo `cierres.db` a otro lugar. No borrar este archivo al actualizar la aplicación.

Los precios son ilustrativos y el 18 % es una regla académica, no una afirmación sobre impuestos vigentes.

## Abrir el código

Requiere el SDK de .NET 10 y, en Visual Studio, las herramientas de desarrollo de escritorio con .NET. Abrir `Mesa.sln` y seleccionar `Mesa.Desktop` como proyecto de inicio. También se puede abrir la carpeta del repositorio en VS Code y ejecutar desde una terminal:

```powershell
dotnet run --project src/Mesa.Desktop
```

El código fuente se organiza así:

- `src/Mesa.Desktop`: ventanas WPF, comportamiento y vista previa de impresión.
- `src/Mesa.Core`: catálogo editable, validación, cálculos con decimal, comparación con la copia guardada, actualización del esquema SQLite e historial mediante consultas parametrizadas. Los detalles históricos no tienen dependencia del menú actual.
- `tests/Mesa.Checks`: verificaciones ejecutables de cálculos, estados e historial con una base SQLite temporal real.

Para verificar la lógica:

```powershell
dotnet run --project tests/Mesa.Checks
```

Para publicar una nueva versión desde Windows:

```powershell
dotnet publish src/Mesa.Desktop -c Release -r win-x64 --self-contained true -o Publicado
```

## Ejemplo para demostrar en clase

Con el menú inicial de ejemplo, ingresar, en orden: **14, 22, 18, 6, 8, 11, 4, 12**.

Resultado esperado: **$ 344.800,00** en ventas, **$ 62.064,00** de impuesto y **95** unidades.

Después, cambiar hamburguesas a 16: los resultados se actualizan inmediatamente, sin botón de cálculo. El nuevo resultado es **$ 357.800,00**, **$ 64.404,00** de impuesto y **97** unidades. Probar −3 y 4,5 para mostrar las validaciones. Guardar el cierre, abrir Historial y consultar el detalle. Cerrar y volver a abrir Mesa para comprobar la persistencia. Comprobar que se limpiaron las cantidades y los totales, y que la misma fecha no permite volver a guardar por accidente. Usar Editar registro para modificar sus cantidades y confirmar Guardar cambios. Elegir otro día para una nueva carga. Finalmente, probar Conservar y Sí, limpiar: el historial debe permanecer intacto.

Para demostrar la conservación histórica, guardar ese cierre y luego ir a Menú. Cambiar el nombre o precio de Hamburguesa, o quitarla. Volver a Historial y abrir el día guardado: seguirá mostrando Hamburguesa a $6.500,00 con sus cantidades e importes originales. No hace falta volver a guardar ese cierre para consultar sus datos.

## Decisiones de rediseño

La tabla reúne el nombre, el precio, la cantidad y el importe de cada producto. El panel lateral separa los resultados de las entradas. El botón terracota identifica Guardar cierre, la acción principal. El cálculo automático elimina una acción manual y da respuesta inmediata a cada cambio de cantidad. Se agregaron navegación por teclado, mensajes por campo, confirmación de limpieza, guardado, historial por día y vista previa de impresión. Las pestañas separan la carga del cierre, la consulta del historial y la administración del menú. Los estados mantienen la misma distribución. La ventana utiliza controles de escritorio y la barra nativa de Windows.

## Validación de esta entrega

- Solución compilada en Release sin advertencias ni errores.
- Publicación autocontenida para Windows x64; ejecutable PE de interfaz gráfica.
- 95 verificaciones de lógica correctas: cálculos, validaciones, historial SQLite, reemplazos, transacciones, actualización desde la base anterior, alta/edición/baja de productos, precios y nombres, menú vacío, y conservación de los cierres después de modificar o borrar productos.
- La integración continua ejecuta 12 verificaciones de interfaz WPF en Windows con una base temporal: guardar y limpiar, conservación del historial, bloqueo de día repetido, cambio de fecha, apertura del calendario, navegación de meses y años, y apertura de edición histórica.
- El entorno de preparación Linux compila el código; las verificaciones WPF se ejecutan en el runner Windows. El aspecto visual final, el recorrido manual de teclado y la impresión se pueden comprobar con el ejemplo anterior.
