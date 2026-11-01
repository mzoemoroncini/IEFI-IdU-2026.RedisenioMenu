using System.Windows;

namespace Mesa.Desktop;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string question, string explanation, string accept)
    {
        InitializeComponent();
        Question.Text = question; Explanation.Text = explanation; AcceptButton.Content = accept;
    }
    private void Window_Loaded(object sender, RoutedEventArgs e) => KeepButton.Focus();
    private void Accept_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
