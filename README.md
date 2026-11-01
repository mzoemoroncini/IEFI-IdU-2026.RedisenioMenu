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
3. Presionar **Calcular ventas** o Enter desde un campo.
4. Revisar importes, ventas totales, impuesto del 18 % y unidades.
5. **Imprimir resumen** abre una vista previa. Su botón **Imprimir** abre el diálogo de Windows; allí se puede elegir una impresora o **Microsoft Print to PDF**.
6. **Guardar cierre** conserva el cálculo en la base de datos local. Si ya hay un cierre de esa fecha, solicita confirmación antes de reemplazarlo.
7. En **Historial**, seleccionar un día para **Ver detalle** o **Imprimir**. También se puede abrir el detalle con doble clic o Enter.
8. **Limpiar cantidades** solicita confirmación y vuelve a cero. Conserva la fecha elegida y los cierres del historial.

Tab y Shift+Tab recorren los campos y acciones. Ctrl+P abre la vista previa si el cálculo está actualizado. En la confirmación, Conservar recibe el foco; Esc cancela y Enter activa el botón enfocado.

Los resultados se actualizan al calcular. Una edición posterior marca el resumen como **Último cálculo** y bloquea el guardado y la impresión hasta recalcular. Los importes modificados aparecen como **Pendiente**. Las cantidades inválidas muestran su mensaje junto al campo y no producen un cálculo parcial.

## Base de datos e historial

Mesa utiliza **SQLite**, mediante Microsoft.Data.Sqlite. La base se crea automáticamente en:

```text
%LOCALAPPDATA%\Mesa\cierres.db
```

No necesita un servidor, contraseñas ni Internet. El historial pertenece al usuario de Windows y permanece aunque se actualice o se mueva el ejecutable.

- Cada fecha tiene un único cierre; reemplazarlo requiere confirmación.
- Se conservan productos, precios, cantidades y tasa del momento del guardado. Un cambio futuro del menú no modifica esos registros.
- El historial se ordena del día más reciente al más antiguo. Los detalles son de solo lectura y se pueden imprimir nuevamente.
- Un día sin ventas también se puede guardar después de calcular sus cantidades en cero.
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
- `src/Mesa.Core`: productos, validación, cálculos con decimal, comparación con el último cálculo y acceso a SQLite mediante consultas parametrizadas.
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

Ingresar, en orden: **14, 22, 18, 6, 8, 11, 4, 12**.

Resultado esperado: **$ 344.800,00** en ventas, **$ 62.064,00** de impuesto y **95** unidades.

Después, cambiar hamburguesas a 16: la impresión debe quedar deshabilitada hasta recalcular. El nuevo resultado es **$ 357.800,00**, **$ 64.404,00** de impuesto y **97** unidades. Probar −3 y 4,5 para mostrar las validaciones. Guardar el cierre, abrir Historial y consultar el detalle. Cerrar y volver a abrir Mesa para comprobar la persistencia. Guardar otra vez la misma fecha para probar Conservar y Sí, reemplazar. Finalmente, probar Conservar y Sí, limpiar: el historial debe permanecer intacto.

## Decisiones de rediseño

La tabla reúne el nombre, el precio, la cantidad y el importe de cada producto. El panel lateral separa los resultados de las entradas. El botón terracota identifica la acción principal. Se agregaron navegación por teclado, mensajes por campo, confirmación de limpieza, guardado, historial por día y vista previa de impresión. Las pestañas separan la tarea de carga de la consulta de cierres anteriores. Los estados mantienen la misma distribución. La ventana utiliza controles de escritorio y la barra nativa de Windows.

## Validación de esta entrega

- Solución compilada en Release sin advertencias ni errores.
- Publicación autocontenida para Windows x64; ejecutable PE de interfaz gráfica.
- 53 verificaciones correctas: cálculos, validaciones, guardado y recuperación desde SQLite, fechas únicas, reemplazo, precios históricos, protección frente a bases dañadas y reversión completa ante fallos de transacción.
- La interfaz se compiló, pero no se pudo ejecutar visualmente desde el entorno Linux de preparación. Queda por comprobar en Windows el aspecto de las ventanas, el recorrido de teclado y la impresión. Usar el ejemplo anterior para esa primera prueba.
