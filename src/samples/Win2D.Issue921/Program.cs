// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct2D1.D2D1;
using static Vortice.DirectWrite.DWrite;

namespace Vortice.Windows.Discussion400;

public static class Program
{
    public static void Main()
    {
        using var app = new TestApplication();
        app.Run();
    }

    private class TestApplication : Application
    {
        private readonly ID2D1Factory7 _d2dFactory;
        private ID3D11Device5 D3Device;
        private ID3D11DeviceContext4 D3DeviceContext;
        private IDXGISwapChain4 SwapChain;
        private IDXGISurface2 BackBuffer;
        private ID2D1Bitmap1 D2RenderTarget;
        private ID2D1Device6 D2Device;
        private ID2D1DeviceContext6 D2DeviceContext;

        private Vortice.Mathematics.Color4 bgcolor = new(0.1f, 0.1f, 0.1f, 1.0f);
        private ID2D1SolidColorBrush OnscreenBrush;
        private ID2D1SolidColorBrush OffscreenBrush;
        private ID2D1BitmapRenderTarget OffscreenBuffer;
        private TextHightlightShader TextHightlightShader;

        public TestApplication()
            : base(false)
        {
            _d2dFactory = D2D1CreateFactory<ID2D1Factory7>();
            TextHightlightShader.Register(_d2dFactory);
        }

        public override void Dispose()
        {
            if (OnscreenBrush != null) { OnscreenBrush.Dispose(); }
            if (OffscreenBrush != null) { OffscreenBrush.Dispose(); }
            if (OffscreenBuffer != null) { OffscreenBuffer.Dispose(); }
            if (TextHightlightShader != null) { TextHightlightShader.Dispose(); }

            D3Device?.Dispose();
            D3DeviceContext?.Dispose();
            SwapChain?.Dispose();
            BackBuffer?.Dispose();
            D2RenderTarget?.Dispose();
            D2Device?.Dispose();
            D2DeviceContext?.Dispose();
            _d2dFactory.Dispose();
        }

        private void CreateResources()
        {
            if (OnscreenBrush != null) { OnscreenBrush.Dispose(); }
            if (OffscreenBrush != null) { OffscreenBrush.Dispose(); }
            if (OffscreenBuffer != null) { OffscreenBuffer.Dispose(); }
            if (TextHightlightShader != null) { TextHightlightShader.Dispose(); }

            D3Device?.Dispose();
            D3DeviceContext?.Dispose();
            SwapChain?.Dispose();
            BackBuffer?.Dispose();
            D2RenderTarget?.Dispose();
            D2Device?.Dispose();
            D2DeviceContext?.Dispose();

            // First, create the Direct3D device.
            D3D11.D3D11CreateDevice(
                IntPtr.Zero, // Specify nullptr to use the default adapter.
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport // This flag is required in order to enable compatibility with Direct2D.
                #if DEBUG
                    | DeviceCreationFlags.Debug // If the project is in a debug build, enable debugging via SDK Layers with this flag.
                #endif
                ,
                new[] { // This array defines the ordering of feature levels that D3D should attempt to create.
                    Vortice.Direct3D.FeatureLevel.Level_11_1,
                    Vortice.Direct3D.FeatureLevel.Level_11_0,
                    Vortice.Direct3D.FeatureLevel.Level_10_1,
                    Vortice.Direct3D.FeatureLevel.Level_10_0,
                    Vortice.Direct3D.FeatureLevel.Level_9_3,
                    Vortice.Direct3D.FeatureLevel.Level_9_1,
                },
                out var d3Device,
                out var d3DeviceContext
            );

            // Retrieve the Direct3D 11.1 interfaces.
            D3Device = d3Device.QueryInterface<ID3D11Device5>();
            D3DeviceContext = d3DeviceContext.QueryInterface<ID3D11DeviceContext4>();

            d3Device.Dispose();
            d3DeviceContext.Dispose();

            using (var dxgiDevice = D3Device.QueryInterface<IDXGIDevice4>())
            {
                D2Device = _d2dFactory.CreateDevice(dxgiDevice);
                D2DeviceContext = D2Device.CreateDeviceContext(DeviceContextOptions.None);
            }

            // If the swap chain already exists, resize it.
            if (SwapChain != null)
            {
                // Setting all values to 0 automatically chooses the width & height to match the client rect for HWNDs,
                // and preserves the existing buffer count and format.
                SwapChain.ResizeBuffers(0, 0, 0).CheckError();
            }
            else // If the swap chain does not exist, create it.
            {
                // First, retrieve the underlying DXGI Device from the D3D Device.
                // The swap must be created on the same adapter as the existing D3D Device.
                using var dxgiDevice = D3Device!.QueryInterface<IDXGIDevice4>();

                // Next, get the parent factory from the DXGI Device.
                using var dxgiAdapter = dxgiDevice.GetAdapter();
                using var dxgiFactory = dxgiAdapter.GetParent<IDXGIFactory7>();

                // Finally, create the swap chain.
                using var swapChain = dxgiFactory.CreateSwapChainForHwnd(
                    D3Device!,
                    MainWindow.Handle,
                    new SwapChainDescription1 {
                        Width = 0, // Use automatic sizing.
                        Height = 0,
                        Format = Format.B8G8R8A8_UNorm, // This is the most common swap chain format.
                        Stereo = false,
                        SampleDescription = new() {
                            Count = 1, // Don't use multi-sampling.
                            Quality = 0,
                        },
                        BufferUsage = Usage.RenderTargetOutput,
                        BufferCount = 2, // Use two buffers to enable flip effect.
                        Scaling = Scaling.Stretch,
                        SwapEffect = SwapEffect.FlipSequential, // MS recommends using this swap effect for all applications.
                        Flags = 0,
                    },
                    new SwapChainFullscreenDescription {
                        Windowed = true
                    },
                    null // allow on all displays
                );

                SwapChain = swapChain.QueryInterface<IDXGISwapChain4>();

                swapChain.Dispose();

                // Ensure that DXGI does not queue more than one frame at a time.
                // This both reduces latency and ensures that the application will only render
                // after each VSync, minimizing power consumption.
                dxgiDevice.SetMaximumFrameLatency(1);

                dxgiFactory.MakeWindowAssociation(MainWindow.Handle, WindowAssociationFlags.IgnoreAll);
            }

            // Get a D2D surface from the DXGI back buffer to use as the D2D render target.
            // Direct2D needs the dxgi version of the backbuffer surface pointer.
            BackBuffer = SwapChain.GetBuffer<IDXGISurface2>(0);

            // So now we can set the Direct2D render target.
            var dpi = 96f;

            D2DeviceContext!.SetDpi(dpi, dpi);
            D2DeviceContext!.Target =
            D2RenderTarget = D2DeviceContext!.CreateBitmapFromDxgiSurface(BackBuffer, new BitmapProperties1 {
                BitmapOptions = BitmapOptions.Target | BitmapOptions.CannotDraw,
                PixelFormat = new() {
                    Format = Format.B8G8R8A8_UNorm,
                    AlphaMode = Vortice.DCommon.AlphaMode.Premultiplied,
                },
                DpiX = dpi,
                DpiY = dpi,
            });

            OnscreenBrush = D2DeviceContext.CreateSolidColorBrush(new Color4(0.1f, 0.2f, 0.3f, 0.4f));
            OffscreenBrush = D2DeviceContext.CreateSolidColorBrush(new Color4(0.1f, 0.2f, 0.3f, 0.4f));
            OffscreenBuffer = D2DeviceContext.CreateCompatibleRenderTarget(
                desiredSize: new SizeF(64, 64),
                desiredPixelSize: new Size(64, 64),
                desiredFormat: D2DeviceContext.PixelFormat,
                options: CompatibleRenderTargetOptions.None
            );
            TextHightlightShader = new(D2DeviceContext);
            TextHightlightShader.SetInput(0, OffscreenBuffer.Bitmap, invalidate: true);
        }

