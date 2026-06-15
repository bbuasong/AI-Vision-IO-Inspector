using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// VLAD 산출물에 포함된 LibVLCSharp/LibVLC 런타임으로 RTSP 프레임 1장을 스냅샷 저장합니다.
    /// OpenCvSharp보다 .NET 9 호환성이 안정적인 RTSP 대체 경로입니다.
    /// </summary>
    internal class VlcRtspFrameGrabber
    {
        private const int PlayWaitMilliseconds = 5000;
        private const int SnapshotWaitMilliseconds = 5000;

        private readonly string _baseDirectory;
        private readonly string _nativeDirectory;
        private readonly string _pluginDirectory;
        private readonly object _syncLock;
        private Assembly _libVlcSharpAssembly;
        private Type _coreType;
        private Type _libVlcType;
        private Type _mediaType;
        private Type _mediaPlayerType;
        private Type _fromTypeType;
        private bool _isLoaded;
        private bool _assemblyResolverAttached;

        public VlcRtspFrameGrabber(string rootPath)
        {
            string projectRoot = ProjectDataRootResolver.Resolve(rootPath);
            string vladDirectory = Path.Combine(projectRoot, "Native", "VLAD");
            string runtimeDataDirectory = Path.Combine(projectRoot, "RuntimeData", "Native", "LibVLC");

            if (IsLibVlcRuntimeAvailable(vladDirectory, vladDirectory, Path.Combine(vladDirectory, "plugins")))
            {
                _baseDirectory = vladDirectory;
                _nativeDirectory = vladDirectory;
                _pluginDirectory = Path.Combine(vladDirectory, "plugins");
            }
            else
            {
                _baseDirectory = runtimeDataDirectory;
                _nativeDirectory = Path.Combine(runtimeDataDirectory, "win-x64");
                _pluginDirectory = Path.Combine(_nativeDirectory, "plugins");
            }

            _syncLock = new object();
        }

        public bool IsAvailable()
        {
            return IsLibVlcRuntimeAvailable(_baseDirectory, _nativeDirectory, _pluginDirectory);
        }

        public string BuildMissingRuntimeMessage()
        {
            return "LibVLC RTSP 런타임을 찾을 수 없습니다. "
                   + "우선 경로는 Native\\VLAD이며, LibVLCSharp.dll, libvlc.dll, libvlccore.dll, plugins 폴더가 필요합니다. "
                   + "현재 확인한 경로: " + _baseDirectory;
        }

        public void CaptureFrame(string rtspUrl, string outputFilePath, string displayName)
        {
            if (string.IsNullOrWhiteSpace(rtspUrl))
            {
                throw new InvalidOperationException(displayName + " RTSP URL이 비어 있습니다.");
            }

            if (!IsAvailable())
            {
                throw new FileNotFoundException(BuildMissingRuntimeMessage());
            }

            EnsureLoaded();

            object libVlc = null;
            object media = null;
            object mediaPlayer = null;
            try
            {
                libVlc = CreateLibVlc();
                media = CreateMedia(libVlc, rtspUrl);
                AddMediaOption(media, ":rtsp-tcp");
                AddMediaOption(media, ":network-caching=100");
                AddMediaOption(media, ":live-caching=100");
                AddMediaOption(media, ":clock-jitter=0");
                AddMediaOption(media, ":clock-synchro=0");
                AddMediaOption(media, ":drop-late-frames");
                AddMediaOption(media, ":skip-frames");
                AddMediaOption(media, ":no-audio");

                mediaPlayer = CreateMediaPlayer(libVlc);
                if (!Play(mediaPlayer, media))
                {
                    throw new InvalidOperationException(displayName + " LibVLC 재생 시작에 실패했습니다.");
                }

                if (!WaitUntilPlaying(mediaPlayer, PlayWaitMilliseconds))
                {
                    throw new TimeoutException(displayName + " LibVLC RTSP 재생 대기 시간이 초과되었습니다.");
                }

                Thread.Sleep(1200);
                string outputDirectory = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                if (!TakeSnapshot(mediaPlayer, outputFilePath))
                {
                    throw new InvalidOperationException(displayName + " LibVLC 스냅샷 저장에 실패했습니다.");
                }

                if (!WaitUntilFileCreated(outputFilePath, SnapshotWaitMilliseconds))
                {
                    throw new TimeoutException(displayName + " LibVLC 스냅샷 파일 생성 시간이 초과되었습니다.");
                }
            }
            catch (TargetInvocationException ex)
            {
                Exception innerException = ex.InnerException == null ? ex : ex.InnerException;
                throw new InvalidOperationException(displayName + " LibVLC RTSP 캡처 실패: " + innerException.Message, innerException);
            }
            finally
            {
                StopPlayer(mediaPlayer);
                DisposeObject(mediaPlayer);
                DisposeObject(media);
                DisposeObject(libVlc);
            }
        }

        private void EnsureLoaded()
        {
            if (_isLoaded)
            {
                return;
            }

            lock (_syncLock)
            {
                if (_isLoaded)
                {
                    return;
                }

                ApplyNativeSearchPath();
                AttachAssemblyResolver();

                _libVlcSharpAssembly = Assembly.LoadFrom(Path.Combine(_baseDirectory, "LibVLCSharp.dll"));
                _coreType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.Core", true);
                _libVlcType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.LibVLC", true);
                _mediaType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.Media", true);
                _mediaPlayerType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.MediaPlayer", true);
                _fromTypeType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.FromType", true);
                InitializeCore();
                _isLoaded = true;
            }
        }

        private void ApplyNativeSearchPath()
        {
            PrependProcessPath(_baseDirectory);
            PrependProcessPath(_nativeDirectory);
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", _pluginDirectory);
            SetDllDirectory(_nativeDirectory);
        }

        private void PrependProcessPath(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            string pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                Environment.SetEnvironmentVariable("PATH", directoryPath);
                return;
            }

            string[] pathItems = pathValue.Split(Path.PathSeparator);
            foreach (string pathItem in pathItems)
            {
                if (string.Equals(pathItem.TrimEnd(Path.DirectorySeparatorChar), directoryPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            Environment.SetEnvironmentVariable("PATH", directoryPath + Path.PathSeparator + pathValue);
        }

        private void AttachAssemblyResolver()
        {
            if (_assemblyResolverAttached)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += ResolveManagedAssembly;
            _assemblyResolverAttached = true;
        }

        private Assembly ResolveManagedAssembly(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name + ".dll";
            string candidatePath = Path.Combine(_baseDirectory, assemblyName);
            if (File.Exists(candidatePath))
            {
                return Assembly.LoadFrom(candidatePath);
            }

            return null;
        }

        private void InitializeCore()
        {
            MethodInfo initializeMethod = _coreType.GetMethod("Initialize", new Type[] { typeof(string) });
            if (initializeMethod == null)
            {
                throw new MissingMethodException("LibVLCSharp.Shared.Core.Initialize(string)을 찾을 수 없습니다.");
            }

            initializeMethod.Invoke(null, new object[] { _nativeDirectory });
        }

        private object CreateLibVlc()
        {
            ConstructorInfo constructor = _libVlcType.GetConstructor(new Type[] { typeof(string[]) });
            if (constructor == null)
            {
                throw new MissingMethodException("LibVLC(string[]) 생성자를 찾을 수 없습니다.");
            }

            string[] options = new string[]
            {
                "--no-video-title-show",
                "--rtsp-tcp",
                "--network-caching=100",
                "--live-caching=100",
                "--clock-jitter=0",
                "--clock-synchro=0",
                "--drop-late-frames",
                "--skip-frames",
                "--no-audio"
            };
            return constructor.Invoke(new object[] { options });
        }

        private object CreateMedia(object libVlc, string rtspUrl)
        {
            ConstructorInfo constructor = _mediaType.GetConstructor(new Type[] { _libVlcType, typeof(string), _fromTypeType, typeof(string[]) });
            if (constructor == null)
            {
                throw new MissingMethodException("Media(LibVLC, string, FromType, string[]) 생성자를 찾을 수 없습니다.");
            }

            object fromLocation = Enum.Parse(_fromTypeType, "FromLocation");
            return constructor.Invoke(new object[] { libVlc, rtspUrl, fromLocation, new string[0] });
        }

        private void AddMediaOption(object media, string option)
        {
            MethodInfo addOptionMethod = _mediaType.GetMethod("AddOption", new Type[] { typeof(string) });
            if (addOptionMethod != null)
            {
                addOptionMethod.Invoke(media, new object[] { option });
            }
        }

        private object CreateMediaPlayer(object libVlc)
        {
            ConstructorInfo constructor = _mediaPlayerType.GetConstructor(new Type[] { _libVlcType });
            if (constructor == null)
            {
                throw new MissingMethodException("MediaPlayer(LibVLC) 생성자를 찾을 수 없습니다.");
            }

            return constructor.Invoke(new object[] { libVlc });
        }

        private bool Play(object mediaPlayer, object media)
        {
            MethodInfo playMethod = _mediaPlayerType.GetMethod("Play", new Type[] { _mediaType });
            if (playMethod == null)
            {
                throw new MissingMethodException("MediaPlayer.Play(Media)를 찾을 수 없습니다.");
            }

            object result = playMethod.Invoke(mediaPlayer, new object[] { media });
            return result is bool && (bool)result;
        }

        private bool WaitUntilPlaying(object mediaPlayer, int timeoutMilliseconds)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.Now < deadline)
            {
                if (ReadBooleanProperty(mediaPlayer, "IsPlaying"))
                {
                    return true;
                }

                Thread.Sleep(100);
            }

            return false;
        }

        private bool TakeSnapshot(object mediaPlayer, string outputFilePath)
        {
            MethodInfo snapshotMethod = _mediaPlayerType.GetMethod("TakeSnapshot", new Type[] { typeof(uint), typeof(string), typeof(uint), typeof(uint) });
            if (snapshotMethod == null)
            {
                throw new MissingMethodException("MediaPlayer.TakeSnapshot(uint, string, uint, uint)를 찾을 수 없습니다.");
            }

            object result = snapshotMethod.Invoke(mediaPlayer, new object[] { 0u, outputFilePath, 0u, 0u });
            return result is bool && (bool)result;
        }

        private bool WaitUntilFileCreated(string filePath, int timeoutMilliseconds)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMilliseconds);
            while (DateTime.Now < deadline)
            {
                if (File.Exists(filePath))
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > 0)
                    {
                        return true;
                    }
                }

                Thread.Sleep(100);
            }

            return false;
        }

        private bool ReadBooleanProperty(object instance, string propertyName)
        {
            PropertyInfo property = _mediaPlayerType.GetProperty(propertyName);
            if (property == null)
            {
                throw new MissingMemberException(_mediaPlayerType.FullName, propertyName);
            }

            object value = property.GetValue(instance, null);
            return value is bool && (bool)value;
        }

        private void StopPlayer(object mediaPlayer)
        {
            if (mediaPlayer == null)
            {
                return;
            }

            try
            {
                MethodInfo stopMethod = _mediaPlayerType.GetMethod("Stop", Type.EmptyTypes);
                if (stopMethod != null)
                {
                    stopMethod.Invoke(mediaPlayer, null);
                }
            }
            catch
            {
            }
        }

        private void DisposeObject(object instance)
        {
            IDisposable disposable = instance as IDisposable;
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }

        private static bool IsLibVlcRuntimeAvailable(string baseDirectory, string nativeDirectory, string pluginDirectory)
        {
            return File.Exists(Path.Combine(baseDirectory, "LibVLCSharp.dll"))
                   && File.Exists(Path.Combine(nativeDirectory, "libvlc.dll"))
                   && File.Exists(Path.Combine(nativeDirectory, "libvlccore.dll"))
                   && Directory.Exists(pluginDirectory);
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}
