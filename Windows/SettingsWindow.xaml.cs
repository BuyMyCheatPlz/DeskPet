using System.Windows;
using System.Windows.Controls;
using DeskPet.Models;
using DeskPet.Services;

namespace DeskPet.Shell;

public partial class SettingsWindow : Window
{
    private bool _loading;

    public SettingsWindow()
    {
        InitializeComponent();
        Load();
    }

    private void Load()
    {
        var s = AppSettings.Instance;
        PetEnabled.IsChecked = s.PetEnabled;
        PetScale.Value = s.PetScale;
        RoamInterval.Value = s.PetRoamInterval;
        StatDecay.Value = s.PetStatDecaySpeed;
        MusicDance.IsChecked = s.PetMusicDance;
        SoundVolume.Value = s.PetSoundVolume;
        SkinDirectory.Text = s.PetSkinDirectory;

        PetModelCombo.Items.Clear();
        foreach (var m in PetSkin.GetAvailableModels()) PetModelCombo.Items.Add(m);
        PetModelCombo.SelectedItem = s.PetModel;

        EnableHaptics.IsChecked = s.EnableHaptics;

        // AI
        _loading = true;
        SelectProvider(s.AiProvider);
        _loading = false;
        AiApiKey.Text = s.AiApiKey;
        AiModel.Text = s.AiModel;
        AiBaseUrl.Text = s.AiBaseUrl;
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

    private void Save()
    {
        var s = AppSettings.Instance;
        s.PetEnabled = PetEnabled.IsChecked == true;
        s.PetScale = PetScale.Value;
        s.PetRoamInterval = RoamInterval.Value;
        s.PetStatDecaySpeed = StatDecay.Value;
        s.PetMusicDance = MusicDance.IsChecked == true;
        s.PetSoundVolume = SoundVolume.Value;
        s.PetSkinDirectory = SkinDirectory.Text.Trim();
        var newModel = PetModelCombo.SelectedItem as string ?? "cat";
        bool modelChanged = newModel != s.PetModel;
        s.PetModel = newModel;

        s.EnableHaptics = EnableHaptics.IsChecked == true;

        s.AiProvider = (AiProviderCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "deepseek";
        s.AiApiKey = AiApiKey.Text.Trim();
        s.AiModel = AiModel.Text.Trim();
        s.AiBaseUrl = AiBaseUrl.Text.Trim();

        s.Save();

        if (!s.PetEnabled && PetWindow.Instance.IsVisible) PetWindow.Instance.Hide();
        if (s.PetEnabled && !PetWindow.Instance.IsVisible && PetManager.Instance.LifeState == PetLifeState.Roaming)
            PetWindow.Instance.ShowPet();

        if (modelChanged) PetManager.Instance.ReloadSkin();
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(System.EventArgs e)
    {
        base.OnClosed(e);
        Save();
    }
}
