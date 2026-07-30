using Graviton.Models.Notifications;

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace Graviton.Saves
{
    record BufferedFrame(DateTime Timestamp, byte[] Screenshot);

    public enum ScreenshotResolution
    {
        P720 = 720,
        P1080 = 1080,
        P1440 = 1440,
        UHD4K = 2160
    }

    internal class ScreenshotService
    {
        #region COM Imports
        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
            IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
        }

        private Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        [DllImport("d3d11.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        [ComImport]
        [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDirect3DDxgiInterfaceAccess { IntPtr GetInterface([In] ref Guid iid); }
        #endregion

        private static readonly ImageCodecInfo JpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        private static readonly EncoderParameters JpegEncoderParams = new(1)
        {
            Param = { [0] = new EncoderParameter(Encoder.Quality, 85L) }
        };

        private bool IsSetup = false;
        private int MaxFrames => GravitonPlugin.Instance.Settings.SecondsBeforeSave;

        private GraphicsCaptureItem? Window;
        private ID3D11Device? D3D11Device;
        private Direct3D11CaptureFramePool? FramePool;
        private GraphicsCaptureSession? Session;
        private Queue<BufferedFrame> Frames = new();

        private DateTime NextFrameCapturedTime;

        public async Task Setup(int processID)
        {
            if (!GraphicsCaptureSession.IsSupported())
            {
                GravitonNotify.Add(new GravitonNotification("graviton.screencap.notsupported", "Cannot setup screenshot capture as this device doesn't support it!", GravitonSeverity.Warn));
                return;
            }
                
            // Wait for emulator to start before trying to setup capture
            await Task.Delay(2000);

            try
            {
                var windowHandle = await FindWindow(processID);
                if (windowHandle == IntPtr.Zero)
                {
                    
                    return;
                }

                var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
                var ptr = interop.CreateForWindow(windowHandle, GraphicsCaptureItemGuid);

                Window = GraphicsCaptureItem.FromAbi(ptr);

                D3D11Device = D3D11.D3D11CreateDevice(driverType: DriverType.Hardware, flags: DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport, FeatureLevel.Level_11_0);
                if (D3D11Device == null) 
                    throw new InvalidOperationException("Failed to create D3D11 device");

                var dxgiDevice = D3D11Device.QueryInterface<IDXGIDevice>();

                uint hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var graphicsDevice);
                if (hr != 0)
                    throw new Exception($"CreateDirect3D11DeviceFromDXGIDevice failed. HRESULT: {hr:X}");

                var device = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevice);
                Marshal.Release(graphicsDevice);

                FramePool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 3, Window.Size);
                Session = FramePool.CreateCaptureSession(Window);

                IsSetup = true;
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.screencap.setupfailed", "Failed to setup window capture!", GravitonSeverity.Warn, ex));
                return;
            }
        }

        public async Task Start()
        {
            if (!IsSetup || Window == null)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.screencap.notsetup", "Cannot start screenshot capture as setup wasn't completed!", GravitonSeverity.Warn));
                return;
            }

            FramePool?.FrameArrived += ProcessNewFrame;
            Session!.StartCapture();

        }

        public async Task Stop()
        {
            Session?.Dispose();
            FramePool?.FrameArrived -= ProcessNewFrame;
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

        private void ProcessNewFrame(Direct3D11CaptureFramePool sender, object args)
        {
            using var frame = sender.TryGetNextFrame();
            if (frame == null) 
                return;

            var now = DateTime.UtcNow;

            if (now >= NextFrameCapturedTime)
            {
                NextFrameCapturedTime = now.AddSeconds(1);

                var access = (IDirect3DDxgiInterfaceAccess)(object)frame.Surface;
                var iid = typeof(ID3D11Texture2D).GUID;
                var texturePtr = access.GetInterface(ref iid);

                using var sourceTexture = new ID3D11Texture2D(texturePtr);
                Marshal.Release(texturePtr);
                var desc = sourceTexture.Description with
                {
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CPUAccessFlags = CpuAccessFlags.Read,
                    MiscFlags = ResourceOptionFlags.None
                };
                using var copy = D3D11Device!.CreateTexture2D(desc);
                D3D11Device.ImmediateContext.CopyResource(copy, sourceTexture);

                var mapped = D3D11Device.ImmediateContext.Map(copy, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                try
                {
                    using var fullResView = new Bitmap((int)desc.Width, (int)desc.Height, (int)mapped.RowPitch, PixelFormat.Format32bppRgb, mapped.DataPointer);

                    int targetHeight;
                    int targetWidth;
                    bool skipShrink = false;

                    if (desc.Width < desc.Height)
                    {
                        targetWidth = (int)Math.Min((int)GravitonPlugin.Instance.Settings.ScreenshotResolution, desc.Width);
                        targetHeight = (int)Math.Round(desc.Height * (targetWidth / (double)desc.Width));

                        if (targetWidth == desc.Width)
                        {
                            skipShrink = true;
                        }
                    }
                    else
                    {
                        targetHeight = (int)Math.Min((int)GravitonPlugin.Instance.Settings.ScreenshotResolution, desc.Height);
                        targetWidth = (int)Math.Round(desc.Width * (targetHeight / (double)desc.Height));

                        if (targetHeight == desc.Height)
                        {
                            skipShrink = true;
                        }
                    }

                    using var ms = new MemoryStream();

                    if (skipShrink)
                    {
                        fullResView.Save(ms, JpegEncoder, JpegEncoderParams);
                    }
                    else
                    {
                        using var small = new Bitmap(targetWidth, targetHeight, PixelFormat.Format32bppRgb);
                        using (var g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                            g.DrawImage(fullResView, 0, 0, targetWidth, targetHeight);
                        }
                        small.Save(ms, JpegEncoder, JpegEncoderParams);
                    }

                    Frames.Enqueue(new(now, ms.ToArray()));
                }
                finally
                {
                    D3D11Device.ImmediateContext.Unmap(copy, 0);
                }

                while (Frames.Count > MaxFrames)
                    Frames.Dequeue();
            }
        }

    }
}
