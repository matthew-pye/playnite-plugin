using QRCoder;

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace Graviton.Settings.UI
{
    internal static class QRCodeRenderer
    {
        public static BitmapImage Build(string logoFilePath, string data)
        {
            using (Bitmap logo = new Bitmap(logoFilePath))
            {
                using (var qrGenerator = new QRCodeGenerator())
                using (var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.H))
                using (var qrCode = new ArtQRCode(qrCodeData))
                {
                    var lightcolour = System.Drawing.Color.FromArgb(255, 250, 250, 245);
                    var darkcolour = System.Drawing.Color.FromArgb(255, 20, 20, 30);

                    Bitmap qrBitmap = qrCode.GetGraphic(
                                                    pixelsPerModule: 20,
                                                    darkColor: lightcolour,
                                                    lightColor: darkcolour,
                                                    backgroundColor: System.Drawing.Color.Transparent,
                                                    pixelSizeFactor: 0.7,
                                                    drawQuietZones: false
                                                    );

                    const int padding = 20;
                    Bitmap canvas = new Bitmap(qrBitmap.Width + padding * 2, qrBitmap.Height + padding * 2, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    using (Graphics g = Graphics.FromImage(canvas))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = PixelOffsetMode.Half;

                        g.DrawImage(qrBitmap, padding, padding);

                        int finderSize = 7 * 20;
                        using (var coverPath = new GraphicsPath())
                        {
                            coverPath.AddRectangle(new RectangleF(padding, padding, finderSize, finderSize));
                            coverPath.AddRectangle(new RectangleF(canvas.Width - padding - finderSize, padding, finderSize, finderSize));
                            coverPath.AddRectangle(new RectangleF(padding, canvas.Height - padding - finderSize, finderSize, finderSize));

                            GraphicsState clearState = g.Save();
                            g.SetClip(coverPath);
                            g.Clear(System.Drawing.Color.Transparent);
                            g.Restore(clearState);
                        }

                        DrawFinder(g, 20, 20, 20, lightcolour);
                        DrawFinder(g, canvas.Width - 20 - finderSize, 20, 20, lightcolour);
                        DrawFinder(g, 20, canvas.Height - 20 - finderSize, 20, lightcolour);

                        int iconSize = (int)(canvas.Width * 0.20);
                        int iconX = (canvas.Width - iconSize) / 2;
                        int iconY = (canvas.Height - iconSize) / 2;

                        const int badgePadding = 20;
                        const int badgeRadius = 20;

                        RectangleF badgeRect = new RectangleF(iconX - badgePadding, iconY - badgePadding, iconSize + badgePadding * 2, iconSize + badgePadding * 2);
                        using (var badgePath = RoundedRect(badgeRect, badgeRadius))
                        {
                            GraphicsState state = g.Save();
                            g.SetClip(badgePath);
                            g.Clear(System.Drawing.Color.Transparent);
                            g.Restore(state);

                            using (GraphicsPath logoPath = RoundedRect(new RectangleF(iconX, iconY, iconSize, iconSize), 16))
                            {
                                state = g.Save();
                                g.SetClip(logoPath);
                                g.DrawImage(logo, iconX, iconY, iconSize, iconSize);
                                g.Restore(state);
                            }
                        }
                    }

                    qrBitmap.Dispose();
                    qrBitmap = canvas;

                    using (MemoryStream memory = new MemoryStream())
                    {
                        qrBitmap.Save(memory, ImageFormat.Png);
                        memory.Position = 0;

                        BitmapImage bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = memory;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        qrBitmap.Dispose();
                        return bitmapImage;
                    }
                }
            }

        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();

            float d = radius * 2;

            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            if (d > rect.Width) d = rect.Width;
            if (d > rect.Height) d = rect.Height;

            radius = d / 2;

            path.StartFigure();
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        private static void DrawFinder(Graphics g, int x, int y, int pixelsPerModule, Color light)
        {
            int size = pixelsPerModule * 7;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var lightBrush = new SolidBrush(light))
            {
                using (var outer = new GraphicsPath())
                using (var middleHole = new GraphicsPath())
                {
                    using (var outerShape = RoundedRect(new RectangleF(x, y, size, size), pixelsPerModule * 1.6f))
                    {
                        outer.AddPath(outerShape, false);
                    }

                    int border = (int)(pixelsPerModule * 0.6f);
                    using (var middleHoleShape = RoundedRect(new RectangleF(x + border, y + border, size - border * 2, size - border * 2), pixelsPerModule * 1.2f))
                    {
                        middleHole.AddPath(middleHoleShape, false);
                    }

                    using (var region = new System.Drawing.Region(outer))
                    {
                        region.Exclude(middleHole);
                        g.FillRegion(lightBrush, region);
                    }
                }

                int centre = pixelsPerModule * 2;
                using (var centerPath = RoundedRect(new RectangleF(x + centre, y + centre, size - centre * 2, size - centre * 2), pixelsPerModule * 0.8f))
                {
                    g.FillPath(lightBrush, centerPath);
                }
            }

        }
    }
}
