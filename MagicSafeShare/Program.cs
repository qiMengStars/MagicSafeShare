using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ImageMagick;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MagicSafeShare
{
    /// <summary>
    /// 程序入口类，处理图片元数据管理核心功能
    /// </summary>
    class Program
    {
        static void Main()
        {
            // 初始化文件夹路径
            string inputFolder = Path.Combine(Directory.GetCurrentDirectory(), "MSSresource");
            string outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "MSSoutput");

            // 创建必要文件夹并打开资源目录
            FolderManager.CreateFolder(inputFolder);
            FolderManager.CreateFolder(outputFolder);
            FolderManager.OpenFolder(inputFolder);

            Console.WriteLine("请将需要处理的图片放入MSSresource文件夹");
            Console.WriteLine("图片放好后，请输入相应数字继续");
            Console.WriteLine("【1】清除所有图像元数据");
            Console.WriteLine("【2】扫描并分类列出图像元数据");

            string command = Console.ReadLine();
            var imageFiles = Directory.GetFiles(inputFolder, "*.*", SearchOption.TopDirectoryOnly);

            switch (command)
            {
                case "1":
                    ImageProcessor.StripMetadata(imageFiles, inputFolder, outputFolder);
                    FolderManager.OpenFolder(outputFolder);
                    break;
                case "2":
                    MetadataScanner.ScanAndDisplayMetadata(imageFiles);
                    break;
                default:
                    Console.WriteLine("无效命令输入，请输入1或2");
                    break;
            }
        }
    }

    /// <summary>
    /// 文件夹管理工具类，处理跨平台文件夹操作
    /// </summary>
    public static class FolderManager
    {
        /// <summary>
        /// 创建指定路径的文件夹（如果不存在）
        /// </summary>
        /// <param name="path">要创建的文件夹路径</param>
        public static void CreateFolder(string path)
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

        /// <summary>
        /// 使用系统默认应用打开指定文件夹
        /// </summary>
        /// <param name="path">要打开的文件夹路径</param>
        public static void OpenFolder(string path)
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
    }

    /// <summary>
    /// 控制台输出辅助类，处理带颜色的文本输出
    /// </summary>
    public static class ConsoleHelper
    {
        /// <summary>
        /// 设置控制台前景色
        /// </summary>
        /// <param name="color">要设置的颜色</param>
        public static void SetColor(ConsoleColor color)
        {
            Console.ForegroundColor = color;
        }

        /// <summary>
        /// 重置控制台颜色为默认值
        /// </summary>
        public static void ResetColor()
        {
            Console.ResetColor();
        }
    }

    /// <summary>
    /// 图像处理工具类，包含元数据处理相关方法
    /// </summary>
    public static class ImageProcessor
    {
        /// <summary>
        /// 清除指定图像文件的元数据
        /// </summary>
        /// <param name="imageFiles">要处理的图像文件路径数组</param>
        /// <param name="inputFolder">输入文件夹路径</param>
        /// <param name="outputFolder">输出文件夹路径</param>
        public static void StripMetadata(string[] imageFiles, string inputFolder, string outputFolder)
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
        }

        /// <summary>
        /// 判断文件是否为支持的图像格式
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否为图像文件</returns>
        public static bool IsImageFile(string filePath)
        {
            string[] extensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
            string ext = Path.GetExtension(filePath).ToLower();
            return Array.Exists(extensions, e => e == ext);
        }
    }

    /// <summary>
    /// 元数据处理类，负责元数据扫描和分类显示
    /// </summary>
    public static class MetadataScanner
    {
        /// <summary>
        /// 扫描并显示所有图像文件的元数据
        /// </summary>
        /// <param name="imageFiles">要处理的图像文件路径数组</param>
        public static void ScanAndDisplayMetadata(string[] imageFiles)
        {
            string xmlPath = Path.Combine(Directory.GetCurrentDirectory(), "MetadataCategories.xml");
            var categories = MetadataHelper.LoadMetadataCategories(xmlPath);

            foreach (var filePath in imageFiles)
            {
                if (!ImageProcessor.IsImageFile(filePath)) continue;

                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"\n正在处理文件: {fileName}");

                using (var image = new MagickImage(filePath))
                {
                    var metadata = MetadataHelper.GetAllMetadata(image);
                    metadata["FileName"] = fileName;
                    metadata["FileSize"] = new FileInfo(filePath).Length.ToString();
                    metadata["FileType"] = Path.GetExtension(filePath).ToUpper();

                    DisplayMetadata(categories, metadata, fileName);
                }
            }
        }

        /// <summary>
        /// 显示分类后的元数据
        /// </summary>
        /// <param name="categories">元数据分类配置</param>
        /// <param name="metadata">提取的元数据字典</param>
        /// <param name="fileName">文件名</param>
        private static void DisplayMetadata(Dictionary<string, MetadataCategory> categories, Dictionary<string, string> metadata, string fileName)
        {
            var remaining = new Dictionary<string, string>(metadata);

            foreach (var category in categories)
            {
                ConsoleHelper.SetColor(category.Value.Color);
                Console.WriteLine($"\n【{category.Key}】");
                ConsoleHelper.ResetColor();

                bool found = false;
                foreach (var keyPair in category.Value.Keys)
                {
                    string key = keyPair.Key;
                    string displayName = keyPair.DisplayName;
                    if (metadata.TryGetValue("EXIF:" + key, out string value) || metadata.TryGetValue(key, out value))
                    {
                        Console.Write($"  ");
                        ConsoleHelper.SetColor(ConsoleColor.White);
                        Console.Write($"{key} ({displayName}): ");
                        ConsoleHelper.ResetColor();

                        object displayValue = FormatMetadataValue(key, value);
                        Console.WriteLine(displayValue);
                        found = true;
                        remaining.Remove("EXIF:" + key);
                        remaining.Remove(key);
                    }
                }
                if (!found)
                {
                    ConsoleHelper.SetColor(ConsoleColor.DarkGray);
                    Console.WriteLine("  无相关数据");
                    ConsoleHelper.ResetColor();
                }
            }

            Console.WriteLine("\n【未分类元数据】");
            if (remaining.Count > 0)
            {
                ConsoleHelper.SetColor(ConsoleColor.Red);
                foreach (var kvp in remaining)
                {
                    Console.Write($"  ");
                    ConsoleHelper.SetColor(ConsoleColor.White);
                    Console.Write($"{kvp.Key}: ");
                    ConsoleHelper.ResetColor();
                    Console.WriteLine(FormatMetadataValue(kvp.Key, kvp.Value));
                }
                ConsoleHelper.ResetColor();
            }
            else
            {
                ConsoleHelper.SetColor(ConsoleColor.DarkGray);
                Console.WriteLine("  无");
                ConsoleHelper.ResetColor();
            }
            Console.WriteLine("--------------------");
        }

        /// <summary>
        /// 格式化元数据值显示方式
        /// </summary>
        /// <param name="key">元数据键名</param>
        /// <param name="value">原始值</param>
        /// <returns>格式化后的显示值</returns>
        private static object FormatMetadataValue(string key, string value)
        {
            if (long.TryParse(value, out long longValue))
                return longValue;
            if (double.TryParse(value, out double doubleValue))
                return doubleValue;
            if (key == "XMP" || key.StartsWith("Profile:") || key.Contains("MakerNote"))
                return $"(数据, 长度: {value.Length})";
            return value;
        }
    }

    /// <summary>
    /// 元数据帮助类，处理元数据加载和提取
    /// </summary>
    public static class MetadataHelper
    {
        /// <summary>
        /// 加载元数据分类配置文件
        /// </summary>
        /// <param name="xmlFilePath">XML配置文件路径</param>
        /// <returns>元数据分类字典</returns>
        public static Dictionary<string, MetadataCategory> LoadMetadataCategories(string xmlFilePath)
        {
            var categories = new Dictionary<string, MetadataCategory>();

            if (!File.Exists(xmlFilePath))
            {
                Console.WriteLine("找不到分类配置文件！");
                Console.WriteLine("此目录下所有文件：");
                foreach (var file in Directory.GetFiles(Directory.GetCurrentDirectory()))
                {
                    Console.WriteLine(file);
                }
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

        /// <summary>
        /// 提取指定图像的所有元数据
        /// </summary>
        /// <param name="image">MagickImage对象</param>
        /// <returns>包含所有元数据的字典</returns>
        public static Dictionary<string, string> GetAllMetadata(MagickImage image)
        {
            var metadata = new Dictionary<string, string>();

            // 提取EXIF信息
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

            // 提取XMP信息
            var xmp = image.GetXmpProfile();
            if (xmp != null)
                metadata["XMP"] = xmp.ToString();

            // 提取IPTC信息
            var iptc = image.GetIptcProfile();
            if (iptc != null)
            {
                foreach (var value in iptc.Values)
                {
                    metadata[$"IPTC:{value.Tag}"] = value.ToString();
                }
            }

            // 提取其他配置文件信息
            foreach (var name in image.ProfileNames)
            {
                if (name.ToLower() is "exif" or "xmp" or "iptc") continue;
                var profile = image.GetProfile(name);
                if (profile != null)
                    metadata[$"Profile:{name}"] = Convert.ToBase64String(profile.ToByteArray());
            }

            // 提取常见属性
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

    /// <summary>
    /// 元数据分类配置类
    /// </summary>
    public class MetadataCategory
    {
        /// <summary>
        /// 分类名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 分类显示颜色
        /// </summary>
        public ConsoleColor Color { get; set; }

        /// <summary>
        /// 分类包含的元数据键值对
        /// </summary>
        public (string Key, string DisplayName)[] Keys { get; set; }
    }
}