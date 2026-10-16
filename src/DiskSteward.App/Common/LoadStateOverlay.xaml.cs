using System.Windows.Controls;
using System.Windows.Input;

namespace DiskSteward.App.Common;

public sealed partial class LoadStateOverlay : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(LoadState), typeof(LoadStateOverlay), new PropertyMetadata(LoadState.Idle));

    public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(
        nameof(ErrorMessage), typeof(string), typeof(LoadStateOverlay), new PropertyMetadata(null));

    public static readonly DependencyProperty EmptyTextProperty = DependencyProperty.Register(
        nameof(EmptyText), typeof(string), typeof(LoadStateOverlay), new PropertyMetadata(null));

    public static readonly DependencyProperty RetryCommandProperty = DependencyProperty.Register(
        nameof(RetryCommand), typeof(ICommand), typeof(LoadStateOverlay), new PropertyMetadata(null));

    // False on pages that show their own scan card, so the first load has one indicator, not two.
    public static readonly DependencyProperty ShowSpinnerProperty = DependencyProperty.Register(
        nameof(ShowSpinner), typeof(bool), typeof(LoadStateOverlay), new PropertyMetadata(true));

    public LoadStateOverlay() => InitializeComponent();

    public bool ShowSpinner
    {
        get => (bool)GetValue(ShowSpinnerProperty);
        set => SetValue(ShowSpinnerProperty, value);
    }

    public LoadState State
    {
        get => (LoadState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string? ErrorMessage
    {
        get => (string?)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public string? EmptyText
    {
        get => (string?)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public ICommand? RetryCommand
    {
        get => (ICommand?)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }
}
