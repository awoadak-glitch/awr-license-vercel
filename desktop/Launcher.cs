using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Reflection;

[assembly: AssemblyTitle("المستقبل")]
[assembly: AssemblyDescription("نظام المستقبل لإدارة الأعمال")]
[assembly: AssemblyCompany("المستقبل")]
[assembly: AssemblyProduct("المستقبل")]
[assembly: AssemblyCopyright("Copyright © المستقبل 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace AlMustaqbal
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class LicenseReply
    {
        public bool success { get; set; }
        public string code { get; set; }
        public string protocol { get; set; }
        public string alg { get; set; }
        public string kid { get; set; }
        public string status { get; set; }
        public string license_id { get; set; }
        public string device_fingerprint { get; set; }
        public string nonce { get; set; }
        public string app_version { get; set; }
        public string client_hash { get; set; }
        public long issued_at { get; set; }
        public long token_expires_at { get; set; }
        public string license_expires_at { get; set; }
        public long? remaining_seconds { get; set; }
        public string signature { get; set; }
        public int retry_after { get; set; }
        public int max_devices { get; set; }
    }

    internal sealed class GradientPanel : Panel
    {
        public Color Color1 = Color.FromArgb(18, 83, 214);
        public Color Color2 = Color.FromArgb(47, 132, 255);

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(ClientRectangle, Color1, Color2, 25f))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        public int Radius = 18;
        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            using (GraphicsPath p = new GraphicsPath())
            {
                int d = Radius * 2;
                p.AddArc(0, 0, d, d, 180, 90);
                p.AddArc(Width - d - 1, 0, d, d, 270, 90);
                p.AddArc(Width - d - 1, Height - d - 1, d, d, 0, 90);
                p.AddArc(0, Height - d - 1, d, d, 90, 90);
                p.CloseFigure();
                Region = new Region(p);
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private const string AppVersion = "1.0.0";
        private const string Protocol = "mustaqbal-license-v1";
        private const string Algorithm = "ES256-P1363";
        private const string KeyId = "82f6e932a8d975f00adf";
        private const string PubX = "ODac4HCfHNJTEn8vRsBLjySvGRXkgcsWcsG5ivpCfuI";
        private const string PubY = "Cyy0BkZECkmfh5q_pdunajsw9Pa7QmssZYmXS4IX9n8";
        private const string CoreHash = "349634fbb4fc331344c126fa238929dada30a22a95ce481a29c81ac946524ba8";
        private const string CommonHash = "7c53c355f40d6fa56ac1279edfc991bd060e38820d5505c23535fea27cb7c2e1";
        private const string CoreArguments = "mnf pos vm fa mj hr arc lr pk of re fs rs ot1 ot2";

        private TextBox keyBox;
        private Button verifyButton;
        private Label statusLabel;
        private Label remainingLabel;
        private Label shellLicenseLabel;
        private Panel hostPanel;
        private Panel contentPanel;
        private Process coreProcess;
        private IntPtr hostedWindow = IntPtr.Zero;
        private Timer watchdogTimer;
        private Timer resizeTimer;
        private bool verifying;
        private string activeKey;
        private LicenseReply activeReply;
        private string activeRawReply;
        private DateTime nextOnlineCheckUtc = DateTime.MinValue;
        private Point dragStart;
        private bool dragging;

        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        public MainForm()
        {
            Text = "المستقبل";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(920, 590);
            Size = new Size(1040, 680);
            BackColor = Color.FromArgb(245, 248, 253);
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.None;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            DoubleBuffered = true;

            BuildLicenseView();

            watchdogTimer = new Timer();
            watchdogTimer.Interval = 3000;
            watchdogTimer.Tick += WatchdogTimer_Tick;

            resizeTimer = new Timer();
            resizeTimer.Interval = 250;
            resizeTimer.Tick += delegate { ResizeHostedWindow(); };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            string saved = LoadSavedKey();
            if (!string.IsNullOrWhiteSpace(saved))
            {
                keyBox.Text = saved;
                BeginVerify(saved, true);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                if (coreProcess != null && !coreProcess.HasExited)
                {
                    coreProcess.CloseMainWindow();
                    if (!coreProcess.WaitForExit(1200)) coreProcess.Kill();
                }
            }
            catch { }
            base.OnFormClosing(e);
        }

        private void BuildLicenseView()
        {
            Controls.Clear();
            hostedWindow = IntPtr.Zero;
            WindowState = FormWindowState.Normal;
            Size = new Size(1040, 680);
            CenterToScreen();

            GradientPanel header = BuildHeader(false);
            Controls.Add(header);

            contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Color.FromArgb(245, 248, 253);
            Controls.Add(contentPanel);
            header.BringToFront();

            RoundedPanel card = new RoundedPanel();
            card.Size = new Size(560, 390);
            card.BackColor = Color.White;
            card.Anchor = AnchorStyles.None;
            contentPanel.Controls.Add(card);
            contentPanel.Resize += delegate
            {
                card.Left = (contentPanel.ClientSize.Width - card.Width) / 2;
                card.Top = Math.Max(30, (contentPanel.ClientSize.Height - card.Height) / 2);
            };
            card.Left = (contentPanel.ClientSize.Width - card.Width) / 2;
            card.Top = 70;

            Label badge = new Label();
            badge.Text = "M";
            badge.TextAlign = ContentAlignment.MiddleCenter;
            badge.Font = new Font("Segoe UI", 22f, FontStyle.Bold);
            badge.ForeColor = Color.White;
            badge.BackColor = Color.FromArgb(25, 103, 231);
            badge.Size = new Size(64, 64);
            badge.Location = new Point(248, 30);
            badge.Region = MakeRoundRegion(64, 64, 32);
            card.Controls.Add(badge);

            Label title = new Label();
            title.Text = "تفعيل المستقبل";
            title.Font = new Font("Segoe UI", 21f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(23, 42, 78);
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.SetBounds(40, 105, 480, 45);
            card.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "أدخل كود الاشتراك للمتابعة إلى النظام";
            sub.Font = new Font("Segoe UI", 10.5f);
            sub.ForeColor = Color.FromArgb(98, 112, 139);
            sub.TextAlign = ContentAlignment.MiddleCenter;
            sub.SetBounds(40, 150, 480, 28);
            card.Controls.Add(sub);

            Label keyLabel = new Label();
            keyLabel.Text = "كود الاشتراك";
            keyLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            keyLabel.ForeColor = Color.FromArgb(45, 60, 88);
            keyLabel.TextAlign = ContentAlignment.MiddleRight;
            keyLabel.RightToLeft = RightToLeft.Yes;
            keyLabel.SetBounds(60, 195, 440, 24);
            card.Controls.Add(keyLabel);

            keyBox = new TextBox();
            keyBox.Font = new Font("Segoe UI", 12f);
            keyBox.BorderStyle = BorderStyle.FixedSingle;
            keyBox.TextAlign = HorizontalAlignment.Center;
            keyBox.SetBounds(60, 222, 440, 38);
            keyBox.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter) BeginVerify(keyBox.Text, false);
            };
            card.Controls.Add(keyBox);

            verifyButton = new Button();
            verifyButton.Text = "تحقق وابدأ";
            verifyButton.FlatStyle = FlatStyle.Flat;
            verifyButton.FlatAppearance.BorderSize = 0;
            verifyButton.BackColor = Color.FromArgb(25, 103, 231);
            verifyButton.ForeColor = Color.White;
            verifyButton.Cursor = Cursors.Hand;
            verifyButton.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            verifyButton.SetBounds(60, 278, 440, 44);
            verifyButton.Click += delegate { BeginVerify(keyBox.Text, false); };
            card.Controls.Add(verifyButton);

            statusLabel = new Label();
            statusLabel.Text = "اتصال آمن • ترخيص مرتبط بالجهاز";
            statusLabel.ForeColor = Color.FromArgb(105, 118, 143);
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            statusLabel.SetBounds(40, 330, 480, 24);
            card.Controls.Add(statusLabel);

            remainingLabel = new Label();
            remainingLabel.Text = "";
            remainingLabel.ForeColor = Color.FromArgb(25, 103, 231);
            remainingLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            remainingLabel.TextAlign = ContentAlignment.MiddleCenter;
            remainingLabel.SetBounds(40, 354, 480, 24);
            card.Controls.Add(remainingLabel);
        }

        private GradientPanel BuildHeader(bool shell)
        {
            GradientPanel header = new GradientPanel();
            header.Dock = DockStyle.Top;
            header.Height = shell ? 72 : 112;
            header.Color1 = Color.FromArgb(13, 74, 191);
            header.Color2 = Color.FromArgb(44, 135, 255);
            header.MouseDown += Header_MouseDown;
            header.MouseMove += Header_MouseMove;
            header.MouseUp += Header_MouseUp;

            Label name = new Label();
            name.Text = "المستقبل";
            name.ForeColor = Color.White;
            name.BackColor = Color.Transparent;
            name.Font = new Font("Segoe UI", shell ? 20f : 29f, FontStyle.Bold);
            name.TextAlign = ContentAlignment.MiddleRight;
            name.RightToLeft = RightToLeft.Yes;
            name.SetBounds(32, shell ? 13 : 18, 330, shell ? 45 : 48);
            name.MouseDown += Header_MouseDown;
            name.MouseMove += Header_MouseMove;
            name.MouseUp += Header_MouseUp;
            header.Controls.Add(name);

            Label slogan = new Label();
            slogan.Text = shell ? "نظام إدارة الأعمال" : "إدارة أعمالك برؤية أوضح";
            slogan.ForeColor = Color.FromArgb(220, 235, 255);
            slogan.BackColor = Color.Transparent;
            slogan.Font = new Font("Segoe UI", 10f);
            slogan.TextAlign = ContentAlignment.MiddleRight;
            slogan.RightToLeft = RightToLeft.Yes;
            slogan.SetBounds(36, shell ? 43 : 67, 326, 26);
            header.Controls.Add(slogan);

            if (shell)
            {
                shellLicenseLabel = new Label();
                shellLicenseLabel.ForeColor = Color.White;
                shellLicenseLabel.BackColor = Color.FromArgb(40, 255, 255, 255);
                shellLicenseLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                shellLicenseLabel.TextAlign = ContentAlignment.MiddleCenter;
                shellLicenseLabel.SetBounds(390, 20, 300, 32);
                header.Controls.Add(shellLicenseLabel);
            }

            Button close = BuildWindowButton("×", Color.FromArgb(201, 54, 54));
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(Width - 50, 14);
            close.Click += delegate { Close(); };
            header.Controls.Add(close);

            Button min = BuildWindowButton("—", Color.FromArgb(255, 255, 255));
            min.ForeColor = Color.FromArgb(26, 77, 158);
            min.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            min.Location = new Point(Width - 96, 14);
            min.Click += delegate { WindowState = FormWindowState.Minimized; };
            header.Controls.Add(min);

            Button max = BuildWindowButton("□", Color.FromArgb(255, 255, 255));
            max.ForeColor = Color.FromArgb(26, 77, 158);
            max.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            max.Location = new Point(Width - 142, 14);
            max.Visible = shell;
            max.Click += delegate
            {
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                ResizeHostedWindow();
            };
            header.Controls.Add(max);

            return header;
        }

        private Button BuildWindowButton(string text, Color back)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            b.Size = new Size(38, 32);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private async void BeginVerify(string key, bool automatic)
        {
            if (verifying) return;
            key = (key ?? "").Trim();
            if (key.Length == 0)
            {
                SetLicenseStatus("أدخل كود الاشتراك أولاً", false);
                return;
            }

            verifying = true;
            if (verifyButton != null) verifyButton.Enabled = false;
            SetLicenseStatus("جاري التحقق الآمن من الاشتراك...", true);

            try
            {
                VerificationResult result = await Task.Run(delegate { return VerifyOnline(key); });
                if (!result.Valid)
                {
                    if (!automatic || contentPanel != null) SetLicenseStatus(MapError(result.Code), false);
                    return;
                }

                activeKey = key;
                activeReply = result.Reply;
                activeRawReply = result.Raw;
                SaveKey(key);
                UpdateRemainingLabel(result.Reply);
                SetLicenseStatus("تم التحقق من الترخيص بنجاح", true);
                await Task.Delay(650);
                EnterApplicationShell();
            }
            catch (Exception ex)
            {
                SetLicenseStatus("تعذر الاتصال بخادم الترخيص. يلزم اتصال إنترنت.", false);
                Debug.WriteLine(ex);
            }
            finally
            {
                verifying = false;
                if (verifyButton != null) verifyButton.Enabled = true;
            }
        }

        private VerificationResult VerifyOnline(string key)
        {
            string nonce = CreateNonce();
            string deviceId = DeviceId();
            string selfHash = Sha256File(Application.ExecutablePath).ToLowerInvariant();

            Dictionary<string, object> body = new Dictionary<string, object>();
            body["key"] = key;
            body["device_id"] = deviceId;
            body["nonce"] = nonce;
            body["app_version"] = AppVersion;
            body["client_hash"] = selfHash;

            string requestJson = serializer.Serialize(body);
            string endpoint = DecodeEndpoint();
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Accept = "application/json";
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            req.Proxy = WebRequest.DefaultWebProxy;
            req.UserAgent = "AlMustaqbal/" + AppVersion;
            byte[] payload = Encoding.UTF8.GetBytes(requestJson);
            req.ContentLength = payload.Length;
            using (Stream s = req.GetRequestStream()) s.Write(payload, 0, payload.Length);

            string raw;
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                using (StreamReader r = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    raw = r.ReadToEnd();
            }
            catch (WebException wex)
            {
                if (wex.Response == null) throw;
                using (StreamReader r = new StreamReader(wex.Response.GetResponseStream(), Encoding.UTF8))
                    raw = r.ReadToEnd();
            }

            LicenseReply reply = serializer.Deserialize<LicenseReply>(raw);
            if (reply == null || !reply.success)
                return VerificationResult.Fail(reply == null ? "SERVER_ERROR" : reply.code);

            if (!string.Equals(reply.protocol, Protocol, StringComparison.Ordinal) ||
                !string.Equals(reply.alg, Algorithm, StringComparison.Ordinal) ||
                !string.Equals(reply.kid, KeyId, StringComparison.Ordinal) ||
                !string.Equals(reply.status, "VALID", StringComparison.Ordinal) ||
                !string.Equals(reply.nonce, nonce, StringComparison.Ordinal) ||
                !string.Equals(reply.app_version, AppVersion, StringComparison.Ordinal) ||
                !string.Equals(reply.client_hash, selfHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(reply.device_fingerprint, Sha256Text(deviceId), StringComparison.OrdinalIgnoreCase))
                return VerificationResult.Fail("BAD_SIGNATURE");

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (reply.issued_at > now + 90 || reply.issued_at < now - 300) return VerificationResult.Fail("STALE_TOKEN");
            if (reply.token_expires_at < now || reply.token_expires_at > reply.issued_at + 180) return VerificationResult.Fail("STALE_TOKEN");
            if (reply.remaining_seconds.HasValue && reply.remaining_seconds.Value <= 0) return VerificationResult.Fail("EXPIRED");

            string canonical = serializer.Serialize(new object[] {
                Protocol,
                reply.status,
                reply.license_id,
                reply.device_fingerprint,
                reply.nonce,
                reply.app_version,
                reply.client_hash,
                reply.issued_at,
                reply.token_expires_at,
                string.IsNullOrEmpty(reply.license_expires_at) ? "" : reply.license_expires_at,
                reply.remaining_seconds.HasValue ? (object)reply.remaining_seconds.Value : (object)(-1L)
            });

            if (!VerifySignature(canonical, reply.signature)) return VerificationResult.Fail("BAD_SIGNATURE");
            return VerificationResult.Ok(reply, raw);
        }

        private void EnterApplicationShell()
        {
            if (!VerifyCoreIntegrity())
            {
                BuildLicenseView();
                SetLicenseStatus("فشل فحص سلامة ملفات البرنامج. أعد تثبيت نسخة المستقبل الأصلية.", false);
                return;
            }

            Controls.Clear();
            GradientPanel header = BuildHeader(true);
            Controls.Add(header);

            hostPanel = new Panel();
            hostPanel.Dock = DockStyle.Fill;
            hostPanel.BackColor = Color.White;
            hostPanel.Padding = new Padding(1);
            Controls.Add(hostPanel);
            header.BringToFront();
            hostPanel.Resize += delegate { ResizeHostedWindow(); };

            WindowState = FormWindowState.Maximized;
            UpdateShellLicenseText();

            try
            {
                string core = FindCorePath();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = core;
                psi.WorkingDirectory = Path.GetDirectoryName(core);
                psi.Arguments = CoreArguments;
                psi.UseShellExecute = false;
                psi.EnvironmentVariables["MUSTAQBAL_SESSION"] = activeRawReply ?? "";
                psi.EnvironmentVariables["MUSTAQBAL_LICENSE_ID"] = activeReply == null ? "" : (activeReply.license_id ?? "");
                psi.EnvironmentVariables["MUSTAQBAL_PARENT_PID"] = Process.GetCurrentProcess().Id.ToString();
                coreProcess = Process.Start(psi);
                coreProcess.EnableRaisingEvents = true;
                coreProcess.Exited += delegate
                {
                    try { BeginInvoke((MethodInvoker)delegate { Close(); }); } catch { }
                };
                nextOnlineCheckUtc = DateTime.UtcNow.AddMinutes(5);
                watchdogTimer.Start();
                resizeTimer.Start();
                Task.Run(delegate { AttachCoreWindow(); });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                BuildLicenseView();
                SetLicenseStatus("تعذر تشغيل ملفات النظام الداخلية.", false);
            }
        }

        private void AttachCoreWindow()
        {
            if (coreProcess == null) return;
            try { coreProcess.WaitForInputIdle(12000); } catch { }

            for (int i = 0; i < 120; i++)
            {
                if (coreProcess == null || coreProcess.HasExited) return;
                AutoAcceptLegacyDemo(coreProcess.Id);
                coreProcess.Refresh();
                IntPtr h = coreProcess.MainWindowHandle;
                if (h != IntPtr.Zero)
                {
                    string t = GetText(h);
                    if (!ContainsDemoText(t))
                    {
                        try
                        {
                            BeginInvoke((MethodInvoker)delegate { HostWindow(h); });
                            return;
                        }
                        catch { return; }
                    }
                }
                System.Threading.Thread.Sleep(250);
            }
        }

        private void HostWindow(IntPtr h)
        {
            if (hostPanel == null || h == IntPtr.Zero) return;
            hostedWindow = h;
            try
            {
                SetWindowText(h, "المستقبل");
                IntPtr stylePtr = GetWindowLongPtr(h, GWL_STYLE);
                long style = stylePtr.ToInt64();
                style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_POPUP);
                style |= WS_CHILD | WS_VISIBLE;
                SetWindowLongPtr(h, GWL_STYLE, new IntPtr(style));
                SetParent(h, hostPanel.Handle);
                ResizeHostedWindow();
                SendMessage(h, WM_SETICON, new IntPtr(ICON_SMALL), Icon.Handle);
                SendMessage(h, WM_SETICON, new IntPtr(ICON_BIG), Icon.Handle);
            }
            catch { }
        }

        private async void WatchdogTimer_Tick(object sender, EventArgs e)
        {
            if (coreProcess == null || coreProcess.HasExited) return;
            AutoAcceptLegacyDemo(coreProcess.Id);
            RetitleProcessWindows(coreProcess.Id);
            UpdateShellLicenseText();

            if (DateTime.UtcNow >= nextOnlineCheckUtc && !verifying && !string.IsNullOrEmpty(activeKey))
            {
                verifying = true;
                nextOnlineCheckUtc = DateTime.UtcNow.AddMinutes(5);
                VerificationResult result = null;
                try { result = await Task.Run(delegate { return VerifyOnline(activeKey); }); }
                catch { result = VerificationResult.Fail("NETWORK_REQUIRED"); }
                finally { verifying = false; }

                if (result == null || !result.Valid)
                {
                    string code = result == null ? "NETWORK_REQUIRED" : result.Code;
                    StopCore();
                    BuildLicenseView();
                    keyBox.Text = activeKey ?? "";
                    SetLicenseStatus(MapError(code), false);
                    return;
                }

                activeReply = result.Reply;
                activeRawReply = result.Raw;
                UpdateShellLicenseText();
            }
        }

        private bool VerifyCoreIntegrity()
        {
            try
            {
                string core = FindCorePath();
                string common = Path.Combine(Path.GetDirectoryName(core), "MNCommonLibrary.dll");
                return string.Equals(Sha256File(core), CoreHash, StringComparison.OrdinalIgnoreCase) &&
                       File.Exists(common) &&
                       string.Equals(Sha256File(common), CommonHash, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private string FindCorePath()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string[] names = new string[] { "MustaqbalCore.exe", "MizanNet2.exe" };
            foreach (string n in names)
            {
                string p = Path.Combine(dir, n);
                if (File.Exists(p)) return p;
            }
            throw new FileNotFoundException("Core executable not found");
        }

        private void StopCore()
        {
            watchdogTimer.Stop();
            resizeTimer.Stop();
            try
            {
                if (coreProcess != null && !coreProcess.HasExited)
                {
                    coreProcess.CloseMainWindow();
                    if (!coreProcess.WaitForExit(1200)) coreProcess.Kill();
                }
            }
            catch { }
            coreProcess = null;
            hostedWindow = IntPtr.Zero;
        }

        private void ResizeHostedWindow()
        {
            if (hostPanel == null || hostedWindow == IntPtr.Zero) return;
            try
            {
                SetWindowPos(hostedWindow, IntPtr.Zero, 0, 0,
                    Math.Max(100, hostPanel.ClientSize.Width), Math.Max(100, hostPanel.ClientSize.Height),
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
            catch { }
        }

        private void UpdateRemainingLabel(LicenseReply r)
        {
            if (remainingLabel == null || r == null) return;
            remainingLabel.Text = "المدة المتبقية: " + FormatRemaining(r.remaining_seconds);
        }

        private void UpdateShellLicenseText()
        {
            if (shellLicenseLabel == null || activeReply == null) return;
            long? remain = activeReply.remaining_seconds;
            if (!string.IsNullOrEmpty(activeReply.license_expires_at))
            {
                DateTime exp;
                if (DateTime.TryParse(activeReply.license_expires_at, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal, out exp))
                    remain = Math.Max(0L, (long)(exp.ToUniversalTime() - DateTime.UtcNow).TotalSeconds);
            }
            shellLicenseLabel.Text = "اشتراك فعّال • المتبقي: " + FormatRemaining(remain);
        }

        private string FormatRemaining(long? seconds)
        {
            if (!seconds.HasValue) return "بدون انتهاء";
            long s = Math.Max(0, seconds.Value);
            long days = s / 86400;
            long hours = (s % 86400) / 3600;
            long minutes = (s % 3600) / 60;
            if (days > 0) return days.ToString() + " يوم و " + hours.ToString() + " ساعة";
            if (hours > 0) return hours.ToString() + " ساعة و " + minutes.ToString() + " دقيقة";
            return minutes.ToString() + " دقيقة";
        }

        private void SetLicenseStatus(string text, bool ok)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text;
            statusLabel.ForeColor = ok ? Color.FromArgb(21, 126, 91) : Color.FromArgb(200, 61, 61);
        }

        private string MapError(string code)
        {
            switch ((code ?? "").ToUpperInvariant())
            {
                case "INVALID_KEY": return "الكود غير موجود أو غير صحيح";
                case "EXPIRED": return "انتهى الاشتراك. أدخل كوداً جديداً للمتابعة";
                case "REVOKED": return "تم إيقاف هذا الاشتراك";
                case "DEVICE_LIMIT": return "تم الوصول للحد الأقصى للأجهزة لهذا الكود";
                case "RATE_LIMITED": return "محاولات كثيرة. انتظر قليلاً ثم حاول مجدداً";
                case "CLIENT_NOT_ALLOWED": return "هذه النسخة غير معتمدة. ثبّت النسخة الرسمية من المستقبل";
                case "BAD_SIGNATURE": return "فشل التحقق الأمني من استجابة الخادم";
                case "STALE_TOKEN": return "انتهت صلاحية جلسة التحقق. أعد المحاولة";
                case "NETWORK_REQUIRED": return "تعذر الاتصال بخادم الترخيص. يلزم اتصال إنترنت";
                default: return "تعذر التحقق من الاشتراك. تحقق من الاتصال والكود";
            }
        }

        private bool VerifySignature(string canonical, string signature)
        {
            try
            {
                byte[] sig = Base64UrlDecode(signature);
                if (sig.Length != 64) return false;
                ECParameters p = new ECParameters();
                p.Curve = ECCurve.NamedCurves.nistP256;
                p.Q = new ECPoint();
                p.Q.X = Base64UrlDecode(PubX);
                p.Q.Y = Base64UrlDecode(PubY);
                using (ECDsaCng ecdsa = new ECDsaCng())
                {
                    ecdsa.ImportParameters(p);
                    return ecdsa.VerifyData(Encoding.UTF8.GetBytes(canonical), sig, HashAlgorithmName.SHA256);
                }
            }
            catch { return false; }
        }

        private string DeviceId()
        {
            string machineGuid = "";
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey k = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    if (k != null) machineGuid = Convert.ToString(k.GetValue("MachineGuid")) ?? "";
            }
            catch { }
            if (machineGuid.Length == 0)
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                    using (RegistryKey k = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                        if (k != null) machineGuid = Convert.ToString(k.GetValue("MachineGuid")) ?? "";
                }
                catch { }
            }

            uint serial = 0, maxLen = 0, flags = 0;
            StringBuilder volume = new StringBuilder(260);
            StringBuilder fs = new StringBuilder(260);
            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory);
                GetVolumeInformation(root, volume, volume.Capacity, out serial, out maxLen, out flags, fs, fs.Capacity);
            }
            catch { }

            return Sha256Text(machineGuid + "|" + serial.ToString("X8") + "|" + Environment.MachineName);
        }

        private static string Sha256File(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream f = File.OpenRead(path))
                return Hex(sha.ComputeHash(f));
        }

        private static string Sha256Text(string text)
        {
            using (SHA256 sha = SHA256.Create())
                return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? "")));
        }

        private static string Hex(byte[] b)
        {
            StringBuilder s = new StringBuilder(b.Length * 2);
            for (int i = 0; i < b.Length; i++) s.Append(b[i].ToString("x2"));
            return s.ToString();
        }

        private static string CreateNonce()
        {
            byte[] b = new byte[32];
            using (RandomNumberGenerator r = RandomNumberGenerator.Create()) r.GetBytes(b);
            return Base64UrlEncode(b);
        }

        private static string Base64UrlEncode(byte[] b)
        {
            return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] Base64UrlDecode(string s)
        {
            s = (s ?? "").Replace('-', '+').Replace('_', '/');
            while ((s.Length % 4) != 0) s += "=";
            return Convert.FromBase64String(s);
        }

        private static string DecodeEndpoint()
        {
            string a = "aHR0cHM6Ly9hd3ItbGljZW5zZS12ZXJjZWwudmVyY2VsLmFwcA==";
            string b = "L2FwaS9tdXN0YXFiYWwtdmVyaWZ5";
            return Encoding.UTF8.GetString(Convert.FromBase64String(a)) + Encoding.UTF8.GetString(Convert.FromBase64String(b));
        }

        private static string StoragePath()
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AlMustaqbal");
            Directory.CreateDirectory(d);
            return Path.Combine(d, "license.dat");
        }

        private static void SaveKey(string key)
        {
            try
            {
                byte[] clear = Encoding.UTF8.GetBytes(key ?? "");
                byte[] entropy = Encoding.UTF8.GetBytes("AlMustaqbal-License-v1");
                byte[] enc = ProtectedData.Protect(clear, entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(StoragePath(), enc);
            }
            catch { }
        }

        private static string LoadSavedKey()
        {
            try
            {
                string p = StoragePath();
                if (!File.Exists(p)) return "";
                byte[] entropy = Encoding.UTF8.GetBytes("AlMustaqbal-License-v1");
                byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(p), entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clear);
            }
            catch { return ""; }
        }

        private void AutoAcceptLegacyDemo(int pid)
        {
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint p;
                GetWindowThreadProcessId(h, out p);
                if (p != (uint)pid || !IsWindowVisible(h)) return true;
                EnumChildWindows(h, delegate(IntPtr c, IntPtr lp)
                {
                    string text = GetText(c);
                    if (ContainsDemoText(text)) SendMessage(c, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);
        }

        private void RetitleProcessWindows(int pid)
        {
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint p;
                GetWindowThreadProcessId(h, out p);
                if (p != (uint)pid || !IsWindowVisible(h)) return true;
                string t = GetText(h);
                if (!ContainsDemoText(t))
                {
                    if (t.IndexOf("Mizan", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("ميزان", StringComparison.OrdinalIgnoreCase) >= 0)
                        SetWindowText(h, "المستقبل");
                    SendMessage(h, WM_SETICON, new IntPtr(ICON_SMALL), Icon.Handle);
                }
                return true;
            }, IntPtr.Zero);
        }

        private static bool ContainsDemoText(string text)
        {
            text = text ?? "";
            return text.IndexOf("نسخة عرض", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("تشغيل كنسخة", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Demo", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetText(IntPtr h)
        {
            int n = GetWindowTextLength(h);
            StringBuilder b = new StringBuilder(Math.Max(2, n + 2));
            GetWindowText(h, b, b.Capacity);
            return b.ToString();
        }

        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragStart = Cursor.Position;
            }
        }

        private void Header_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging || WindowState == FormWindowState.Maximized) return;
            Point now = Cursor.Position;
            Location = new Point(Location.X + now.X - dragStart.X, Location.Y + now.Y - dragStart.Y);
            dragStart = now;
        }

        private void Header_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        private static Region MakeRoundRegion(int w, int h, int radius)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                int d = radius * 2;
                p.AddArc(0, 0, d, d, 180, 90);
                p.AddArc(w - d, 0, d, d, 270, 90);
                p.AddArc(w - d, h - d, d, d, 0, 90);
                p.AddArc(0, h - d, d, d, 90, 90);
                p.CloseFigure();
                return new Region(p);
            }
        }

        private sealed class VerificationResult
        {
            public bool Valid;
            public string Code;
            public LicenseReply Reply;
            public string Raw;

            public static VerificationResult Ok(LicenseReply reply, string raw)
            {
                VerificationResult r = new VerificationResult();
                r.Valid = true;
                r.Code = "VALID";
                r.Reply = reply;
                r.Raw = raw;
                return r;
            }

            public static VerificationResult Fail(string code)
            {
                VerificationResult r = new VerificationResult();
                r.Valid = false;
                r.Code = code ?? "SERVER_ERROR";
                return r;
            }
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int GWL_STYLE = -16;
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_MINIMIZEBOX = 0x00020000L;
        private const long WS_MAXIMIZEBOX = 0x00010000L;
        private const long WS_POPUP = 0x80000000L;
        private const long WS_CHILD = 0x40000000L;
        private const long WS_VISIBLE = 0x10000000L;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int BM_CLICK = 0x00F5;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeInformation(string rootPathName, StringBuilder volumeNameBuffer, int volumeNameSize,
            out uint volumeSerialNumber, out uint maximumComponentLength, out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer, int nFileSystemNameSize);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }
        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
