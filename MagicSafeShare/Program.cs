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
            // 精细分类映射（包含中文注释和颜色）
            var categories = new Dictionary<string, (string[], string[], ConsoleColor)>
            {
                { "基本相机信息", (new string[] { "Make", "Model", "Software", "SensingMethod" }, new string[] { "制造商", "型号", "软件", "感应方法" }, ConsoleColor.Cyan) },
                { "图像尺寸与分辨率", (new string[] { "ImageLength", "ImageWidth", "PixelYDimension", "PixelXDimension", "YResolution", "XResolution", "ResolutionUnit" }, new string[] { "图像长度", "图像宽度", "像素高度", "像素宽度", "垂直分辨率", "水平分辨率", "分辨率单位" }, ConsoleColor.Magenta) },
                { "时间信息", (new string[] { "DateTime", "DateTimeOriginal", "DateTimeDigitized", "SubsecTimeOriginal", "SubsecTime", "SubsecTimeDigitized", "OffsetTime", "OffsetTimeOriginal", "GPSDateStamp" }, new string[] { "日期时间", "原始日期时间", "数字化日期时间", "原始子秒时间", "子秒时间", "数字化子秒时间", "偏移时间", "原始偏移时间", "GPS日期戳" }, ConsoleColor.Yellow) },
                { "曝光与镜头信息", (new string[] { "ApertureValue", "ExposureBiasValue", "ExposureProgram", "ExposureMode", "ExposureTime", "Flash", "FNumber", "ShutterSpeedValue", "MeteringMode", "FocalLength", "FocalLengthIn35mmFilm", "ISOSpeed", "ISOSpeedRatings", "SensitivityType", "BrightnessValue", "WhiteBalance", "LightSource" }, new string[] { "光圈值", "曝光偏差值", "曝光程序", "曝光模式", "曝光时间", "闪光灯", "光圈", "快门速度值", "测光模式", "焦距", "35mm等效焦距", "ISO速度", "ISO速度等级", "感光度类型", "亮度值", "白平衡", "光源" }, ConsoleColor.Green) },
                { "场景与特效", (new string[] { "SceneType", "SceneCaptureType", "FlashpixVersion", "ComponentsConfiguration" }, new string[] { "场景类型", "场景捕捉类型", "Flashpix版本", "组件配置" }, ConsoleColor.DarkCyan) },
                { "GPS位置信息", (new string[] { "GPSLatitude", "GPSLongitude", "GPSAltitude", "GPSLatitudeRef", "GPSLongitudeRef", "GPSSpeed", "GPSSpeedRef", "GPSAltitudeRef", "GPSTimestamp" }, new string[] { "GPS纬度", "GPS经度", "GPS高度", "GPS纬度参考", "GPS经度参考", "GPS速度", "GPS速度参考", "GPS高度参考", "GPS时间戳" }, ConsoleColor.DarkMagenta) },
                { "厂商及其他信息", (new string[] { "39321", "34970", "34974", "42593", "34973", "40965", "34975", "34967", "34965" }, new string[] { "厂商特定信息1", "厂商特定信息2", "厂商特定信息3", "厂商特定信息4", "厂商特定信息5", "厂商特定信息6", "厂商特定信息7", "厂商特定信息8", "厂商特定信息9" }, ConsoleColor.DarkYellow) },
                { "图像其他属性", (new string[] { "Orientation", "YCbCrPositioning", "ColorSpace" }, new string[] { "方向", "YCbCr定位", "色彩空间" }, ConsoleColor.DarkGreen) },
                 { "文件信息", (new string[] { "FileName", "FileSize", "FileType" }, new string[] { "文件名", "文件大小", "文件类型" }, ConsoleColor.Blue) },
            };

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
                        Console.WriteLine($"\n【{category.Key}】");
                        bool found = false;
                        SetColor(category.Value.Item3); // 设置分类颜色
                        for (int i = 0; i < category.Value.Item1.Length; i++)
                        {
                            string key = category.Value.Item1[i];
                            string displayName = category.Value.Item2[i]; // 获取中文名称
                            // 优先匹配完整键名（例如 "EXIF:Make"），然后匹配不带前缀的键名（例如 "Make"）
                            if (metadata.TryGetValue("EXIF:" + key, out string value) || metadata.TryGetValue(key, out value))
                            {
                                if (key == "XMP" || key.StartsWith("Profile:") || key.Contains("MakerNote"))
                                    Console.WriteLine($"  {key} ({displayName}): (数据, 长度: {value.Length})");
                                else
                                    Console.WriteLine($"  {key} ({displayName}): {value}");
                                found = true;
                                remaining.Remove("EXIF:" + key); // 移除已匹配的键
                                remaining.Remove(key);     // 移除不带前缀的键
                            }
                        }
                        ResetColor();
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
                            if (kvp.Key == "XMP" || kvp.Key.StartsWith("Profile:"))
                                Console.WriteLine($"  {kvp.Key}: (数据, 长度: {kvp.Value.Length})");
                            else
                                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
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
                var exifVal = value.GetValue();
                if (exifVal != null)
                    metadata[$"EXIF:{value.Tag}"] = exifVal.ToString();
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

