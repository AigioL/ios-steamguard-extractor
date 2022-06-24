using System.Text;

// ReSharper disable once CheckNamespace
namespace Avalonia.Controls;

public static class TextBoxExtensions
{
    public static void Flush(this (TextBox textBox, StringBuilder builder) textBox)
    {
        if (textBox.builder.Length > 0)
        {
            textBox.textBox.Text = textBox.builder.ToString();
            textBox.builder.Clear();
        }
    }

    public static void ScrollToEnd(this TextBox textBox)
    {
        textBox.CaretIndex = int.MaxValue;
    }
}
