using Avalonia.Controls;
using System.Collections.Specialized;
using VaultSharpAI.Desktop.ViewModels;

namespace VaultSharpAI.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += MainWindow_DataContextChanged;
    }

    private void MainWindow_DataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Messages.CollectionChanged -= Messages_CollectionChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel != null)
        {
            _viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
        }
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            var listBox = this.FindControl<ListBox>("ChatListBox");
            if (listBox != null && listBox.Items != null && listBox.Items.Count > 0)
            {
                var lastItem = listBox.Items[listBox.Items.Count - 1];
                if (lastItem != null)
                {
                    listBox.ScrollIntoView(lastItem);
                }
            }
        }
    }
}