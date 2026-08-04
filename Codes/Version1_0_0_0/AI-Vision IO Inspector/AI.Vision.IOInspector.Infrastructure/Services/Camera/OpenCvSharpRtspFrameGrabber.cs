using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// VLAD 산출물의 OpenCvSharp 런타임을 선택적으로 사용해 RTSP 프레임 1장을 저장합니다.
    /// VLAD에 포함된 OpenCvSharp가 .NET Framework 전용이면 .NET 9 앱에서는 자동으로 사용하지 않습니다.
    /// </summary>
    internal class OpenCvSharpRtspFrameGrabber
    {
        private readonly string _nativeDirectory;
        private readonly object _syncLock;
        private Assembly _openCvAssembly;
        private Type _videoCaptureType;
        private Type _matType;
        private Type _cv2Type;
        private bool _isLoaded;
        private bool _compatibilityChecked;
        private bool _isManagedAssemblyCompatible;
        private string _compatibilityMessage;

        public OpenCvSharpRtspFrameGrabber(string rootPath)
        {
            string projectRoot = ProjectDataRootResolver.Resolve(rootPath);
            _nativeDirectory = Path.Combine(projectRoot, "RuntimeData", "Native", "OpenCvSharp", "x64");
            _syncLock = new object();
            _compatibilityMessage = string.Empty;
        }

        public bool IsAvailable()
        {
            if (!File.Exists(Path.Combine(_nativeDirectory, "OpenCvSharp.dll")))
            {
                _compatibilityMessage = "OpenCvSharp.dll이 없습니다.";
                return false;
            }

            if (!File.Exists(Path.Combine(_nativeDirectory, "OpenCvSharpExtern.dll"))
                || !File.Exists(Path.Combine(_nativeDirectory, "opencv_world453.dll")))
            {
                _compatibilityMessage = "OpenCvSharp 네이티브 DLL이 없습니다.";
                return false;
            }

            return IsManagedAssemblyCompatible();
        }

        public string BuildMissingRuntimeMessage()
        {
            if (!string.IsNullOrWhiteSpace(_compatibilityMessage))
            {
                return _compatibilityMessage;
            }

            return "OpenCvSharp RTSP 런타임을 찾을 수 없습니다. " + _nativeDirectory + " 폴더를 확인하세요.";
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

            object capture = null;
            object frame = null;
            try
            {
                capture = CreateVideoCapture(rtspUrl);
                if (!ReadBooleanMember(capture, "IsOpened"))
                {
                    throw new InvalidOperationException(displayName + " RTSP 스트림을 열지 못했습니다.");
                }

                frame = Activator.CreateInstance(_matType);
                MethodInfo readMethod = _videoCaptureType.GetMethod("Read", new Type[] { _matType });
                if (readMethod == null)
                {
                    throw new MissingMethodException("OpenCvSharp.VideoCapture.Read(Mat)을 찾을 수 없습니다.");
                }

                object readResult = readMethod.Invoke(capture, new object[] { frame });
                if (!ConvertToBoolean(readResult))
                {
                    throw new InvalidOperationException(displayName + " RTSP 프레임 읽기에 실패했습니다.");
                }

                if (ReadBooleanMember(frame, "Empty"))
                {
                    throw new InvalidOperationException(displayName + " RTSP 프레임이 비어 있습니다.");
                }

                string outputDirectory = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                SaveFrame(outputFilePath, frame);
                if (!File.Exists(outputFilePath))
                {
                    throw new InvalidOperationException(displayName + " RTSP 프레임 파일이 생성되지 않았습니다.");
                }
            }
            catch (TargetInvocationException ex)
            {
                Exception innerException = ex.InnerException == null ? ex : ex.InnerException;
                throw new InvalidOperationException(displayName + " OpenCvSharp RTSP 캡처 실패: " + innerException.Message, innerException);
            }
            finally
            {
                DisposeObject(frame);
                DisposeObject(capture);
            }
        }

        private bool IsManagedAssemblyCompatible()
        {
            if (_compatibilityChecked)
            {
                return _isManagedAssemblyCompatible;
            }

            _compatibilityChecked = true;
            _isManagedAssemblyCompatible = false;

            try
            {
                string assemblyPath = Path.Combine(_nativeDirectory, "OpenCvSharp.dll");
                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                AssemblyName[] references = assembly.GetReferencedAssemblies();
                foreach (AssemblyName reference in references)
                {
                    if (string.Equals(reference.Name, "System.Web", StringComparison.OrdinalIgnoreCase))
                    {
                        _compatibilityMessage = "VLAD OpenCvSharp.dll은 System.Web을 참조하는 .NET Framework 전용 DLL이라 .NET 9에서 사용할 수 없습니다.";
                        return false;
                    }
                }

                _isManagedAssemblyCompatible = true;
                return true;
            }
            catch (Exception ex)
            {
                _compatibilityMessage = "OpenCvSharp.dll 호환성 확인 실패: " + ex.Message;
                return false;
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
                ApplyOpenCvEnvironment();

                string assemblyPath = Path.Combine(_nativeDirectory, "OpenCvSharp.dll");
                _openCvAssembly = Assembly.LoadFrom(assemblyPath);
                _videoCaptureType = _openCvAssembly.GetType("OpenCvSharp.VideoCapture", true);
                _matType = _openCvAssembly.GetType("OpenCvSharp.Mat", true);
                _cv2Type = _openCvAssembly.GetType("OpenCvSharp.Cv2", true);
                _isLoaded = true;
            }
        }

        private void ApplyNativeSearchPath()
        {
            string pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                Environment.SetEnvironmentVariable("PATH", _nativeDirectory);
            }
            else if (pathValue.IndexOf(_nativeDirectory, StringComparison.OrdinalIgnoreCase) < 0)
            {
                Environment.SetEnvironmentVariable("PATH", _nativeDirectory + Path.PathSeparator + pathValue);
            }

            SetDllDirectory(_nativeDirectory);
        }

        private void ApplyOpenCvEnvironment()
        {
            string options = Environment.GetEnvironmentVariable("OPENCV_FFMPEG_CAPTURE_OPTIONS");
            if (string.IsNullOrWhiteSpace(options))
            {
                // IDIS/NVR RTSP 수신은 UDP보다 TCP가 안정적이므로 기본 전송 방식을 TCP로 둡니다.
                Environment.SetEnvironmentVariable("OPENCV_FFMPEG_CAPTURE_OPTIONS", "rtsp_transport;tcp");
            }
        }

        private object CreateVideoCapture(string rtspUrl)
        {
            ConstructorInfo constructor = _videoCaptureType.GetConstructor(new Type[] { typeof(string) });
            if (constructor != null)
            {
                return constructor.Invoke(new object[] { rtspUrl });
            }

            object capture = Activator.CreateInstance(_videoCaptureType);
            MethodInfo openMethod = _videoCaptureType.GetMethod("Open", new Type[] { typeof(string) });
            if (openMethod == null)
            {
                throw new MissingMethodException("OpenCvSharp.VideoCapture(string) 또는 Open(string)을 찾을 수 없습니다.");
            }

            object openResult = openMethod.Invoke(capture, new object[] { rtspUrl });
            if (openResult is bool && !((bool)openResult))
            {
                DisposeObject(capture);
                throw new InvalidOperationException("OpenCvSharp.VideoCapture.Open(string)이 실패했습니다.");
            }

            return capture;
        }

        private void SaveFrame(string outputFilePath, object frame)
        {
            MethodInfo imWriteMethod = FindImWriteMethod();
            if (imWriteMethod == null)
            {
                throw new MissingMethodException("OpenCvSharp.Cv2.ImWrite(string, Mat)을 찾을 수 없습니다.");
            }

            ParameterInfo[] parameters = imWriteMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = outputFilePath;
            arguments[1] = frame;
            for (int index = 2; index < parameters.Length; index++)
            {
                arguments[index] = parameters[index].HasDefaultValue ? parameters[index].DefaultValue : null;
            }

            object saved = imWriteMethod.Invoke(null, arguments);
            if (saved is bool && !((bool)saved))
            {
                throw new InvalidOperationException("OpenCvSharp.Cv2.ImWrite가 실패했습니다.");
            }
        }

        private MethodInfo FindImWriteMethod()
        {
            MethodInfo[] methods = _cv2Type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (MethodInfo method in methods)
            {
                if (method.Name != "ImWrite")
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length >= 2
                    && parameters[0].ParameterType == typeof(string)
                    && parameters[1].ParameterType == _matType)
                {
                    return method;
                }
            }

            return null;
        }

        private bool ReadBooleanMember(object instance, string name)
        {
            if (instance == null)
            {
                return false;
            }

            Type instanceType = instance.GetType();
            MethodInfo method = instanceType.GetMethod(name, Type.EmptyTypes);
            if (method != null)
            {
                object value = method.Invoke(instance, null);
                return ConvertToBoolean(value);
            }

            PropertyInfo property = instanceType.GetProperty(name);
            if (property != null)
            {
                object value = property.GetValue(instance, null);
                return ConvertToBoolean(value);
            }

            throw new MissingMemberException(instanceType.FullName, name);
        }

        private bool ConvertToBoolean(object value)
        {
            if (value is bool)
            {
                return (bool)value;
            }

            return false;
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
                // 호환되지 않는 OpenCvSharp DisposeUnmanaged 예외가 UI로 전파되지 않도록 방어합니다.
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}
