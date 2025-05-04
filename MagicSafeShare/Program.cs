using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ImageMagick;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
    // 控制台文本颜色辅助方法
    static void SetColor(ConsoleColor color)
    {
        Console.ForegroundColor = color;
    }

    // 重置控制台文本颜色
    static void ResetColor()
    {
        Console.ResetColor();
    }

    static void Main()
    {
        Console.WriteLine("请将需要处理的图片放入MSSresource文件夹");

        string inputFolder = Path.Combine(Directory.GetCurrentDirectory(), "MSSresource");
        string outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "MSSoutput");

        CreateFolder(inputFolder);
        CreateFolder(outputFolder);
        OpenFolder(inputFolder);

        // 获取支持的图像文件
        string[] imageFiles = Directory.GetFiles(inputFolder, "*.*", SearchOption.TopDirectoryOnly);
        Console.WriteLine("图片放好后，请输入相应数字继续");
        Console.WriteLine("【1】清除所有图像元数据");
        Console.WriteLine("【2】扫描并分类列出图像元数据");

        string command = Console.ReadLine();

        if (command == "1")
        {
            Console.WriteLine("开始处理图片");

            foreach (var filePath in imageFiles)
            {
                if (!IsImageFile(filePath)) continue;

                string fileName = Path.GetFileName(filePath);
                string outputPath = Path.Combine(outputFolder, fileName);

                using (var image = new MagickImage(filePath))
                {
                    image.Strip();
                    image.Write(outputPath);
                }
                Console.WriteLine($"已处理: {fileName}");
            }
            Console.WriteLine("所有图像处理完成！");
            OpenFolder(outputFolder);
        }
        else if (command == "2")
        {
            var categories = LoadMetadataCategories("MetadataCategories.xml");

            foreach (var filePath in imageFiles)
            {
                if (!IsImageFile(filePath)) continue;

                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"\n正在处理文件: {fileName}");
                using (var image = new MagickImage(filePath))
                {
                    // 提取所有元数据并附加文件信息
                    var metadata = GetAllMetadata(image);
                    metadata["FileName"] = fileName;
                    metadata["FileSize"] = new FileInfo(filePath).Length.ToString();
                    metadata["FileType"] = Path.GetExtension(filePath).ToUpper();

                    // 用于记录未分类的元数据
                    var remaining = new Dictionary<string, string>(metadata);

                    foreach (var category in categories)
                    {
                        SetColor(category.Value.Color); // 设置分类颜色
                        Console.WriteLine($"\n【{category.Key}】");
                        ResetColor(); // 恢复默认颜色，以便元数据值以默认颜色显示

                        bool found = false;
                        foreach (var keyPair in category.Value.Keys)
                        {
                            string key = keyPair.Key;
                            string displayName = keyPair.DisplayName;
                            if (metadata.TryGetValue("EXIF:" + key, out string value) || metadata.TryGetValue(key, out value))
                            {
                                Console.Write($"  ");
                                SetColor(ConsoleColor.White); // 元数据名称
                                Console.Write($"{key} ({displayName}): ");
                                ResetColor(); // 恢复默认
                                object displayValue = value;

                                // 尝试转换为数值
                                if (long.TryParse(value, out long longValue))
                                {
                                    displayValue = longValue;
                                }
                                else if (double.TryParse(value, out double doubleValue))
                                {
                                    displayValue = doubleValue;
                                }
                                else if (key == "XMP" || key.StartsWith("Profile:") || key.Contains("MakerNote"))
                                {
                                    displayValue = $"(数据, 长度: {value.Length})";
                                }
                                Console.WriteLine(displayValue);
                                found = true;
                                remaining.Remove("EXIF:" + key);
                                remaining.Remove(key);
                            }
                        }
                        if (!found)
                        {
                            SetColor(ConsoleColor.DarkGray);
                            Console.WriteLine("  无相关数据");
                            ResetColor();
                        }
                    }

                    Console.WriteLine("\n【未分类元数据】");
                    if (remaining.Count > 0)
                    {
                        SetColor(ConsoleColor.Red);
                        foreach (var kvp in remaining)
                        {
                            Console.Write($"  ");
                            SetColor(ConsoleColor.White);
                            Console.Write($"{kvp.Key}: ");
                            ResetColor();
                            object displayValue = kvp.Value;
                            if (kvp.Key == "XMP" || kvp.Key.StartsWith("Profile:"))
                                displayValue = $"(数据, 长度: {kvp.Value.Length})";
                            Console.WriteLine(displayValue);
                        }
                        ResetColor();
                    }
                    else
                    {
                        SetColor(ConsoleColor.DarkGray);
                        Console.WriteLine("  无");
                        ResetColor();
                    }
                    Console.WriteLine("--------------------");
                }
            }
        }
    }

    static Dictionary<string, MetadataCategory> LoadMetadataCategories(string xmlFilePath)
    {
        var categories = new Dictionary<string, MetadataCategory>();

        if (!File.Exists(xmlFilePath))
        {
            Console.WriteLine("找不到分类配置文件！");
            return categories;
        }

        try
        {
            XDocument doc = XDocument.Load(xmlFilePath);
            foreach (var categoryElement in doc.Descendants("Category"))
            {
                string name = categoryElement.Attribute("Name")?.Value;
                if (Enum.TryParse<ConsoleColor>(categoryElement.Attribute("Color")?.Value, out var color))
                {
                    var keys = new List<(string Key, string DisplayName)>();
                    foreach (var keyElement in categoryElement.Elements("Key"))
                    {
                        string keyValue = keyElement.Attribute("Value")?.Value;
                        string displayName = keyElement.Attribute("DisplayName")?.Value;
                        if (!string.IsNullOrEmpty(keyValue) && !string.IsNullOrEmpty(displayName))
                        {
                            keys.Add((keyValue, displayName));
                        }
                    }
                    categories[name] = new MetadataCategory
                    {
                        Name = name,
                        Color = color,
                        Keys = keys.ToArray()
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载分类配置失败: {ex.Message}");
        }

        return categories;
    }

    static void CreateFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Console.WriteLine($"文件夹已创建: {path}");
        }
        else
        {
            Console.WriteLine($"文件夹已存在: {path}");
        }
    }

    static bool IsImageFile(string filePath)
    {
        string[] extensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
        string ext = Path.GetExtension(filePath).ToLower();
        return Array.Exists(extensions, e => e == ext);
    }

    static void OpenFolder(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start("explorer", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", path);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", path);
            else
                Console.WriteLine("不支持的操作系统");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"无法打开文件夹: {ex.Message}");
        }
    }

    static Dictionary<string, string> GetAllMetadata(MagickImage image)
    {
        var metadata = new Dictionary<string, string>();

        // EXIF 信息
        var exif = image.GetExifProfile();
        if (exif != null)
        {
            foreach (var value in exif.Values)
            {
                object exifVal = value.GetValue();
                string valString = exifVal?.ToString() ?? string.Empty;
                metadata[$"EXIF:{value.Tag}"] = valString;
            }
        }

        // XMP 信息
        var xmp = image.GetXmpProfile();
        if (xmp != null)
            metadata["XMP"] = xmp.ToString();

        // IPTC 信息
        var iptc = image.GetIptcProfile();
        if (iptc != null)
        {
            foreach (var value in iptc.Values)
            {
                metadata[$"IPTC:{value.Tag}"] = value.ToString();
            }
        }

        // 其他配置文件（排除exif,xmp,iptc）
        foreach (var name in image.ProfileNames)
        {
            if (name.ToLower() is "exif" or "xmp" or "iptc") continue;
            var profile = image.GetProfile(name);
            if (profile != null)
                metadata[$"Profile:{name}"] = Convert.ToBase64String(profile.ToByteArray());
        }

        // 常见属性（例如：Make、Model等）
        string[] attrs = { "Make", "Model", "Software", "DateTime", "Copyright" };
        foreach (var attr in attrs)
        {
            string val = image.GetAttribute(attr);
            if (!string.IsNullOrEmpty(val))
                metadata[$"Attribute:{attr}"] = val;
        }

        return metadata;
    }
}

public class MetadataCategory
{
    public string Name { get; set; }
    public ConsoleColor Color { get; set; }
    public (string Key, string DisplayName)[] Keys { get; set; }
}