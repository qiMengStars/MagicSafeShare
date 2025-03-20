using ImageMagick;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
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
            // 精细分类映射
            var categories = new Dictionary<string, string[]>
            {
                { "基本相机信息", new string[] { "EXIF:Make", "EXIF:Model", "EXIF:39424", "EXIF:SensingMethod" } },
                { "图像尺寸与分辨率", new string[] { "EXIF:ImageLength", "EXIF:ImageWidth", "EXIF:PixelYDimension", "EXIF:PixelXDimension", "EXIF:YResolution", "EXIF:XResolution", "EXIF:ResolutionUnit" } },
                { "时间信息", new string[] { "EXIF:DateTime", "EXIF:DateTimeOriginal", "EXIF:DateTimeDigitized", "EXIF:SubsecTimeOriginal", "EXIF:SubsecTime", "EXIF:SubsecTimeDigitized", "EXIF:OffsetTime", "EXIF:OffsetTimeOriginal", "EXIF:GPSDateStamp" } },
                { "曝光与镜头信息", new string[] { "EXIF:ApertureValue", "EXIF:ExposureBiasValue", "EXIF:ExposureProgram", "EXIF:ExposureMode", "EXIF:ExposureTime", "EXIF:Flash", "EXIF:FNumber", "EXIF:ShutterSpeedValue", "EXIF:MeteringMode", "EXIF:FocalLength", "EXIF:FocalLengthIn35mmFilm", "EXIF:ISOSpeed", "EXIF:ISOSpeedRatings", "EXIF:SensitivityType", "EXIF:BrightnessValue", "EXIF:WhiteBalance", "EXIF:LightSource" } },
                { "场景与特效", new string[] { "EXIF:SceneType", "EXIF:SceneCaptureType", "EXIF:FlashpixVersion", "EXIF:ComponentsConfiguration" } },
                { "GPS位置信息", new string[] { "EXIF:GPSLatitude", "EXIF:GPSLongitude", "EXIF:GPSAltitude", "EXIF:GPSLatitudeRef", "EXIF:GPSLongitudeRef", "EXIF:GPSSpeed", "EXIF:GPSSpeedRef", "EXIF:GPSAltitudeRef", "EXIF:GPSTimestamp", "EXIF:GPSDateStamp" } },
                { "厂商及其他信息", new string[] { "EXIF:39321", "EXIF:34970", "EXIF:34974", "EXIF:42593", "EXIF:34973", "EXIF:40965", "EXIF:34975", "EXIF:34967", "EXIF:34965" } },
                { "图像其它属性", new string[] { "EXIF:Orientation", "EXIF:YCbCrPositioning", "EXIF:ColorSpace" } },
                { "文件信息", new string[] { "FileName", "FileSize", "FileType" } },
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
                        foreach (var key in category.Value)
                        {
                            if (metadata.TryGetValue(key, out string value))
                            {
                                // 大数据项只显示长度
                                if (key == "XMP" || key.StartsWith("Profile:") || key.Contains("MakerNote"))
                                    Console.WriteLine($"  {key}: (Data, Length: {value.Length})");
                                else
                                    Console.WriteLine($"  {key}: {value}");
                                found = true;
                                remaining.Remove(key);
                            }
                        }
                        if (!found)
                        {
                            Console.WriteLine("  无相关数据");
                        }
                    }

                    Console.WriteLine("\n【未分类元数据】");
                    if (remaining.Count > 0)
                    {
                        foreach (var kvp in remaining)
                        {
                            if (kvp.Key == "XMP" || kvp.Key.StartsWith("Profile:"))
                                Console.WriteLine($"  {kvp.Key}: (Data, Length: {kvp.Value.Length})");
                            else
                                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("  无");
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
