using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using DeskPet.Models;
using DeskPet.Services;

namespace DeskPet.Shell;

public partial class SettingsWindow : Window
{
    private bool _loading;
    private readonly Dictionary<PetAction, Slider> _speedSliders = new();

    public SettingsWindow()
    {
        InitializeComponent();
        // Subscribe after XAML load so ValueChanged doesn't fire during parsing
        // (which would touch not-yet-created named elements → NullReferenceException).
        FloatScale.ValueChanged += FloatScale_ValueChanged;
        FloatOpacity.ValueChanged += FloatOpacity_ValueChanged;
        AiWalkChance.ValueChanged += AiWalk_Changed;
        AiSleepChance.ValueChanged += AiSleep_Changed;
        AiActionChance.ValueChanged += AiAction_Changed;
        AiIdleChance.ValueChanged += AiIdle_Changed;
        Load();
    }

    private void Load()
    {
        _loading = true; // suppress ValueChanged handlers during programmatic init
        var s = AppSettings.Instance;
        PetEnabled.IsChecked = s.PetEnabled;
        PetScale.Value = s.PetScale;
        PetScale.ValueChanged += PetScale_ValueChanged;
        PetScaleValue.Text = $"{s.PetScale:0.00}×";
        RoamInterval.Value = s.PetRoamInterval;
        StatDecay.Value = s.PetStatDecaySpeed;
        MusicDance.IsChecked = s.PetMusicDance;
        SoundVolume.Value = s.PetSoundVolume;
        SkinDirectory.Text = s.PetSkinDirectory;

        PetModelCombo.Items.Clear();
        foreach (var m in PetSkin.GetAvailableModels()) PetModelCombo.Items.Add(m);
        PetModelCombo.SelectedItem = s.PetModel;

        EnableHaptics.IsChecked = s.EnableHaptics;
        AutoStartOnBoot.IsChecked = AutoStart.IsEnabled;
        PetClickThrough.IsChecked = s.PetClickThrough;

        FloatScale.Value = s.FloatScale;
        FloatScaleValue.Text = $"{s.FloatScale:0.00}×";
        FloatOpacity.Value = s.FloatOpacity;
        FloatOpacityValue.Text = $"{s.FloatOpacity:P0}";

        AiWalkChance.Value = s.AiWalkChance;
        AiWalkValue.Text = $"{s.AiWalkChance:P0}";
        AiSleepChance.Value = s.AiSleepChance;
        AiSleepValue.Text = $"{s.AiSleepChance:P0}";
        AiActionChance.Value = s.AiActionChance;
        AiActionValue.Text = $"{s.AiActionChance:P0}";
        AiIdleChance.Value = s.AiIdleChance;
        AiIdleValue.Text = $"{s.AiIdleChance:P0}";

        BuildActionSpeedPanel();

        SelectProvider(s.AiProvider);
        AiApiKey.Text = s.AiApiKey;
        AiModel.Text = s.AiModel;
        AiBaseUrl.Text = s.AiBaseUrl;
        _loading = false;
    }

