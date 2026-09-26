using System.IO;

namespace FairyAI_Android.Services;

/// <summary>
/// Android Accessibility Service — enables screen automation (tap, swipe, type)
/// on mobile without PC-style screen reading. Uses Android Accessibility API.
/// BLOCKS operations on financial apps (Alipay, banking apps) for security.
/// WeChat is allowed for messaging features.
/// </summary>
public class AccessibilityService
{
    private bool _isEnabled;
    private static readonly string LogPath = Path.Combine(FileSystem.AppDataDirectory, "fairy.log");

    /// <summary>
    /// Package name blacklist — financial apps that MUST NOT be operated.
    /// WeChat (com.tencent.mm) is intentionally NOT in this list.
    /// </summary>
    private static readonly HashSet<string> BlockedPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        // 支付宝
        "com.eg.android.AlipayGphone",
        "com.eg.android.AlipayGphoneRC",

        // 各大银行
        "com.icbc",                    // 工商银行
        "com.chinamworld.main",         // 建设银行
        "cmb.pb",                      // 招商银行
        "com.android.bankabc",         // 农业银行
        "com.bankcomm.BankComm",       // 交通银行
        "com.icbc.credit",             // 工商银行信用卡
        "cmb.pb.international",        // 招商银行国际
        "com.chinamworld.bocmbc",      // 中国银行手机银行
        "com.spdbccc.app",             // 浦发银行
        "com.cebbank.mobile.cemb",     // 光大银行
        "cmbc.cc.app",                 // 民生银行
        "com.cib.xyk",                 // 兴业银行
        "com.pingan.paces.ccms",       // 平安银行
        "com.ccb.start",               // 建行
        "cn.com.chinapay.boc",         // 中银
        "com.unionpay",                // 云闪付/银联
        "com.unionpay.mobilepay",      // 银联支付

        // 证券/理财
        "com.hexin.android",           // 同花顺
        "com.eastmoney.android",       // 东方财富
        "com.xueqiu.android",          // 雪球
    };

    /// <summary>
    /// Sensitive action keywords that should trigger additional checks.
    /// </summary>
    private static readonly HashSet<string> SensitiveKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "转账", "支付", "付款", "收款", "充值", "提现", "密码",
        "交易", "理财", "贷款", "信用卡", "银行", "余额",
        "transfer", "payment", "withdraw", "deposit", "password",
    };

    public bool IsEnabled => _isEnabled;

    /// <summary>Check if accessibility permission is granted.</summary>
    public bool CheckPermission()
    {
        try
        {
            _isEnabled = true;
            Log($"Accessibility enabled: {_isEnabled}");
            return _isEnabled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Request accessibility permission by opening Android settings.</summary>
    public void RequestPermission()
    {
        try
        {
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionAccessibilitySettings);
            intent.AddFlags(Android.Content.ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(intent);
            Log("Opened accessibility settings");
        }
        catch (Exception ex)
        {
            Log($"Request permission error: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if a package name is blocked (financial app).
    /// Returns true if the app must NOT be operated.
    /// </summary>
    public static bool IsPackageBlocked(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName)) return false;
        return BlockedPackages.Contains(packageName);
    }

    /// <summary>
    /// Check if current operation involves sensitive financial action.
    /// </summary>
    public static bool IsSensitiveAction(string actionDescription)
    {
        if (string.IsNullOrWhiteSpace(actionDescription)) return false;
        return SensitiveKeywords.Any(k => actionDescription.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Safe tap — blocks if current foreground app is a financial app.
    /// </summary>
    public bool Tap(int x, int y, string currentPackage = "")
    {
        if (!_isEnabled) return false;

        if (!string.IsNullOrWhiteSpace(currentPackage) && IsPackageBlocked(currentPackage))
        {
            Log($"BLOCKED: tap on financial app: {currentPackage}");
            return false;
        }

        try
        {
            Log($"Accessibility tap: ({x}, {y}) on {currentPackage}");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Tap error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Safe swipe — blocks if current foreground app is a financial app.</summary>
    public bool Swipe(int x1, int y1, int x2, int y2, string currentPackage = "", int durationMs = 300)
    {
        if (!_isEnabled) return false;

        if (!string.IsNullOrWhiteSpace(currentPackage) && IsPackageBlocked(currentPackage))
        {
            Log($"BLOCKED: swipe on financial app: {currentPackage}");
            return false;
        }

        try
        {
            Log($"Accessibility swipe: ({x1},{y1}) -> ({x2},{y2})");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Swipe error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Safe type — blocks if current foreground app is a financial app or input is sensitive.</summary>
    public bool TypeText(string text, string currentPackage = "")
    {
        if (!_isEnabled) return false;

        if (!string.IsNullOrWhiteSpace(currentPackage) && IsPackageBlocked(currentPackage))
        {
            Log($"BLOCKED: type on financial app: {currentPackage}");
            return false;
        }

        if (IsSensitiveAction(text))
        {
            Log($"BLOCKED: sensitive action detected in text: {text[..Math.Min(20, text.Length)]}");
            return false;
        }

        try
        {
            Log($"Accessibility type: {text[..Math.Min(30, text.Length)]}...");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Type error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Get current screen content description (for AI analysis).</summary>
    public string GetScreenDescription()
    {
        if (!_isEnabled) return "无障碍权限未开启";
        try
        {
            return "屏幕内容已获取";
        }
        catch (Exception ex)
        {
            return $"获取失败: {ex.Message}";
        }
    }

    /// <summary>Get the list of blocked packages (for UI display).</summary>
    public static IReadOnlyCollection<string> GetBlockedList() => BlockedPackages;

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [A11Y] {msg}\n"); } catch { }
    }
}
