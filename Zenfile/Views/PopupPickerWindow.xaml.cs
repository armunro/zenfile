using System.Windows;
using System.Windows.Input;
using Zenfile.Models;
using Zenfile.ViewModels;

namespace Zenfile.Views;

public partial class PopupPickerWindow : Window
{
    private readonly PopupPickerViewModel _viewModel;

    public PopupPickerWindow(PopupPickerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        if (_viewModel.TransferMode == TransferMode.Move)
        {
            RbMove.IsChecked = true;
        }
        else
        {
            RbCopy.IsChecked = true;
        }

        Loaded += (s, e) =>
        {
            SearchTextBox.Focus();
        };
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_viewModel.IsCreatingNewTask)
            {
                _viewModel.IsCreatingNewTask = false;
            }
            else
            {
                Close();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (_viewModel.IsCreatingNewTask)
            {
                _viewModel.ConfirmCreateNewTaskCommand.Execute(null);
            }
            else if (_viewModel.ExecuteTransferCommand.CanExecute(null))
            {
                _viewModel.ExecuteTransferCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    private void OnModeMoveClicked(object sender, RoutedEventArgs e)
    {
        _viewModel.TransferMode = TransferMode.Move;
    }

    private void OnModeCopyClicked(object sender, RoutedEventArgs e)
    {
        _viewModel.TransferMode = TransferMode.Copy;
    }
}