    private void FloatScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        if (FloatScaleValue != null) FloatScaleValue.Text = $"{e.NewValue:0.00}×";
        if (!IsLoaded) return;
        AppSettings.Instance.FloatScale = e.NewValue;
        FloatWindow.Instance.ApplyAppearance();
    }

    private void FloatOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        if (FloatOpacityValue != null) FloatOpacityValue.Text = $"{e.NewValue:P0}";
        if (!IsLoaded) return;
        AppSettings.Instance.FloatOpacity = e.NewValue;
        FloatWindow.Instance.ApplyAppearance();
    }

    private void PetScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        if (PetScaleValue != null) PetScaleValue.Text = $"{e.NewValue:0.00}×";
        if (!IsLoaded) return;
        // Live preview: resize the pet immediately as the slider moves.
        AppSettings.Instance.PetScale = e.NewValue;
        PetManager.Instance.ApplyScale();
    }

    private void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        SkinImporter.ImportFromFolder();
        ReloadModelList();
    }

    private void ImportZip_Click(object sender, RoutedEventArgs e)
    {
        SkinImporter.ImportFromZip();
        ReloadModelList();
    }

    private void ReloadModelList()
    {
        var current = PetModelCombo.SelectedItem as string ?? AppSettings.Instance.PetModel;
        PetModelCombo.Items.Clear();
        foreach (var m in PetSkin.GetAvailableModels()) PetModelCombo.Items.Add(m);
        if (current != null && PetModelCombo.Items.Contains(current)) PetModelCombo.SelectedItem = current;
        else if (PetModelCombo.Items.Count > 0) PetModelCombo.SelectedItem = PetModelCombo.Items[0];
    }

    private void SelectProvider(string provider)
    {
        foreach (ComboBoxItem item in AiProviderCombo.Items)
        {
            if (item.Tag as string == provider)
            {
                AiProviderCombo.SelectedItem = item;
                break;
            }
        }
    }

    private void AiProvider_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (AiProviderCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            switch (tag)
            {
                case "deepseek":
                    AiModel.Text = "deepseek-chat";
                    AiBaseUrl.Text = "https://api.deepseek.com";
                    break;
                case "openai":
                    AiModel.Text = "gpt-4o-mini";
                    AiBaseUrl.Text = "https://api.openai.com/v1";
                    break;
                case "custom":
                    break;
            }
        }
    }

    // ---- Behavior AI probability sliders ----

    private void AiWalk_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        if (AiWalkValue != null) AiWalkValue.Text = $"{e.NewValue:P0}";
        if (IsLoaded) AppSettings.Instance.AiWalkChance = e.NewValue;
    }

    private void AiSleep_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        if (AiSleepValue != null) AiSleepValue.Text = $"{e.NewValue:P0}";
        if (IsLoaded) AppSettings.Instance.AiSleepChance = e.NewValue;
    }

    private void AiAction_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        if (AiActionValue != null) AiActionValue.Text = $"{e.NewValue:P0}";
        if (IsLoaded) AppSettings.Instance.AiActionChance = e.NewValue;
    }

    private void AiIdle_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        if (AiIdleValue != null) AiIdleValue.Text = $"{e.NewValue:P0}";
        if (IsLoaded) AppSettings.Instance.AiIdleChance = e.NewValue;
    }

    // ---- Per-action playback speed ----

    private static readonly string[] ActionLabels =
    {
        "待机", "走路", "睡觉", "吃饭", "开心", "委屈", "拖拽", "跳舞", "爬墙", "倒挂", "掉落", "晕眩", "哈欠", "回巢",
    };

    private void BuildActionSpeedPanel()
    {
        ActionSpeedPanel.Children.Clear();
        _speedSliders.Clear();
        var actions = (PetAction[])System.Enum.GetValues(typeof(PetAction));
        for (int i = 0; i < actions.Length; i++)
        {
            var act = actions[i];
            string label = i < ActionLabels.Length ? ActionLabels[i] : act.ToString();
            double cur = AppSettings.Instance.ActionSpeed.TryGetValue(act.ToString(), out var v) ? v : 1.0;

            var title = new TextBlock { Text = label, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC0, 0xC0, 0xC0)), FontSize = 12, Margin = new Thickness(0, 10, 0, 2) };
            var slider = new Slider { Minimum = 0.2, Maximum = 3.0, Value = cur };
            var val = new TextBlock { Text = $"{cur:0.00}×", Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x90, 0x90, 0x90)), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right };
            var actCopy = act;
            slider.ValueChanged += (_, e2) => val.Text = $"{e2.NewValue:0.00}×";
            _speedSliders[actCopy] = slider;

            ActionSpeedPanel.Children.Add(title);
            ActionSpeedPanel.Children.Add(slider);
            ActionSpeedPanel.Children.Add(val);
        }
    }

    private void Save()
    {
        var s = AppSettings.Instance;
        bool scaleChanged = s.PetScale != PetScale.Value;
        bool speedChanged = false;
        foreach (var (act, slider) in _speedSliders)
        {
            double old = s.ActionSpeed.TryGetValue(act.ToString(), out var v) ? v : 1.0;
            if (Math.Abs(old - slider.Value) > 0.001) { speedChanged = true; break; }
        }
        s.PetEnabled = PetEnabled.IsChecked == true;
        s.PetScale = PetScale.Value;
        s.PetRoamInterval = RoamInterval.Value;
        s.PetStatDecaySpeed = StatDecay.Value;
        s.PetMusicDance = MusicDance.IsChecked == true;
        s.PetSoundVolume = SoundVolume.Value;
        s.PetSkinDirectory = SkinDirectory.Text.Trim();
        var newModel = PetModelCombo.SelectedItem as string ?? "deepseek-girl";
        bool modelChanged = newModel != s.PetModel;
        s.PetModel = newModel;

        s.EnableHaptics = EnableHaptics.IsChecked == true;
        s.PetClickThrough = PetClickThrough.IsChecked == true;
        bool autostart = AutoStartOnBoot.IsChecked == true;
        if (autostart != AutoStart.IsEnabled) AutoStart.SetEnabled(autostart);

        s.FloatScale = FloatScale.Value;
        s.FloatOpacity = FloatOpacity.Value;

        s.AiWalkChance = AiWalkChance.Value;
        s.AiSleepChance = AiSleepChance.Value;
        s.AiActionChance = AiActionChance.Value;
        s.AiIdleChance = AiIdleChance.Value;

        s.ActionSpeed.Clear();
        foreach (var (act, slider) in _speedSliders)
        {
            s.ActionSpeed[act.ToString()] = slider.Value;
        }

        s.AiProvider = (AiProviderCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "deepseek";
        s.AiApiKey = AiApiKey.Text.Trim();
        s.AiModel = AiModel.Text.Trim();
        s.AiBaseUrl = AiBaseUrl.Text.Trim();

        s.Save();

        if (!s.PetEnabled && PetWindow.Instance.IsVisible) PetWindow.Instance.Hide();
        if (s.PetEnabled && !PetWindow.Instance.IsVisible && PetManager.Instance.LifeState == PetLifeState.Roaming)
            PetWindow.Instance.ShowPet();

        if (modelChanged || speedChanged) PetManager.Instance.ReloadSkin();
        else if (scaleChanged) PetManager.Instance.ApplyScale();

        FloatWindow.Instance.ApplyAppearance();
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
        Save();
    }
}
