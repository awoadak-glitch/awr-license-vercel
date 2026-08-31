using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AlMustaqbal
{
    internal sealed partial class MainForm
    {
        private void EnterApplicationShell()
        {
            if (!VerifyCoreIntegrity())
            {
                BuildLicenseView();
                SetLicenseStatus("فشل فحص سلامة ملفات البرنامج. أعد تثبيت نسخة المستقبل الأصلية.", false);
                return;
            }

            Controls.Clear();
            BackColor = Color.FromArgb(239, 245, 252);
            hostPanel = new Panel();
            hostPanel.Dock = DockStyle.Fill;
            hostPanel.BackColor = Color.FromArgb(239, 245, 252);
            Controls.Add(hostPanel);
            hostPanel.Resize += delegate { ResizeHostedWindow(); ResizeDashboard(); };

            GradientPanel header = BuildHeader(true);
            Controls.Add(header);
            header.BringToFront();

            WindowState = FormWindowState.Maximized;
            TopMost = true;
            UpdateShellLicenseText();
            BuildDashboard("جاري تشغيل نظام المستقبل والاتصال بقاعدة البيانات...");

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
                coreProcess.Exited += delegate { try { BeginInvoke((MethodInvoker)delegate { Close(); }); } catch { } };
                nextOnlineCheckUtc = DateTime.UtcNow.AddMinutes(5);
                watchdogTimer.Start();
                resizeTimer.Start();
                Task.Run(delegate { AttachCoreWindow(); });
            }
            catch
            {
                TopMost = false;
                BuildLicenseView();
                SetLicenseStatus("تعذر تشغيل ملفات النظام الداخلية.", false);
            }
        }

        private void AttachCoreWindow()
        {
            if (coreProcess == null) return;
            try { coreProcess.WaitForInputIdle(12000); } catch { }
            IntPtr last = IntPtr.Zero;
            int stable = 0;
            for (int i = 0; i < 240; i++)
            {
                if (coreProcess == null || coreProcess.HasExited) return;
                HideLegacySplash(coreProcess.Id);
                AutoAcceptLegacyDemo(coreProcess.Id);
                AutoConnectLegacyDatabase(coreProcess.Id);
                RetitleProcessWindows(coreProcess.Id);
                IntPtr h = FindLegacyMainWindow(coreProcess.Id);
                if (h != IntPtr.Zero)
                {
                    if (h == last) stable++; else { last = h; stable = 1; }
                    if (stable >= 4)
                    {
                        try { BeginInvoke((MethodInvoker)delegate { HostWindow(h); }); } catch { }
                        return;
                    }
                }
                else { last = IntPtr.Zero; stable = 0; }
                System.Threading.Thread.Sleep(250);
            }
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    TopMost = false;
                    if (dashboardStatusLabel != null) dashboardStatusLabel.Text = "تعذر فتح مساحة العمل تلقائياً. أعد تشغيل البرنامج.";
                });
            }
            catch { }
        }

        private void HostWindow(IntPtr h)
        {
            if (hostPanel == null || h == IntPtr.Zero) return;
            hostedWindow = h;
            try
            {
                SetWindowText(h, "المستقبل - نظام إدارة الأعمال");
                long style = GetWindowLongPtr(h, GWL_STYLE).ToInt64();
                style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_POPUP);
                style |= WS_CHILD | WS_VISIBLE;
                SetWindowLongPtr(h, GWL_STYLE, new IntPtr(style));
                SetParent(h, hostPanel.Handle);
                ResizeHostedWindow();
                SendMessage(h, WM_SETICON, new IntPtr(ICON_SMALL), Icon.Handle);
                SendMessage(h, WM_SETICON, new IntPtr(ICON_BIG), Icon.Handle);
                TopMost = false;
                BuildDashboard("النظام جاهز للعمل");
                ShowHomeDashboard();
            }
            catch { TopMost = false; }
        }

        private void BuildDashboard(string status)
        {
            if (hostPanel == null) return;
            if (dashboardOverlay != null)
            {
                if (dashboardStatusLabel != null) dashboardStatusLabel.Text = status;
                ShowHomeDashboard();
                return;
            }

            dashboardOverlay = new Panel();
            dashboardOverlay.BackColor = Color.FromArgb(239, 245, 252);
            hostPanel.Controls.Add(dashboardOverlay);

            GradientPanel hero = new GradientPanel();
            hero.Name = "hero";
            hero.SetBounds(32, 28, 960, 172);
            dashboardOverlay.Controls.Add(hero);

            Label heroTitle = NewLabel("مرحباً بك في المستقبل", 25f, FontStyle.Bold, Color.White);
            heroTitle.TextAlign = ContentAlignment.MiddleRight;
            heroTitle.RightToLeft = RightToLeft.Yes;
            heroTitle.SetBounds(48, 25, 810, 48);
            hero.Controls.Add(heroTitle);

            Label heroSub = NewLabel("إدارة الحسابات والمبيعات والمخزون والموارد من مكان واحد", 11f, FontStyle.Regular, Color.FromArgb(225, 239, 255));
            heroSub.TextAlign = ContentAlignment.MiddleRight;
            heroSub.RightToLeft = RightToLeft.Yes;
            heroSub.SetBounds(48, 75, 810, 30);
            hero.Controls.Add(heroSub);

            dashboardStatusLabel = NewLabel(status, 10f, FontStyle.Bold, Color.White);
            dashboardStatusLabel.BackColor = Color.FromArgb(45, 255, 255, 255);
            dashboardStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
            dashboardStatusLabel.SetBounds(48, 121, 430, 32);
            dashboardStatusLabel.Region = MakeRoundRegion(430, 32, 10);
            hero.Controls.Add(dashboardStatusLabel);

            Button openSystem = new Button();
            openSystem.Text = "فتح مساحة العمل";
            openSystem.FlatStyle = FlatStyle.Flat;
            openSystem.FlatAppearance.BorderSize = 0;
            openSystem.BackColor = Color.White;
            openSystem.ForeColor = Color.FromArgb(18, 83, 202);
            openSystem.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            openSystem.Cursor = Cursors.Hand;
            openSystem.SetBounds(710, 116, 180, 40);
            openSystem.Click += delegate { ShowLegacyWorkspace(); };
            hero.Controls.Add(openSystem);

            string[] titles = { "الحسابات", "المبيعات", "المخزون", "الموظفون", "المركبات", "التقارير" };
            string[] desc = {
                "القيود والحسابات والتقارير المالية",
                "الفواتير والمبيعات ومتابعة العملاء",
                "المواد والمستودعات وحركة المخزون",
                "الموظفون والموارد البشرية",
                "المركبات والمتابعة التشغيلية",
                "تقارير شاملة لدعم القرار"
            };
            for (int i = 0; i < titles.Length; i++)
            {
                RoundedPanel card = CreateDashboardCard(titles[i], desc[i]);
                card.Name = "card" + i;
                dashboardOverlay.Controls.Add(card);
            }

            RoundedPanel footer = new RoundedPanel();
            footer.Name = "footer";
            footer.BackColor = Color.White;
            dashboardOverlay.Controls.Add(footer);
            Label footerText = NewLabel("المستقبل • نظام أعمال متكامل • ترخيص آمن مرتبط بالخادم", 10f, FontStyle.Bold, Color.FromArgb(58, 75, 107));
            footerText.TextAlign = ContentAlignment.MiddleCenter;
            footerText.Dock = DockStyle.Fill;
            footer.Controls.Add(footerText);

            ResizeDashboard();
            ShowHomeDashboard();
        }

        private RoundedPanel CreateDashboardCard(string titleText, string descText)
        {
            RoundedPanel card = new RoundedPanel();
            card.BackColor = Color.White;
            card.Cursor = Cursors.Hand;
            Panel accent = new Panel();
            accent.BackColor = Color.FromArgb(31, 116, 239);
            accent.Dock = DockStyle.Right;
            accent.Width = 7;
            card.Controls.Add(accent);

            Label title = NewLabel(titleText, 15f, FontStyle.Bold, Color.FromArgb(27, 48, 82));
            title.TextAlign = ContentAlignment.MiddleRight;
            title.RightToLeft = RightToLeft.Yes;
            title.SetBounds(22, 20, 235, 34);
            card.Controls.Add(title);
            Label desc = NewLabel(descText, 9.3f, FontStyle.Regular, Color.FromArgb(103, 116, 141));
            desc.TextAlign = ContentAlignment.TopRight;
            desc.RightToLeft = RightToLeft.Yes;
            desc.SetBounds(22, 62, 235, 50);
            card.Controls.Add(desc);
            EventHandler open = delegate { ShowLegacyWorkspace(); };
            card.Click += open; title.Click += open; desc.Click += open;
            return card;
        }

        private void ResizeDashboard()
        {
            if (dashboardOverlay == null || hostPanel == null) return;
            dashboardOverlay.SetBounds(0, 0, hostPanel.ClientSize.Width, hostPanel.ClientSize.Height);
            Control hero = dashboardOverlay.Controls["hero"];
            if (hero != null) hero.SetBounds(32, 28, Math.Max(760, hostPanel.ClientSize.Width - 64), 172);

            int cardW = Math.Max(255, (hostPanel.ClientSize.Width - 104) / 3);
            int cardH = 132;
            int gap = 20;
            int y = 220;
            for (int i = 0; i < 6; i++)
            {
                Control c = dashboardOverlay.Controls["card" + i];
                if (c == null) continue;
                int row = i / 3, col = i % 3;
                c.SetBounds(32 + col * (cardW + gap), y + row * (cardH + gap), cardW, cardH);
            }
            Control footer = dashboardOverlay.Controls["footer"];
            if (footer != null) footer.SetBounds(32, Math.Max(520, hostPanel.ClientSize.Height - 96), Math.Max(760, hostPanel.ClientSize.Width - 64), 66);
        }

        private void ShowHomeDashboard()
        {
            if (dashboardOverlay == null) return;
            dashboardOverlay.Visible = true;
            ResizeDashboard();
            dashboardOverlay.BringToFront();
            try { SetWindowPos(dashboardOverlay.Handle, HWND_TOP, 0, 0, dashboardOverlay.Width, dashboardOverlay.Height, SWP_SHOWWINDOW); } catch { }
        }

        private void ShowLegacyWorkspace()
        {
            if (dashboardOverlay != null) dashboardOverlay.Visible = false;
            if (hostedWindow != IntPtr.Zero)
            {
                ResizeHostedWindow();
                try { SetFocus(hostedWindow); } catch { }
            }
        }

        private async void WatchdogTimer_Tick(object sender, EventArgs e)
        {
            if (clockLabel != null) clockLabel.Text = DateTime.Now.ToString("yyyy/MM/dd  HH:mm");
            if (coreProcess == null || coreProcess.HasExited) return;
            HideLegacySplash(coreProcess.Id);
            AutoAcceptLegacyDemo(coreProcess.Id);
            AutoConnectLegacyDatabase(coreProcess.Id);
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
                    if (keyBox != null) keyBox.Text = activeKey ?? "";
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
                       File.Exists(common) && string.Equals(Sha256File(common), CommonHash, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private string FindCorePath()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string p = Path.Combine(dir, "MustaqbalCore.exe");
            if (File.Exists(p)) return p;
            p = Path.Combine(dir, "MizanNet2.exe");
            if (File.Exists(p)) return p;
            throw new FileNotFoundException("Core executable not found");
        }

        private void StopCore()
        {
            if (watchdogTimer != null) watchdogTimer.Stop();
            if (resizeTimer != null) resizeTimer.Stop();
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
                SetWindowPos(hostedWindow, HWND_BOTTOM, 0, 0,
                    Math.Max(100, hostPanel.ClientSize.Width), Math.Max(100, hostPanel.ClientSize.Height),
                    SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
            }
            catch { }
        }
    }
}
