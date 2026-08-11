using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace EpsonScanApi.Services;

public class ScannerError(string msg) : Exception(msg);

[SupportedOSPlatform("windows")]
public static class ScannerWia
{
    // WIA format GUIDs
    private const string FmtBmp  = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";
    private const string FmtPng  = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";
    private const string FmtJpeg = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";

    // WIA item property IDs
    private const int WIA_IPS_CUR_INTENT  = 6146;
    private const int WIA_IPS_XRES        = 6147;
    private const int WIA_IPS_YRES        = 6148;
    private const int WIA_IPA_DATATYPE    = 4103;
    private const int WIA_DPS_DOCUMENT_HANDLING_SELECT       = 3088;
    private const int WIA_DPS_DOCUMENT_HANDLING_CAPABILITIES = 3086;
    private const int WIA_DPS_DOCUMENT_HANDLING_STATUS       = 3087;
    private const int CAP_FEEDER  = 1;
    private const int CAP_FLATBED = 2;
    private const int STATUS_FEED_READY = 1;

    private static readonly Dictionary<string, int> Intent   = new() { ["color"]=1,["gray"]=2,["grayscale"]=2,["bw"]=4,["text"]=4 };
    private static readonly Dictionary<string, int> Datatype = new() { ["color"]=3,["gray"]=2,["grayscale"]=2,["bw"]=0,["text"]=0 };
    private static readonly Dictionary<string, int> WiaHr = new()
    {
        [unchecked((int)0x80210003).ToString()] = 0,
        [unchecked((int)0x80210006).ToString()] = 1,
        [unchecked((int)0x80210001).ToString()] = 2,
        [unchecked((int)0x80210015).ToString()] = 3,
        [unchecked((int)0x8021000A).ToString()] = 4,
        [unchecked((int)0x80210067).ToString()] = 5,
    };
    private static readonly string[] WiaMessages =
    [
        "스캐너에 용지가 없습니다 (ADF empty). 종이를 올리고 다시 시도하세요.",
        "스캐너가 준비되지 않았습니다 (busy/warming up).",
        "일반 스캔 오류입니다.",
        "스캐너를 사용할 수 없습니다 (offline/disconnected).",
        "스캔 통신 오류입니다.",
        "스캔이 취소되었습니다.",
    ];

