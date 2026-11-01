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
6. **Limpiar cantidades** solicita confirmación y vuelve a cero. Conserva la fecha elegida.

Tab y Shift+Tab recorren los campos y acciones. Ctrl+P abre la vista previa si el cálculo está actualizado. En la confirmación, Conservar recibe el foco; Esc cancela y Enter activa el botón enfocado.

Los resultados se actualizan al calcular. Una edición posterior marca el resumen como **Último cálculo** y bloquea la impresión. Los importes modificados aparecen como **Pendiente**. Las cantidades inválidas muestran su mensaje junto al campo y no producen un cálculo parcial.

Los datos son de la sesión actual. No hay base de datos ni historial de cierres. Al cerrar con cantidades cargadas se solicita confirmación; imprimir a PDF permite conservar el resumen. Los precios son ilustrativos y el 18 % es una regla académica, no una afirmación sobre impuestos vigentes.

## Abrir el código

Requiere el SDK de .NET 10 y, en Visual Studio, las herramientas de desarrollo de escritorio con .NET. Abrir `Mesa.sln` y seleccionar `Mesa.Desktop` como proyecto de inicio. También se puede abrir la carpeta del repositorio en VS Code y ejecutar desde una terminal:

```powershell
dotnet run --project src/Mesa.Desktop
```

El código fuente se organiza así:

- `src/Mesa.Desktop`: ventanas WPF, comportamiento y vista previa de impresión.
- `src/Mesa.Core`: productos, validación, cálculos con decimal y comparación con el último cálculo.
- `tests/Mesa.Checks`: verificaciones ejecutables de los cálculos y reglas que habilitan impresión, sin paquetes adicionales.

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

Después, cambiar hamburguesas a 16: la impresión debe quedar deshabilitada hasta recalcular. El nuevo resultado es **$ 357.800,00**, **$ 64.404,00** de impuesto y **97** unidades. Probar −3 y 4,5 para mostrar las validaciones. Finalmente, probar Conservar y Sí, limpiar.

## Decisiones de rediseño

La tabla reúne el nombre, el precio, la cantidad y el importe de cada producto. El panel lateral separa los resultados de las entradas. El botón terracota identifica la acción principal. Se agregaron navegación por teclado, mensajes por campo, confirmación de borrado y vista previa de impresión. Los estados mantienen la misma distribución. La ventana utiliza controles de escritorio y la barra nativa de Windows.

## Validación de esta entrega

- Solución compilada en Release sin advertencias ni errores.
- Publicación autocontenida para Windows x64; ejecutable PE de interfaz gráfica.
- 34 verificaciones de lógica correctas: importes, impuesto, cantidades inválidas, límites y habilitación de impresión al editar cantidades o fecha.
- La interfaz se compiló, pero no se pudo ejecutar visualmente desde el entorno Linux de preparación. Queda por comprobar en Windows el aspecto de las ventanas, el recorrido de teclado y la impresión. Usar el ejemplo anterior para esa primera prueba.
