using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace DeskPet.Services;

/// <summary>Imports a sequence-frame skin (folder or zip) as a new pet model.</summary>
public static class SkinImporter
{
    public static void ImportFromFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select a pet skin folder (contains idle_0/, walk_0/, ...)",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var source = Path.GetFullPath(dlg.FolderName);
            var name = new DirectoryInfo(source).Name;
            PetSkin.ImportSkinFolder(source, name);
            PetManager.Instance.SwitchModel(name);
            MessageBox.Show($"Skin \"{name}\" imported and applied.", "DeskPet",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Import failed: " + ex.Message, "DeskPet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static void ImportFromZip()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select a pet skin zip archive",
            Filter = "Zip archive (*.zip)|*.zip",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var name = Path.GetFileNameWithoutExtension(dlg.FileName);
            PetSkin.ImportSkinZip(dlg.FileName, name);
            PetManager.Instance.SwitchModel(name);
            MessageBox.Show($"Skin \"{name}\" imported and applied.", "DeskPet",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Import failed: " + ex.Message, "DeskPet", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