        protected override void InitializeBeforeRun()
        {
        }

        protected override void OnKeyboardEvent(KeyboardKey key, bool pressed)
        {
        }

        bool DeviceIsLost = true;

        protected override void OnDraw(int width, int height)
        {
            if (DeviceIsLost) {
                CreateResources();
                DeviceIsLost = false;
            }

            D2DeviceContext.BeginDraw();
            D2DeviceContext.Clear(bgcolor);

            var i = 0;

            ++i;
            D2DeviceContext.FillRectangle(new RectangleF(8*i, 8*i, 64, 64), OnscreenBrush);   // The first rectangle is a normal draw.

            OffscreenBuffer.BeginDraw();
            OffscreenBuffer.Clear(Colors.Transparent);
            OffscreenBuffer.FillRectangle(new RectangleF(0, 0, 64, 64), OffscreenBrush); // The second rectangle is a draw from an offscreen canvas.
            OffscreenBuffer.EndDraw();

            ++i;
            D2DeviceContext.DrawBitmap(                                                  // The second rectangle is a draw from an offscreen canvas.
                bitmap: OffscreenBuffer.Bitmap,
                opacity: 1,
                interpolationMode: BitmapInterpolationMode.Linear,
                sourceRectangle: new RectangleF(0, 0, 64, 64),
                destinationRectangle: new RectangleF(-8*(i-2) + 64*(i-1), 8*i, 64, 64)
            );

            ++i;
            D2DeviceContext.DrawImage(                                                   // The third rectangle is a draw from an offscreen canvas.
                image: OffscreenBuffer.Bitmap,                                           // Note this one is using DrawImage() instead of DrawBitmap().
                compositeMode: CompositeMode.SourceOver,
                interpolationMode: Direct2D1.InterpolationMode.Linear,
                targetOffset: new Vector2(-8*(i-2) + 64*(i-1), 8*i)
            );

            ++i;
            D2DeviceContext.DrawImage(                                                   // The fourth rectangle is a draw from the shader.
                image: TextHightlightShader.Output,
                compositeMode: CompositeMode.SourceOver,
                interpolationMode: Direct2D1.InterpolationMode.Linear,
                targetOffset: new Vector2(-8*(i-2) + 64*(i-1), 8*i)
            );

            var drawResult = D2DeviceContext.EndDraw();

            // Present the rendered image to the window.
            // Because the maximum frame latency is set to 1,
            // the render loop will generally be throttled to the screen refresh rate,
            // typically around 60Hz, by sleeping the application on Present
            // until the screen is refreshed.
            var presentResult = SwapChain.Present(1, 0);

            if (
                   drawResult == Vortice.Direct2D1.ResultCode.RecreateTarget
                || presentResult == Vortice.DXGI.ResultCode.DeviceRemoved
                || presentResult == Vortice.DXGI.ResultCode.DeviceReset
            )
            {
                DeviceIsLost = true;
            }
            else
            {
                drawResult.CheckError();
                presentResult.CheckError();
            }
        }
    }
}
