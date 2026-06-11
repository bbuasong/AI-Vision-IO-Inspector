using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    public class VladSdkSession
    {
        private readonly object _syncRoot = new object();
        private IntPtr _vladId;

        public IntPtr CurrentVladId
        {
            get { return _vladId; }
        }

        public IntPtr EnsureStarted(int user, string rootName, string siteName, int messageVersion, int majorVersion, string modelPath, int gpuId)
        {
            lock (_syncRoot)
            {
                if (_vladId != IntPtr.Zero)
                {
                    return _vladId;
                }

                _vladId = VLAD_Ops_Ai.VLAD_Ops_Ai_Env_Start(user, rootName, siteName, messageVersion, majorVersion, modelPath, gpuId);

                if (_vladId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("VLAD SDK 초기화 실패: vladId가 비어 있습니다.");
                }

                return _vladId;
            }
        }
    }
}
