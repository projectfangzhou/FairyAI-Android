using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace FairyAI_Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int PermissionRequestCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestRequiredPermissions();
    }

    private void RequestRequiredPermissions()
    {
        var permissionsNeeded = new List<string>();

        // Microphone
        if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.RecordAudio) != Permission.Granted)
            permissionsNeeded.Add(Android.Manifest.Permission.RecordAudio);

        // Notifications (Android 13+)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.PostNotifications) != Permission.Granted)
                permissionsNeeded.Add(Android.Manifest.Permission.PostNotifications);
        }

        // Bluetooth (Android 12+)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.BluetoothConnect) != Permission.Granted)
                permissionsNeeded.Add(Android.Manifest.Permission.BluetoothConnect);
            if (ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.BluetoothScan) != Permission.Granted)
                permissionsNeeded.Add(Android.Manifest.Permission.BluetoothScan);
        }

        if (permissionsNeeded.Count > 0)
        {
            ActivityCompat.RequestPermissions(this, permissionsNeeded.ToArray(), PermissionRequestCode);
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

        if (requestCode == PermissionRequestCode)
        {
            for (int i = 0; i < permissions.Length; i++)
            {
                if (grantResults[i] == Permission.Denied)
                {
                    System.Diagnostics.Debug.WriteLine($"Permission denied: {permissions[i]}");
                }
            }
        }
    }
}
