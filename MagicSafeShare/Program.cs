using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using ImageMagick;

namespace MagicSafeShare
{
    /// <summary>
    /// 程序入口：图片元数据管理工具
    /// 提供两种模式：
    /// 1. 清除图片元数据
    /// 2. 扫描并分类显示元数据
    /// </summary>
    class Program
    {
        static void Main()
        {
            // 初始化工作目录
            string inputFolder = Path.Combine(Directory.GetCurrentDirectory(), "MSSresource");
            string outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "MSSoutput");

            // 创建并打开资源目录
            FolderManager.CreateFolder(inputFolder);
            FolderManager.CreateFolder(outputFolder);
            FolderManager.OpenFolder(inputFolder);

            Console.WriteLine("请将图片放入MSSresource文件夹后选择操作：");
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
                    Console.WriteLine("请输入有效命令：1 或 2");
                    break;
            }
        }
    }

    // /// 跨平台文件夹管理工具
    /// </summary>
    public static class FolderManager
    {
        // /// 创建文件夹（如果不存在）
        /// </summary>
        public static void CreateFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine($"创建文件夹: {path}");
            }
        }

        // /// 用系统默认应用打开文件夹
        /// </summary>
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
                    Console.WriteLine("不支持的系统平台");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开失败: {ex.Message}");
            }
        }
    }

    // /// 控制台颜色工具类
    /// </summary>
    public static class ConsoleHelper
    {
        public static void SetColor(ConsoleColor color) => Console.ForegroundColor = color;

        public static void ResetColor() => Console.ResetColor();
    }

    // /// 图像处理核心工具
    /// </summary>
    public static class ImageProcessor
    {
        // /// 清除图片元数据并保存到输出目录
        /// </summary>
        public static void StripMetadata(
            string[] imageFiles,
            string inputFolder,
            string outputFolder
        )
        {
            Console.WriteLine("开始处理图片...");

            foreach (var filePath in imageFiles)
            {
                if (!IsImageFile(filePath))
                    continue;

                string fileName = Path.GetFileName(filePath);
                string outputPath = Path.Combine(outputFolder, fileName);

                using (var image = new MagickImage(filePath))
                {
                    image.Strip(); // 清除元数据
                    image.Write(outputPath);
                }
                Console.WriteLine($"已处理: {fileName}");
            }
            Console.WriteLine("处理完成！");
        }

        // /// 检查是否为支持的图片格式
        /// </summary>
        public static bool IsImageFile(string filePath)
        {
            string[] supportedExts = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
            return supportedExts.Contains(Path.GetExtension(filePath).ToLower());
        }
    }

    // /// 元数据扫描与显示模块
    /// </summary>
    public static class MetadataScanner
    {
        // /// 扫描并显示图片元数据
        /// </summary>
        public static void ScanAndDisplayMetadata(string[] imageFiles)
        {
            string xmlPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "MetadataCategories.xml"
            );
            var categories = MetadataHelper.LoadMetadataCategories(xmlPath);

            foreach (var filePath in imageFiles)
            {
                if (!ImageProcessor.IsImageFile(filePath))
                    continue;

                string fileName = Path.GetFileName(filePath);
                Console.WriteLine($"\n处理文件: {fileName}");

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

        // /// 按分类显示元数据
        /// </summary>
        private static void DisplayMetadata(
            Dictionary<string, MetadataCategory> categories,
            Dictionary<string, string> metadata,
            string fileName
        )
        {
            var remaining = new Dictionary<string, string>(metadata);

            foreach (var category in categories)
            {
                ConsoleHelper.SetColor(category.Value.Color);
                Console.WriteLine($"\n【{category.Key}】");
                ConsoleHelper.ResetColor();

                bool hasData = false;
                foreach (var keyPair in category.Value.Keys)
                {
                    string key = keyPair.Key;
                    string displayName = keyPair.DisplayName;

                    if (
                        metadata.TryGetValue($"EXIF:{key}", out string value)
                        || metadata.TryGetValue(key, out value)
                    )
                    {
                        Console.Write("  ");
                        ConsoleHelper.SetColor(ConsoleColor.White);
                        Console.Write($"{key} ({displayName}): ");
                        ConsoleHelper.ResetColor();
                        Console.WriteLine(FormatMetadataValue(key, value));

                        remaining.Remove($"EXIF:{key}");
                        remaining.Remove(key);
                        hasData = true;
                    }
                }

                if (!hasData)
                {
                    ConsoleHelper.SetColor(ConsoleColor.DarkGray);
                    Console.WriteLine("  无数据");
                    ConsoleHelper.ResetColor();
                }
            }

            Console.WriteLine("\n【未分类元数据】");
            if (remaining.Count > 0)
            {
                ConsoleHelper.SetColor(ConsoleColor.Red);
                foreach (var kvp in remaining)
                {
                    Console.Write("  ");
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

        // /// 格式化元数据值显示
        /// </summary>
        private static object FormatMetadataValue(string key, string value)
        {
            if (long.TryParse(value, out long longVal))
                return longVal;
            if (double.TryParse(value, out double doubleVal))
                return doubleVal;

            if (key == "XMP" || key.StartsWith("Profile:") || key.Contains("MakerNote"))
                return $"(数据, 长度: {value.Length})";

            return value;
        }
    }

    // /// 元数据配置与提取工具
    /// </summary>
    public static class MetadataHelper
    {
        // /// 从XML加载元数据分类配置
        /// </summary>
        public static Dictionary<string, MetadataCategory> LoadMetadataCategories(
            string xmlFilePath
        )
        {
            var categories = new Dictionary<string, MetadataCategory>();

            if (!File.Exists(xmlFilePath))
            {
                Console.WriteLine("找不到分类配置文件！");
                Console.WriteLine("当前目录文件列表：");
                foreach (var file in Directory.GetFiles(Directory.GetCurrentDirectory()))
                    Console.WriteLine(file);
                return categories;
            }

            try
            {
                XDocument doc = XDocument.Load(xmlFilePath);
                foreach (var categoryElement in doc.Descendants("Category"))
                {
                    string name = categoryElement.Attribute("Name")?.Value;
                    if (
                        Enum.TryParse<ConsoleColor>(
                            categoryElement.Attribute("Color")?.Value,
                            out var color
                        )
                    )
                    {
                        var keys = categoryElement
                            .Elements("Key")
                            .Where(e =>
                                !string.IsNullOrEmpty(e.Attribute("Value")?.Value)
                                && !string.IsNullOrEmpty(e.Attribute("DisplayName")?.Value)
                            )
                            .Select(e =>
                                (e.Attribute("Value").Value, e.Attribute("DisplayName").Value)
                            )
                            .ToList();

                        categories[name] = new MetadataCategory
                        {
                            Name = name,
                            Color = color,
                            Keys = keys.ToArray(),
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"配置加载失败: {ex.Message}");
            }

            return categories;
        }

        public static Dictionary<string, string> GetAllMetadata(MagickImage image)
        {
            var metadata = new Dictionary<string, string>();

            // 处理EXIF数据
            SafeAddMetadata(
                metadata,
                "EXIF",
                image.GetExifProfile(),
                exif =>
                    exif.Values.Select(v => new KeyValuePair<string, string>(
                        $"EXIF:{v.Tag}",
                        v.GetValue()?.ToString() ?? string.Empty
                    ))
            );

            // 处理XMP数据
            SafeAddMetadata(
                metadata,
                "XMP",
                image.GetXmpProfile(),
                xmp => new[] { new KeyValuePair<string, string>("XMP", xmp.ToString()) }
            );

            // 处理IPTC数据
            SafeAddMetadata(
                metadata,
                "IPTC",
                image.GetIptcProfile(),
                iptc =>
                    iptc.Values.Select(v => new KeyValuePair<string, string>(
                        $"IPTC:{v.Tag}",
                        v.ToString()
                    ))
            );

            // 处理其他配置文件（非EXIF/XMP/IPTC）
            foreach (var name in image.ProfileNames)
            {
                if (name.ToLower() is "exif" or "xmp" or "iptc")
                    continue;
                var profile = image.GetProfile(name);
                if (profile != null)
                    metadata[$"Profile:{name}"] = Convert.ToBase64String(profile.ToByteArray());
            }

            // 处理常见属性
            var attributes = new[] { "Make", "Model", "Software", "DateTime", "Copyright" };
            foreach (var attr in attributes)
            {
                string value = image.GetAttribute(attr);
                if (!string.IsNullOrEmpty(value))
                {
                    metadata[$"Attribute:{attr}"] = value;
                }
            }

            return metadata;
        }

        // 通用安全处理方法
        private static void SafeAddMetadata<T>(
            Dictionary<string, string> metadata,
            string categoryKey,
            T profile,
            Func<T, IEnumerable<KeyValuePair<string, string>>> extractor
        )
            where T : class
        {
            try
            {
                if (profile != null)
                {
                    foreach (var kvp in extractor(profile))
                    {
                        metadata[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{categoryKey} metadata extraction failed: {ex.Message}");
            }
        }
    }

    // /// 元数据分类配置模型
    /// </summary>
    public class MetadataCategory
    {
        public string Name { get; set; }
        public ConsoleColor Color { get; set; }
        public (string Key, string DisplayName)[] Keys { get; set; }
    }
}
