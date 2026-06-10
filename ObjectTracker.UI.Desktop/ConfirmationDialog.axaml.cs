using Avalonia.Controls;

namespace ObjectTracker.UI.Desktop;

internal partial class ConfirmationDialog : Window
{
    public record struct ConfirmationResult(bool IsConfirmed);

    public ConfirmationDialog(string title, string message)
    {
        InitializeComponent();
        Title = title;
        MessageTextBlock.Text = message;
        ConfirmButton.Click += (_, _) => Close(new ConfirmationResult(true));
        CancelButton.Click += (_, _) => Close(new ConfirmationResult(false));
    }
}
