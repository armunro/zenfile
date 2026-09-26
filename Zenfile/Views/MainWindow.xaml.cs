using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Zenfile.Models;
using Zenfile.ViewModels;

namespace Zenfile.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ThemeHelper.ApplyDarkTheme(this);
        _viewModel = viewModel;
        DataContext = _viewModel;

        TabRoots.IsChecked = true;
        ShowTab(0);
    }

    private void ShowTab(int tabIndex)
    {
        _viewModel.SelectedTabIndex = tabIndex;

        // Visibility toggling for tabs
        var rootsGrid = ((Grid)Content).Children.OfType<Grid>().FirstOrDefault(g => Grid.GetRow(g) == 1);
        if (rootsGrid != null)
        {
            var tab0Grid = rootsGrid.Children.OfType<Grid>().FirstOrDefault(g => g != TabDropPanel && g != TabShellPanel && g != TabHistoryPanel);
            if (tab0Grid != null)
            {
                tab0Grid.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        TabDropPanel.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        TabShellPanel.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        TabHistoryPanel.Visibility = tabIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnTabRootsClicked(object sender, RoutedEventArgs e) => ShowTab(0);
    private void OnTabDropClicked(object sender, RoutedEventArgs e) => ShowTab(1);
    private void OnTabShellClicked(object sender, RoutedEventArgs e) => ShowTab(2);
    private void OnTabHistoryClicked(object sender, RoutedEventArgs e) => ShowTab(3);

    private void OnNewTaskKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_viewModel.CreateTaskCommand.CanExecute(null))
            {
                _viewModel.CreateTaskCommand.Execute(null);
            }
            e.Handled = true;
        }
    }

    private void OnTaskListDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private async void OnTaskListDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && _viewModel.SelectedTask != null)
            {
                await _viewModel.DropFilesAsync(files, _viewModel.SelectedTask);
            }
            else if (_viewModel.SelectedTask == null)
            {
                _viewModel.StatusMessage = "Please select a task folder first or drop directly onto a folder card.";
            }
            e.Handled = true;
        }
    }

    private void OnTaskCardDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private async void OnTaskCardDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                if (sender is FrameworkElement elem && elem.DataContext is WorkTaskFolder targetTask)
                {
                    await _viewModel.DropFilesAsync(files, targetTask);
                }
            }
            e.Handled = true;
        }
    }

    private void OnBigDropAreaDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private async void OnBigDropAreaDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                if (_viewModel.SelectedTask != null)
                {
                    await _viewModel.DropFilesAsync(files, _viewModel.SelectedTask);
                }
                else
                {
                    _viewModel.StatusMessage = "Please select a target folder from the dropdown on the right.";
                }
            }
            e.Handled = true;
        }
    }

    private void OnOpenCardInExplorerClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WorkTaskFolder task)
        {
            _viewModel.OpenTaskInExplorerCommand.Execute(task);
        }
    }

    private void OnDeleteCardClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WorkTaskFolder task)
        {
            _viewModel.SelectedTask = task;
            _viewModel.DeleteTaskCommand.Execute(task);
        }
    }

    private void OnOpenHistoryFolderClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TransferItem item)
        {
            _viewModel.OpenHistoryItemCommand.Execute(item);
        }
    }
}
