using System.Windows;

namespace Mesa.Desktop;

public partial class HelpWindow : Window
{
    public HelpWindow(int section)
    {
        InitializeComponent();
        Heading.Text = section switch { 1 => "Consultar y corregir cierres", 2 => "Administrar el menú", _ => "Guardar el cierre del día" };
        Steps.ItemsSource = section switch
        {
            1 => new[] { "1. El historial muestra un único cierre por fecha, del más reciente al más antiguo.", "2. Desde y Hasta filtran un período, incluyendo ambos días. Podés dejar uno vacío. Limpiar filtros vuelve a mostrar todos los cierres.", "3. Seleccioná un cierre y elegí Ver detalle, Imprimir o Editar registro. Enter sobre la lista abre su detalle.", "4. Al editar se destaca la fecha. Cambiá las cantidades y confirmá Guardar cambios. Se conservan los productos, precios e impuesto originales." },
            2 => new[] { "1. Agregar producto permite cargar un nombre y un precio, por ejemplo 6500 o 6500,50. No uses separadores de miles.", "2. Para corregir un producto, seleccionalo y elegí Editar producto. También podés usar Enter o doble clic.", "3. Quitar del menú pide confirmación. Las ventas guardadas conservan el producto y el precio que tenían al registrarse.", "4. El cierre que todavía estás cargando usa el menú actual. Si quitás un producto con cantidades cargadas se informa antes de confirmar." },
            _ => new[] { "1. Elegí la fecha del cierre e ingresá las cantidades vendidas. Las flechas ↑ y ↓ ajustan la cantidad; Tab o Enter avanzan al siguiente campo.", "2. Los importes, las ventas y el impuesto se calculan automáticamente. El 18 % corresponde a este ejercicio académico.", "3. Guardar cierre conserva el día en esta computadora y limpia la carga. No se puede guardar un segundo cierre de la misma fecha: usá Editar registro para corregirlo.", "4. Limpiar cantidades conserva el historial. Deshacer limpieza recupera las cantidades mientras no cambies cantidades, fecha o menú, ni guardes el cierre. La limpieza posterior a Guardar cierre no tiene deshacer; ese día se corrige desde su registro.", "5. Imprimir resumen abre una vista previa. Ctrl+P permite abrirla cuando las cantidades son válidas y hay ventas." }
        };
    }
    private void Window_Loaded(object sender, RoutedEventArgs e) { WindowBounds.EnsureVisible(this); CloseHelpButton.Focus(); }
}