    public static List<Dictionary<string, string>> ListScanners()
    {
        List<Dictionary<string, string>>? result = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                dynamic mgr = CreateWia();
                result = new List<Dictionary<string, string>>();
                int count = (int)mgr.DeviceInfos.Count;
                for (int i = 1; i <= count; i++)
                {
                    dynamic info = mgr.DeviceInfos.Item(i);
                    if ((int)info.Type != 1) continue;
                    string name = "";
                    try
                    {
                        foreach (dynamic p in info.Properties)
                            if ((string)p.Name == "Name") { name = (string)p.Value; break; }
                    }
                    catch { }
                    result.Add(new() { ["id"] = (string)info.DeviceID, ["name"] = name != "" ? name : (string)info.DeviceID });
                }
            }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        return result ?? [];
    }

    /// <summary>
    /// WIA COM은 STA(Single-Thread Apartment)에서만 안정적으로 동작.
    /// ASP.NET Core 스레드풀(MTA)에서 직접 호출하면 이미지 데이터가 비정상적으로 반환될 수 있음.
    /// Python의 pythoncom.CoInitialize() 동작을 재현.
    /// </summary>
    public static string Scan(string outPath, string? deviceId, int dpi, string mode, string source, string fmt)
    {
        string? result = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = ScanCore(outPath, deviceId, dpi, mode, source, fmt); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        return result!;
    }

    private static string ScanCore(string outPath, string? deviceId, int dpi, string mode, string source, string fmt)
    {
        dynamic mgr = CreateWia();
        dynamic? target = null;
        int count = (int)mgr.DeviceInfos.Count;
        for (int i = 1; i <= count; i++)
        {
            dynamic info = mgr.DeviceInfos.Item(i);
            if ((int)info.Type != 1) continue;
            if (deviceId == null || (string)info.DeviceID == deviceId) { target = info; break; }
        }
        if (target == null)
            throw new ScannerError($"연결된 스캐너를 찾을 수 없습니다 (device_id={deviceId}).");

        dynamic device = target.Connect();
        int caps = GetProp(device.Properties, WIA_DPS_DOCUMENT_HANDLING_CAPABILITIES, 0);
        bool hasFeeder  = (caps & CAP_FEEDER) != 0;
        bool hasFlatbed = (caps & CAP_FLATBED) != 0;

        bool wantFeeder = source.ToLower() is "feeder" or "adf" || (hasFeeder && !hasFlatbed);
        SetProp(device.Properties, WIA_DPS_DOCUMENT_HANDLING_SELECT, wantFeeder ? CAP_FEEDER : CAP_FLATBED);

        if (wantFeeder)
        {
            int status = GetProp(device.Properties, WIA_DPS_DOCUMENT_HANDLING_STATUS, 0);
            if ((status & STATUS_FEED_READY) == 0)
                throw new ScannerError("급지구에 용지가 감지되지 않습니다. 용지를 가이드에 맞춰 끝까지 밀어 넣고 다시 시도하세요.");
        }

        dynamic item = device.Items.Item(1);
        SetProp(item.Properties, WIA_IPS_XRES, dpi);
        SetProp(item.Properties, WIA_IPS_YRES, dpi);
        SetProp(item.Properties, WIA_IPA_DATATYPE, Datatype.GetValueOrDefault(mode, 2));
        try { SetProp(item.Properties, WIA_IPS_CUR_INTENT, Intent.GetValueOrDefault(mode, 2)); } catch { }

        string fmtGuid = fmt.ToLower() switch
        {
            "png"  => FmtPng,
            "jpeg" => FmtJpeg,
            "jpg"  => FmtJpeg,
            _      => FmtBmp,
        };

        if (File.Exists(outPath)) File.Delete(outPath);

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                dynamic image = item.Transfer(fmtGuid);
                image.SaveFile(outPath);
                return outPath;
            }
            catch (COMException ex) when (attempt == 0 && ((uint)ex.HResult == 0x80210003))
            {
                Thread.Sleep(1200);
            }
            catch (COMException ex)
            {
                throw new ScannerError(WiaErrorText(ex.HResult));
            }
        }
        throw new ScannerError("스캔 실패 (재시도 후에도 용지 없음).");
    }

    private static string WiaErrorText(int hr)
    {
        uint uhr = (uint)hr;
        return uhr switch
        {
            0x80210003 => WiaMessages[0],
            0x80210006 => WiaMessages[1],
            0x80210001 => WiaMessages[2],
            0x80210015 => WiaMessages[3],
            0x8021000A => WiaMessages[4],
            0x80210067 => WiaMessages[5],
            _ => $"스캔 실패 (HRESULT 0x{uhr:X8}).",
        };
    }

    private static dynamic CreateWia()
    {
        var t = Type.GetTypeFromProgID("WIA.DeviceManager", true)!;
        return Activator.CreateInstance(t)!;
    }

    private static void SetProp(dynamic props, int pid, int value)
    {
        try
        {
            int n = (int)props.Count;
            for (int i = 1; i <= n; i++)
            {
                dynamic p = props.Item(i);
                if ((int)p.PropertyID == pid) { p.Value = value; return; }
            }
        }
        catch { }
    }

    private static int GetProp(dynamic props, int pid, int def)
    {
        try
        {
            int n = (int)props.Count;
            for (int i = 1; i <= n; i++)
            {
                dynamic p = props.Item(i);
                if ((int)p.PropertyID == pid) return (int)p.Value;
            }
        }
        catch { }
        return def;
    }
}
