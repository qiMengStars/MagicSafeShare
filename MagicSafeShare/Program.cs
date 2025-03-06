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

Console.WriteLine("图片放好后，请输入「1」继续");

string command = Console.ReadLine();

if (command == "1")
{
    Console.WriteLine("开始处理图片");

    string inputDirectory = folderPath ;  // 输入文件夹路径
    string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "MSSoutput");      // 输出文件夹路径

    // 如果输出文件夹不存在，则创建它
    if (!Directory.Exists(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    // 获取输入目录下所有图像文件（假设支持的格式为 jpg, png, gif 等）
    string[] imageFiles = Directory.GetFiles(inputDirectory, "*.*", SearchOption.TopDirectoryOnly);

    foreach (var filePath in imageFiles)
    {
        // 只处理图像文件（可以根据需要增加其他图像格式）
        if (IsImageFile(filePath))
        {
            string fileName = Path.GetFileName(filePath);  // 获取文件名
            string outputPath = Path.Combine(outputDirectory, fileName);  // 生成输出路径

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

// 判断文件是否为图像文件（可以扩展更多的图像格式）
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