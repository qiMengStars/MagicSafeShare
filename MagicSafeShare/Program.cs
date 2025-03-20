using ImageMagick;
using System.Diagnostics;
using System.Runtime.InteropServices;

Console.WriteLine("请将需要处理的图片放入MSSresource文件夹");

string folderName = "MSSresource";
string folderPath = Path.Combine(Directory.GetCurrentDirectory(), folderName);

// 创建文件夹
if (!Directory.Exists(folderPath))
{
    Directory.CreateDirectory(folderPath);
    Console.WriteLine($"文件夹已创建: {folderPath}");
}
else
{
    Console.WriteLine($"文件夹已存在: {folderPath}");
}

// 打开文件夹
OpenFolder(folderPath);

string inputDirectory = folderPath;  // 输入文件夹路径
string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "MSSoutput");        // 输出文件夹路径

// 如果输出文件夹不存在，则创建它
if (!Directory.Exists(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

// 获取输入目录下所有图像文件（假设支持的格式为 jpg, png, gif 等）
string[] imageFiles = Directory.GetFiles(inputDirectory, "*.*", SearchOption.TopDirectoryOnly);

Console.WriteLine("图片放好后，请输入相应数字继续");
Console.WriteLine("【1】直接清除所有图像元数据，这包括位置，动态图片的动态视频部分等等");
Console.WriteLine("【2】扫描并列出图像的元数据，进行进一步选择");


string command = Console.ReadLine();

if (command == "1")
{
    Console.WriteLine("开始处理图片");

    foreach (var filePath in imageFiles)
    {
        // 只处理图像文件（可以根据需要增加其他图像格式）
        if (IsImageFile(filePath))
        {
            string fileName = Path.GetFileName(filePath);    // 获取文件名
            string outputPath = Path.Combine(outputDirectory, fileName);    // 生成输出路径

            // 使用 Magick.NET 打开图像并去除元数据
            using (MagickImage image = new MagickImage(filePath))
            {
                // 移除所有元数据
                image.Strip();

                // 保存处理后的图像到 output 文件夹 
                image.Write(outputPath);
            }

            Console.WriteLine($"已处理: {fileName}");

            OpenFolder(outputDirectory);

        }
    }


    Console.WriteLine("所有图像处理完成！");
}
else if (command == "2")
{
    // 预定义的元数据类别对照表
    Dictionary<string, string[]> metadataCategories = new Dictionary<string, string[]>()
    {
        { "基本信息", new string[] { "Make", "Model", "Software", "DateTime" } },
        { "位置信息", new string[] { "GPSLatitude", "GPSLongitude", "GPSAltitude" } },
        { "相机信息", new string[] { "ExposureTime", "FNumber", "ISOSpeedRatings" } },
        { "版权信息", new string[] { "Copyright" } },
        { "文件信息", new string[] {"FileName", "FileSize", "FileType"} } //Added File Information
    };

    foreach (var filePath in imageFiles)
    {
        // 只处理图像文件
        if (IsImageFile(filePath))
        {
            string fileName = Path.GetFileName(filePath);
            Console.WriteLine($"\n正在处理文件: {fileName}");

            using (var image = new MagickImage(filePath))
            {
                // 获取所有元数据
                var allMetadata = GetAllMetadata(image);

                // 添加文件名、大小和类型到元数据中
                allMetadata.Add("FileName", fileName);
                allMetadata.Add("FileSize", new FileInfo(filePath).Length.ToString());
                allMetadata.Add("FileType", Path.GetExtension(filePath).ToUpper());

                // 遍历元数据类别
                foreach (var category in metadataCategories)
                {
                    Console.WriteLine($"\n【{category.Key}】"); // 输出类别名称
                    bool hasDataInCategory = false; // 标记是否有数据在该类别下

                    // 遍历当前类别下的元数据名称
                    foreach (var metadataName in category.Value)
                    {
                        // 检查元数据是否存在于提取的所有元数据中
                        if (allMetadata.ContainsKey(metadataName))
                        {
                            Console.WriteLine($"  {metadataName}: {allMetadata[metadataName]}"); // 输出元数据
                            hasDataInCategory = true; // 设置标记为true
                        }
                    }
                    if (!hasDataInCategory)
                    {
                        Console.WriteLine("  无相关数据"); // 如果类别下没有数据，输出提示
                    }
                }
                Console.WriteLine("--------------------");
            }
        }
    }
}

// 判断文件是否为图像文件
static bool IsImageFile(string filePath)
{
    string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" };
    string fileExtension = Path.GetExtension(filePath).ToLower();
    foreach (var ext in imageExtensions)
    {
        if (fileExtension == ext)
        {
            return true;
        }
    }
    return false;
}

static void OpenFolder(string path)
{
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start("explorer", path);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", path);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", path);
        }
        else
        {
            Console.WriteLine("不支持的操作系统");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"无法打开文件夹: {ex.Message}");
    }
}
static Dictionary<string, string> GetAllMetadata(MagickImage image)
{
    var metadata = new Dictionary<string, string>();

    // 获取 EXIF 元数据
    var exifProfile = image.GetExifProfile();
    if (exifProfile != null)
    {
        foreach (var value in exifProfile.Values)
        {
            var exifValue = value.GetValue();
            if (exifValue != null)
            {
                metadata[$"EXIF:{value.Tag}"] = exifValue.ToString();
            }
        }
    }

    // 获取 XMP 元数据
    var xmpProfile = image.GetXmpProfile();
    if (xmpProfile != null)
    {
        // Read the entire XMP string.  You might need an XML parser for complex XMP data.
        metadata["XMP"] = xmpProfile.ToString();
    }

    // 获取 IPTC 元数据
    var iptcProfile = image.GetIptcProfile();
    if (iptcProfile != null)
    {
        foreach (var value in iptcProfile.Values)
        {
            var iptcValue = value.ToString();
            if (iptcValue != null)
            {
                metadata[$"IPTC:{value.Tag}"] = iptcValue.ToString();
            }
        }
    }

    // 获取其他配置文件数据
    foreach (var profileName in image.ProfileNames)
    {
        var profile = image.GetProfile(profileName);
        if (profile != null && profileName != "exif" && profileName != "xmp" && profileName != "iptc") // Exclude already processed
        {
            metadata[$"Profile:{profileName}"] = Convert.ToBase64String(profile.ToByteArray());
        }
    }

    // 直接使用 GetAttribute 获取可能的额外键
    string[] commonKeys = { "Make", "Model", "Software", "DateTime", "Copyright" };
    foreach (var key in commonKeys)
    {
        string value = image.GetAttribute(key);
        if (!string.IsNullOrEmpty(value))
        {
            metadata[$"Attribute:{key}"] = value;
        }
    }

    return metadata;
}

