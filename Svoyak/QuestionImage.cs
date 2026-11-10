using System.IO;
using System.Windows.Media.Imaging;

namespace Svoyak;

internal static class QuestionImage
{
    public static BitmapImage Load(string path)
    {
        // Load once and release the file so the editor can replace the image later.
        using var stream = File.OpenRead(path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
