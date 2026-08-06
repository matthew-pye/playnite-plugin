using Graviton.Models.Notifications;

using Playnite;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Graviton.Saves
{
    record BufferedFrame(DateTime Timestamp, byte[] Screenshot);

    public class ScreenshotService
    {
        #region Win32 Imports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        private const uint PW_CLIENTONLY = 0x00000001;
        private const uint PW_RENDERFULLCONTENT = 0x00000002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public readonly int Width => Right - Left;
            public readonly int Height => Bottom - Top;
        }
        #endregion

        private readonly ImageCodecInfo JpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        private readonly EncoderParameters JpegEncoderParams = new(1)
        {
            Param = { [0] = new EncoderParameter(Encoder.Quality, 85L) }
        };

        private bool IsSetup = false;
        private int MaxFrames;
        private int FrameCaptureInterval;

        private IntPtr WindowHandle;
        private ConcurrentQueue<BufferedFrame> Frames = new();
        private readonly object CaptureLock = new();
        private Timer? CaptureTimer;

        public async Task<bool> Setup(int processID, int maxFramesCaptured, int intervalBetweenFrameCaptures = 1000)
        {
            // Wait for emulator to start before trying to setup capture
            await Task.Delay(2000);

            try
            {
                var windowHandle = await FindWindow(processID);
                if (windowHandle == IntPtr.Zero)
                    throw new Exception($"Failed to find window handle");

                WindowHandle = windowHandle;
                MaxFrames = maxFramesCaptured;
                FrameCaptureInterval = intervalBetweenFrameCaptures;
                IsSetup = true;
                return true;
            }
            catch (Exception ex)
            {
                GravitonPlugin.Logger.Error($"Failed to setup window capture!\n{ex}");
                return false;
            }
        }

        public Task Start()
        {
            if (!IsSetup || WindowHandle == IntPtr.Zero)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.screencap.notsetup", Loc.GetString("ScreenCaptureNotSetup"), GravitonSeverity.Warn));
                return Task.CompletedTask;
            }

            CaptureTimer = new Timer(Tick, null, FrameCaptureInterval, Timeout.Infinite);
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            IsSetup = false;
            CaptureTimer?.Dispose();
            CaptureTimer = null;
            return Task.CompletedTask;
        }

        public byte[]? GetScreenshotFromSecondsAgo(int seconds)
        {
            var target = DateTime.UtcNow.AddSeconds(-seconds);
            BufferedFrame? closestFrame = null;
            var minDifference = TimeSpan.MaxValue;

            foreach (var frame in Frames)
            {
                var diff = (frame.Timestamp - target).Duration();
                if (diff > TimeSpan.FromMilliseconds(FrameCaptureInterval) || diff < TimeSpan.FromMilliseconds(-FrameCaptureInterval))
                    continue;

                if (diff < minDifference)
                {
                    minDifference = diff;
                    closestFrame = frame;
                }
            }

            if (closestFrame == null)
            {
                GravitonPlugin.Logger.Error("Failed to find screenshot close to the requested time");
                return null;
            }
            else
            {
                return closestFrame.Screenshot;
            }
        }

        private async Task<IntPtr> FindWindow(int processID)
        {
            // This may need changing to enumerating the processes top-level windows
            // As some emulators may not set the main window correctly
            Process process = Process.GetProcessById(processID);
            process.WaitForInputIdle();
            process.Refresh();

            return process.MainWindowHandle;
        }

        private void Tick(object? state)
        {
            try
            {
                lock (CaptureLock)
                {
                    CaptureFrame();
                }
            }
            catch (Exception ex)
            {
                GravitonPlugin.Logger.Error($"Failed to process captured frame!\n{ex}");
            }
            finally
            {
                
                try
                {
                    CaptureTimer?.Change(FrameCaptureInterval, Timeout.Infinite);
                }
                catch (Exception){}
                
            }
        }

        private void CaptureFrame()
        {
            if (!IsWindow(WindowHandle))
            {
                GravitonPlugin.Logger.Warn("Capture target window no longer exists; stopping capture.");
                _ = Stop();
                return;
            }

            // Check if window is minimized
            if (IsIconic(WindowHandle))
                return;
            
            if (!GetClientRect(WindowHandle, out var rect) || rect.Width <= 0 || rect.Height <= 0)
                return;

            var now = DateTime.UtcNow;

            var screenContext = GetDC(IntPtr.Zero);
            if (screenContext == IntPtr.Zero)
                return;

            var memoryContext = IntPtr.Zero;
            var bitmap = IntPtr.Zero;
            var oldBitmap = IntPtr.Zero;

            try
            {
                memoryContext = CreateCompatibleDC(screenContext);
                if (memoryContext == IntPtr.Zero)
                    return;

                bitmap = CreateCompatibleBitmap(screenContext, rect.Width, rect.Height);
                if (bitmap == IntPtr.Zero)
                    return;

                oldBitmap = SelectObject(memoryContext, bitmap);

                // Get image from window
                if (!PrintWindow(WindowHandle, memoryContext, PW_CLIENTONLY | PW_RENDERFULLCONTENT))
                    return;

                using var captured = Image.FromHbitmap(bitmap);

                int targetHeight;
                int targetWidth;
                bool skipShrink = false;

                // Resize image to selected max resolution
                if (rect.Width < rect.Height)
                {
                    targetWidth = Math.Min((int)GravitonPlugin.Instance.Settings.ScreenshotResolution, rect.Width);
                    targetHeight = (int)Math.Round(rect.Height * (targetWidth / (double)rect.Width));

                    if (targetWidth == rect.Width)
                    {
                        skipShrink = true;
                    }
                }
                else
                {
                    targetHeight = Math.Min((int)GravitonPlugin.Instance.Settings.ScreenshotResolution, rect.Height);
                    targetWidth = (int)Math.Round(rect.Width * (targetHeight / (double)rect.Height));

                    if (targetHeight == rect.Height)
                    {
                        skipShrink = true;
                    }
                }

                using var ms = new MemoryStream();

                // Skip shrink if image is already lower res than max resolution
                if (skipShrink)
                {
                    captured.Save(ms, JpegEncoder, JpegEncoderParams);
                }
                else
                {
                    using var small = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppRgb);
                    using (var g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                        g.DrawImage(captured, 0, 0, targetWidth, targetHeight);
                    }
                    small.Save(ms, JpegEncoder, JpegEncoderParams);
                }

                Frames.Enqueue(new(now, ms.ToArray()));

                while (Frames.Count > MaxFrames)
                    Frames.TryDequeue(out _);
            }
            finally
            {
                if (memoryContext != IntPtr.Zero && oldBitmap != IntPtr.Zero)
                    SelectObject(memoryContext, oldBitmap);

                if (bitmap != IntPtr.Zero)
                    DeleteObject(bitmap);

                if (memoryContext != IntPtr.Zero)
                    DeleteDC(memoryContext);

                ReleaseDC(IntPtr.Zero, screenContext);
            }
        }
    }
}
