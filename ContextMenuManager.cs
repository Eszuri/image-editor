using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ImageEditor;

public static class ContextMenuManager
{
    public const string VerbKey = "ImageEditor";
    public const string MenuText = "Edit image with Image Editor";
    public static readonly string[] SupportedExtensions =
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif", ".tiff", ".tif", ".ico"
    };

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\SystemFileAssociations\image\shell\" + VerbKey);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static void Register(string? exePath = null)
    {
        exePath ??= Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;

        string command = $"\"{exePath}\" \"%1\"";
        string icon = $"\"{exePath}\",0";

        // Register under generic image perceived type
        RegisterVerb(@"Software\Classes\SystemFileAssociations\image\shell\" + VerbKey, MenuText, icon, command);

        // Register under explicit image file extensions
        foreach (var ext in SupportedExtensions)
        {
            RegisterVerb($@"Software\Classes\SystemFileAssociations\{ext}\shell\{VerbKey}", MenuText, icon, command);
        }
    }

    public static void Unregister()
    {
        UnregisterVerb(@"Software\Classes\SystemFileAssociations\image\shell\" + VerbKey);
        foreach (var ext in SupportedExtensions)
        {
            UnregisterVerb($@"Software\Classes\SystemFileAssociations\{ext}\shell\{VerbKey}");
        }
    }

    private static void RegisterVerb(string subKeyPath, string text, string icon, string command)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(subKeyPath);
            if (key == null) return;
            key.SetValue("", text);
            key.SetValue("Icon", icon);

            using var cmdKey = key.CreateSubKey("command");
            cmdKey?.SetValue("", command);
        }
        catch { }
    }

    private static void UnregisterVerb(string subKeyPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKeyPath, false);
        }
        catch { }
    }
}
