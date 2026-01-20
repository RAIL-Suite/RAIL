using System.Collections.ObjectModel;
using System.IO;
using RailStudio.Models;

namespace RailStudio.Services;

/// <summary>
/// Service for loading and managing file system tree structures.
/// Supports recursive directory traversal with MaterialDesign icon mapping.
/// </summary>
public class FileSystemService
{
    /// <summary>
    /// Loads a directory tree recursively starting from the specified path.
    /// Shows ALL files and folders without any filtering.
    /// </summary>
    public FileSystemNode LoadDirectory(string path)
    {
        var root = new FileSystemNode
        {
            Name = System.IO.Path.GetFileName(path) ?? path,
            Path = path,
            IsDirectory = true,
            Icon = "Folder",
            Children = new ObservableCollection<FileSystemNode>()
        };
        
        try
        {
            // Load subdirectories first
            var directories = Directory.GetDirectories(path);
            foreach (var dir in directories)
            {
                try
                {
                    var childNode = LoadDirectory(dir); // Recursive
                    root.Children.Add(childNode);
                }
                catch
                {
                    // Skip directories we can't access (permissions, etc.)
                }
            }
            
            // Load files
            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                try
                {
                    var extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    root.Children.Add(new FileSystemNode
                    {
                        Name = System.IO.Path.GetFileName(file),
                        Path = file,
                        IsDirectory = false,
                        Icon = GetIconForFileType(extension)
                    });
                }
                catch
                {
                    // Skip files we can't access
                }
            }
        }
        catch
        {
            // If we can't read the directory at all, return empty node
        }
        
        return root;
    }
    
    /// <summary>
    /// Maps file extensions to MaterialDesign PackIconKind names.
    /// Comprehensive icon support for all common file types.
    /// </summary>
    private string GetIconForFileType(string extension)
    {
        return extension switch
        {
            ".cs" => "LanguageCsharp",
            ".csproj" => "LanguageCsharp",
            ".sln" => "CodeBraces",
            ".py" => "LanguagePython",
            ".js" => "LanguageJavascript",
            ".ts" => "LanguageTypescript",
            ".exe" => "Application",
            ".dll" => "Library",
            ".jar" => "LanguageJava",
            ".json" => "CodeJson",
            ".xml" => "Xml",
            ".xaml" => "Xml",
            ".md" => "FileDocument",
            ".txt" => "FileDocument",
            ".html" => "Web",
            ".css" => "LanguageCss3",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" => "FileImage",
            ".pdf" => "FilePdf",
            ".zip" or ".rar" or ".7z" => "FolderZip",
            ".log" => "TextBox",
            ".config" or ".ini" => "Cog",
            _ => "File"
        };
    }
    
    /// <summary>
    /// Finds a node by full path in the tree.
    /// </summary>
    public FileSystemNode? FindNodeByPath(FileSystemNode root, string path)
    {
        if (root.Path == path)
            return root;
            
        foreach (var child in root.Children)
        {
            var found = FindNodeByPath(child, path);
            if (found != null)
                return found;
        }
        
        return null;
    }
}




