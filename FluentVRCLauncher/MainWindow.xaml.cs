using iNKORE.UI.WPF.Modern;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Windows.UI.ViewManagement;

namespace FluentVRC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public enum WindowsTheme
        {
            Unknown,
            Light,
            Dark
        }

        private DispatcherTimer statusVisibilityTimer; // ★ DispatcherTimer フィールドを追加


        public MainWindow()
        {
            InitializeComponent();

            InitializeStatusTimer();

            LoadSettings();
        }




        public void TerminateVRProcesses()
        {
            try
            {
                // システム上の全プロセスを取得
                var allProcesses = Process.GetProcesses();

                // 条件に合うプロセスをフィルタリング
                // 1. プロセス名に "VR" を含む (大文字小文字を区別しない)
                // 2. プロセス名が "FluentVRC" ではない (大文字小文字を区別しない)
                var processesToKill = allProcesses
                    .Where(p => p.ProcessName.Contains("VR", StringComparison.OrdinalIgnoreCase)
                             && !p.ProcessName.Equals("FluentVRC", StringComparison.OrdinalIgnoreCase));

                bool foundProcess = false; // 対象が見つかったかどうかのフラグ

                // フィルタリングされた各プロセスを終了
                foreach (var process in processesToKill)
                {
                    foundProcess = true;
                    try
                    {
                        Console.WriteLine($"プロセス '{process.ProcessName}' (ID: {process.Id}) を終了しようとしています...");
                        process.Kill(); // プロセスを強制終了
                                        // Kill() は非同期の場合があるので、少し待つか確認が必要な場合がある
                                        // process.WaitForExit(500); // 必要であれば短い待機時間を追加 (ミリ秒)
                        Console.WriteLine($"プロセス '{process.ProcessName}' (ID: {process.Id}) を終了しました。");
                    }
                    catch (Win32Exception ex)
                    {
                        // アクセス権がない、などのOSレベルのエラー
                        Console.WriteLine($"エラー: プロセス '{process.ProcessName}' (ID: {process.Id}) の終了に失敗しました (権限等)。{ex.Message}");
                    }
                    catch (InvalidOperationException ex)
                    {
                        // プロセスが既に終了している、または終了できない状態
                        Console.WriteLine($"エラー: プロセス '{process.ProcessName}' (ID: {process.Id}) は既に終了しているか、終了できません。{ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        // その他の予期せぬエラー
                        Console.WriteLine($"エラー: プロセス '{process.ProcessName}' (ID: {process.Id}) の終了中に予期せぬエラーが発生しました。{ex.Message}");
                    }
                    finally
                    {
                        // 各プロセスオブジェクトのリソースを解放
                        process.Dispose();
                    }
                }

                // 対象プロセスが見つからなかった場合にメッセージを表示
                if (!foundProcess)
                {
                    Console.WriteLine("終了対象となるVR関連プロセスは見つかりませんでした。");
                }
            }
            catch (Exception ex)
            {
                // GetProcesses() など、プロセス列挙自体でエラーが発生した場合
                Console.WriteLine($"プロセスの取得または処理中にエラーが発生しました: {ex.Message}");
            }
        }



        private void InitializeStatusTimer()
        {
            statusVisibilityTimer = new DispatcherTimer();
            // Tickイベントハンドラを設定（タイマーが指定時間経過したときに呼ばれる）
            statusVisibilityTimer.Tick += StatusVisibilityTimer_Tick;
            // Interval は ShowStatusIndicator で設定する
        }

        // タイマーの Tick イベントハンドラ
        private void StatusVisibilityTimer_Tick(object sender, EventArgs e)
        {
            // タイマーが Tick したら実行される処理
            if (status is Shape statusShape)
            {
                statusShape.Opacity = 0.0; // Opacity を 0 にして非表示に
            }
            // タイマーを停止 (一度だけ実行するため)
            statusVisibilityTimer.Stop();
        }

        private void LoadSettings()
        {
            // Properties.Settings.Default から設定値を読み込む
            // 初回起動時などは設定が存在しない可能性があるため、デフォルト値を考慮する
            AutoStartCheckBox.IsChecked = Properties.Settings.Default.IsAutoStartEnabled;
            UseDesktopByDefaultCheckBox.IsChecked = Properties.Settings.Default.UseDesktopByDefault;
            if (UseDesktopByDefaultCheckBox.IsChecked == true)
            {
                vr.IsDefault = false;
                desktop.IsDefault = true;
                desktop.SetResourceReference(FrameworkElement.StyleProperty, ThemeKeys.AccentButtonStyleKey);
                vr.SetResourceReference(FrameworkElement.StyleProperty, ThemeKeys.DefaultButtonStyleKey);
            }
            else
            {
                vr.IsDefault = true;
                desktop.IsDefault = false;
                vr.SetResourceReference(FrameworkElement.StyleProperty, ThemeKeys.AccentButtonStyleKey);
                desktop.SetResourceReference(FrameworkElement.StyleProperty, ThemeKeys.DefaultButtonStyleKey);
            }



            if (AutoStartCheckBox.IsChecked == true)
            {
                if (UseDesktopByDefaultCheckBox.IsChecked == true)
                {
                    LaunchVRChat(false);
                }
                else
                {
                    LaunchVRChat(true);
                }

            }
        }

        private void SaveSettings()
        {
            // CheckBox の IsChecked プロパティ (Nullable<bool>) から値を取得
            // IsChecked が null の場合は false として扱う (チェックされていない状態)
            Properties.Settings.Default.IsAutoStartEnabled = AutoStartCheckBox.IsChecked ?? false;
            Properties.Settings.Default.UseDesktopByDefault = UseDesktopByDefaultCheckBox.IsChecked ?? false;

            // 変更をファイルに保存
            Properties.Settings.Default.Save();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveSettings(); // 設定を保存する
        }


        private const string VRChatAppId = "438100";


        public bool LaunchVRChat(bool launchInVr)
        {
            string steamUri;

            if (launchInVr)
            {
                // VRモードの場合、steam://launch/<appid>/vr を試す
                // これがSteamVRの起動もハンドルしてくれる可能性がある
                steamUri = $"steam://launch/{VRChatAppId}/vr";
                Console.WriteLine("VRモード (launchプロトコル) で起動試行中...");
            }
            else
            {
                // デスクトップモードは steam://run を使う (--no-vr オプション付き)
                steamUri = $"steam://run/{VRChatAppId}//--no-vr";
                Console.WriteLine("デスクトップモードで起動試行中...");
            }

            Console.WriteLine($"実行するURI: {steamUri}");

            try
            {
                Process.Start(new ProcessStartInfo(steamUri) { UseShellExecute = true });
                ShowStatusIndicator();

                return true;
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine($"エラー: Steamプロトコルを実行できませんでした。Steamが正しくインストールされているか確認してください。 詳細: {ex.Message}");
                // WPF/WinForms等では MessageBox.Show などで通知
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"エラー: VRChatの起動中に予期せぬエラーが発生しました。 詳細: {ex.Message}");
                // WPF/WinForms等では MessageBox.Show などで通知
                return false;
            }
        }

        private static readonly UISettings uiSettings = new UISettings();

        /// <summary>
        /// 現在の Windows アプリケーションテーマ (Light/Dark) を取得します。
        /// </summary>
        /// <returns>判定結果 (Light, Dark, または Unknown)</returns>
        public static WindowsTheme GetCurrentTheme()
        {
            try
            {
                // アプリケーションの背景色を取得
                Windows.UI.Color backgroundColor = uiSettings.GetColorValue(UIColorType.Background);

                // 色の輝度 (Luminance) を計算して明るさを判断
                // 輝度 Y = 0.2126*R + 0.7152*G + 0.0722*B (sRGB/Rec.709 標準)
                // 値は 0 (黒) から 1 (白) の範囲になる
                double luminance = (0.2126 * backgroundColor.R + 0.7152 * backgroundColor.G + 0.0722 * backgroundColor.B) / 255.0;

                // 輝度が 0.5 より大きい場合はライトテーマ、そうでなければダークテーマと判定
                return luminance > 0.5 ? WindowsTheme.Light : WindowsTheme.Dark;
            }
            catch (Exception ex)
            {
                // WinRT API が利用できない環境 (古いWindowsなど) やその他のエラー
                Console.WriteLine($"Windows テーマの取得に失敗しました: {ex.Message}");
                // または System.Diagnostics.Debug.WriteLine を使用
                return WindowsTheme.Unknown;
            }
        }

        private void ShowStatusIndicator()
        {
            WindowsTheme currentSystemTheme = MainWindow.GetCurrentTheme(); // MainWindow内に定義されている場合



            // status コントロールが Shape であることを確認
            if (!(status is Shape statusShape)) return;

            // ★ 既存のタイマーが動作中なら停止する
            if (statusVisibilityTimer.IsEnabled)
            {
                statusVisibilityTimer.Stop();
                // 念のため、前のTickでOpacityが0になっていなくてもここで0にする
                // (連続クリックなどで前のタイマーが止まる前に再度呼ばれた場合)
                // statusShape.Opacity = 0.0;
            }


            if (currentSystemTheme == WindowsTheme.Dark)
            {
                statusShape.Fill = new SolidColorBrush(Colors.Lime); // ライム色
            }
            else
            {
                statusShape.Fill = new SolidColorBrush(Colors.Green); // 緑色
            }

            //// 1. テーマに応じて色を設定
            //if (ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark)
            //{
            //    statusShape.Fill = new SolidColorBrush(Colors.Lime); // ライム色
            //}
            //else // Light テーマ または Default
            //{
            //    statusShape.Fill = new SolidColorBrush(Colors.Green); // 緑色
            //}

            // 2. Opacity を 1 にして表示状態にする
            statusShape.Opacity = 1.0;

            // ★ 3. DispatcherTimer の Interval を設定し、開始する
            statusVisibilityTimer.Interval = TimeSpan.FromSeconds(30); // 10秒後に Tick イベントが発生
            statusVisibilityTimer.Start();

            // --- アニメーション関連のコードは削除 ---
            // DoubleAnimation fadeOutAnimation = new DoubleAnimation { ... };
            // statusFadeOutStoryboard = new Storyboard();
            // statusFadeOutStoryboard.Children.Add(fadeOutAnimation);
            // Storyboard.SetTarget(...);
            // Storyboard.SetTargetProperty(...);
            // statusFadeOutStoryboard.Begin(statusShape, true);
        }


        private void vr_Click(object sender, RoutedEventArgs e)
        {
            LaunchVRChat(true);
        }

        private void desktop_Click(object sender, RoutedEventArgs e)
        {
            LaunchVRChat(false);
        }

        private void UseDesktopByDefaultCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (UseDesktopByDefaultCheckBox.IsChecked == true)
            {
                vr.IsDefault = false;
                desktop.IsDefault = true;
                desktop.SetResourceReference(FrameworkElement.StyleProperty, ThemeKeys.AccentButtonStyleKey);
                vr.SetResourceReference(FrameworkElement.StyleProperty, ThemeKeys.DefaultButtonStyleKey);
            }
            else
            {
                vr.IsDefault = true;
                desktop.IsDefault = false;
                vr.SetResourceReference(FrameworkElement.StyleProperty, ThemeKeys.AccentButtonStyleKey);
                desktop.SetResourceReference(FrameworkElement.StyleProperty, ThemeKeys.DefaultButtonStyleKey);
            }
        }


        private void killButton_Click(object sender, RoutedEventArgs e)
        {
            TerminateVRProcesses();
        }
    }
}