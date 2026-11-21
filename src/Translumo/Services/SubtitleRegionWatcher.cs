using System;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using Translumo.Configuration;
using Translumo.Processing.Interfaces;

namespace Translumo.Services
{
    public class FrameChangedEventArgs : EventArgs
    {
        public byte[] FrameBytes { get; }

        public FrameChangedEventArgs(byte[] frameBytes)
        {
            FrameBytes = frameBytes;
        }
    }

    public class SubtitleRegionWatcher : IDisposable
    {
        public event EventHandler RegionBecameEmpty;
        public event EventHandler<FrameChangedEventArgs> RegionChangedWithText;
        public event EventHandler RegionChanged;

        private readonly IScreenCapturer _capturer;
        private readonly ChatWindowConfiguration _configuration;
        private CancellationTokenSource _cts;
        private Task _watchTask;

        private Mat _prevMat;
        private Mat _currMat;
        private Mat _diffMat;
        private Mat _edgeMat;
        private readonly Size _scaledSize = new Size(160, 40);
        private int _consecutiveEmptyFrames;

        private const double MIN_EDGE_ENERGY_THRESHOLD = 5.0;

        public SubtitleRegionWatcher(IScreenCapturer capturer, ChatWindowConfiguration configuration)
        {
            _capturer = capturer;
            _configuration = configuration;
        }

        public void Start()
        {
            if (_watchTask != null && !_watchTask.IsCompleted)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _watchTask = Task.Run(() => WatchLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _watchTask?.Wait(500);
            }
            catch { }

            _cts?.Dispose();
            _cts = null;
            _watchTask = null;
        }

        private void WatchLoop(CancellationToken token)
        {
            _prevMat = new Mat(_scaledSize, MatType.CV_8UC1);
            _currMat = new Mat(_scaledSize, MatType.CV_8UC1);
            _diffMat = new Mat(_scaledSize, MatType.CV_8UC1);
            _edgeMat = new Mat(_scaledSize, MatType.CV_8UC1);

            bool isFirstFrame = true;

            while (!token.IsCancellationRequested)
            {
                int interval = Math.Max(20, _configuration?.WatchIntervalMs ?? 80);
                try
                {
                    Thread.Sleep(interval);
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    byte[] screenBytes = null;
                    try
                    {
                        screenBytes = _capturer.CaptureScreen();
                    }
                    catch
                    {
                        continue;
                    }

                    if (screenBytes == null || screenBytes.Length == 0)
                    {
                        continue;
                    }

                    using (var rawMat = Cv2.ImDecode(screenBytes, ImreadModes.Grayscale))
                    {
                        if (rawMat.Empty())
                        {
                            continue;
                        }

                        Cv2.Resize(rawMat, _currMat, _scaledSize, 0, 0, InterpolationFlags.Area);
                    }

                    // 1. Text-presence calculation via edge gradient
                    Cv2.Sobel(_currMat, _edgeMat, MatType.CV_8U, 1, 0, 3);
                    var edgeMean = Cv2.Mean(_edgeMat).Val0;
                    bool hasText = edgeMean >= MIN_EDGE_ENERGY_THRESHOLD;

                    if (!hasText)
                    {
                        _consecutiveEmptyFrames++;
                        int requiredEmptyFrames = Math.Max(1, _configuration?.EmptyConfirmFrames ?? 2);
                        if (_consecutiveEmptyFrames >= requiredEmptyFrames)
                        {
                            RegionBecameEmpty?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    else
                    {
                        _consecutiveEmptyFrames = 0;

                        // 2. Change detection with text present
                        if (!isFirstFrame)
                        {
                            Cv2.Absdiff(_currMat, _prevMat, _diffMat);
                            double changeScore = Cv2.Mean(_diffMat).Val0;
                            double changeThreshold = _configuration?.ChangeThreshold ?? 5.0;

                            if (changeScore >= changeThreshold)
                            {
                                RegionChanged?.Invoke(this, EventArgs.Empty);
                                RegionChangedWithText?.Invoke(this, new FrameChangedEventArgs(screenBytes));
                            }
                        }
                    }

                    _currMat.CopyTo(_prevMat);
                    isFirstFrame = false;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Fail safely to keep loop running
                }
            }

            _prevMat?.Dispose();
            _currMat?.Dispose();
            _diffMat?.Dispose();
            _edgeMat?.Dispose();
            _prevMat = null;
            _currMat = null;
            _diffMat = null;
            _edgeMat = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
