using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace AlMustaqbal
{
    internal sealed partial class MainForm : Form
    {
        private const string AppVersion = "2.0.0";
        private const string Protocol = "mustaqbal-license-v1";
        private const string Algorithm = "ES256-P1363";
        private const string KeyId = "82f6e932a8d975f00adf";
        private const string PubX = "ODac4HCfHNJTEn8vRsBLjySvGRXkgcsWcsG5ivpCfuI";
        private const string PubY = "Cyy0BkZECkmfh5q_pdunajsw9Pa7QmssZYmXS4IX9n8";
        private const string CoreHash = "349634fbb4fc331344c126fa238929dada30a22a95ce481a29c81ac946524ba8";
        private const string CommonHash = "7c53c355f40d6fa56ac1279edfc991bd060e38820d5505c23535fea27cb7c2e1";
        private const string CoreArguments = "mnf pos vm fa mj hr arc lr pk of re fs rs ot1 ot2";

        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private TextBox keyBox;
        private Button verifyButton;
        private Label statusLabel;
        private Label remainingLabel;
        private Label shellLicenseLabel;
        private Label clockLabel;
        private Panel contentPanel;
        private Panel hostPanel;
        private Panel dashboardOverlay;
        private Label dashboardStatusLabel;
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

        public MainForm()
        {
            Text = "المستقبل";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(960, 620);
            Size = new Size(1080, 700);
            BackColor = Color.FromArgb(241, 246, 253);
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.None;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            DoubleBuffered = true;

            watchdogTimer = new Timer();
            watchdogTimer.Interval = 2500;
            watchdogTimer.Tick += WatchdogTimer_Tick;
            resizeTimer = new Timer();
            resizeTimer.Interval = 250;
            resizeTimer.Tick += delegate { ResizeHostedWindow(); };

            BuildLicenseView();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            string saved = LoadSavedKey();
            if (!string.IsNullOrWhiteSpace(saved) && keyBox != null)
            {
                keyBox.Text = saved;
                BeginVerify(saved, true);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopCore();
            base.OnFormClosing(e);
        }

        private void BuildLicenseView()
        {
            TopMost = false;
            Controls.Clear();
            hostedWindow = IntPtr.Zero;
            dashboardOverlay = null;
            hostPanel = null;
            WindowState = FormWindowState.Normal;
            Size = new Size(1080, 700);
            CenterToScreen();

            contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Color.FromArgb(241, 246, 253);
            Controls.Add(contentPanel);

            GradientPanel header = BuildHeader(false);
            Controls.Add(header);
            header.BringToFront();

            RoundedPanel card = new RoundedPanel();
            card.Size = new Size(590, 410);
            card.BackColor = Color.White;
            card.Anchor = AnchorStyles.None;
            contentPanel.Controls.Add(card);
            Action place = delegate
            {
                card.Left = Math.Max(20, (contentPanel.ClientSize.Width - card.Width) / 2);
                card.Top = Math.Max(28, (contentPanel.ClientSize.Height - card.Height) / 2);
            };
            contentPanel.Resize += delegate { place(); };
            place();

            Label badge = new Label();
            badge.Text = "M";
            badge.TextAlign = ContentAlignment.MiddleCenter;
            badge.Font = new Font("Segoe UI", 23f, FontStyle.Bold);
            badge.ForeColor = Color.White;
            badge.BackColor = Color.FromArgb(25, 103, 231);
            badge.SetBounds(263, 30, 64, 64);
            badge.Region = MakeRoundRegion(64, 64, 32);
            card.Controls.Add(badge);

            Label title = NewLabel("تفعيل المستقبل", 21f, FontStyle.Bold, Color.FromArgb(23, 42, 78));
            title.TextAlign = ContentAlignment.MiddleCenter;
            title.SetBounds(45, 108, 500, 44);
            card.Controls.Add(title);

            Label sub = NewLabel("أدخل كود الاشتراك للمتابعة إلى النظام", 10.5f, FontStyle.Regular, Color.FromArgb(98, 112, 139));
            sub.TextAlign = ContentAlignment.MiddleCenter;
            sub.SetBounds(45, 152, 500, 28);
            card.Controls.Add(sub);

            Label keyLabel = NewLabel("كود الاشتراك", 10f, FontStyle.Bold, Color.FromArgb(45, 60, 88));
            keyLabel.TextAlign = ContentAlignment.MiddleRight;
            keyLabel.RightToLeft = RightToLeft.Yes;
            keyLabel.SetBounds(65, 201, 460, 24);
            card.Controls.Add(keyLabel);

            keyBox = new TextBox();
            keyBox.Font = new Font("Segoe UI", 12f);
            keyBox.BorderStyle = BorderStyle.FixedSingle;
            keyBox.TextAlign = HorizontalAlignment.Center;
            keyBox.SetBounds(65, 230, 460, 38);
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
            verifyButton.SetBounds(65, 286, 460, 46);
            verifyButton.Click += delegate { BeginVerify(keyBox.Text, false); };
            card.Controls.Add(verifyButton);

            statusLabel = NewLabel("اتصال آمن • ترخيص مرتبط بالجهاز", 9.5f, FontStyle.Regular, Color.FromArgb(105, 118, 143));
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            statusLabel.SetBounds(45, 342, 500, 24);
            card.Controls.Add(statusLabel);

            remainingLabel = NewLabel("", 9.5f, FontStyle.Bold, Color.FromArgb(25, 103, 231));
            remainingLabel.TextAlign = ContentAlignment.MiddleCenter;
            remainingLabel.SetBounds(45, 369, 500, 24);
            card.Controls.Add(remainingLabel);
        }

        private GradientPanel BuildHeader(bool shell)
        {
            GradientPanel header = new GradientPanel();
            header.Dock = DockStyle.Top;
            header.Height = shell ? 82 : 104;
            header.MouseDown += Header_MouseDown;
            header.MouseMove += Header_MouseMove;
            header.MouseUp += Header_MouseUp;

            Label mark = NewLabel("M", shell ? 16f : 20f, FontStyle.Bold, Color.FromArgb(18, 88, 214));
            mark.BackColor = Color.White;
            mark.TextAlign = ContentAlignment.MiddleCenter;
            mark.SetBounds(24, shell ? 16 : 24, shell ? 48 : 56, shell ? 48 : 56);
            mark.Region = MakeRoundRegion(mark.Width, mark.Height, mark.Width / 2);
            header.Controls.Add(mark);

            Label name = NewLabel("المستقبل", shell ? 19f : 28f, FontStyle.Bold, Color.White);
            name.BackColor = Color.Transparent;
            name.TextAlign = ContentAlignment.MiddleLeft;
            name.RightToLeft = RightToLeft.Yes;
            name.SetBounds(86, shell ? 8 : 13, 300, shell ? 42 : 48);
            name.MouseDown += Header_MouseDown;
            name.MouseMove += Header_MouseMove;
            name.MouseUp += Header_MouseUp;
            header.Controls.Add(name);

            Label slogan = NewLabel(shell ? "نظام إدارة الأعمال" : "إدارة أعمالك برؤية أوضح", 9.5f, FontStyle.Regular, Color.FromArgb(224, 239, 255));
            slogan.BackColor = Color.Transparent;
            slogan.SetBounds(88, shell ? 44 : 61, 310, 25);
            header.Controls.Add(slogan);

            if (shell)
            {
                Button home = BuildNavButton("الرئيسية");
                home.SetBounds(400, 22, 94, 38);
                home.Click += delegate { ShowHomeDashboard(); };
                header.Controls.Add(home);

                Button work = BuildNavButton("مساحة العمل");
                work.SetBounds(504, 22, 116, 38);
                work.Click += delegate { ShowLegacyWorkspace(); };
                header.Controls.Add(work);

                shellLicenseLabel = NewLabel("اشتراك فعّال", 9f, FontStyle.Bold, Color.White);
                shellLicenseLabel.BackColor = Color.FromArgb(45, 255, 255, 255);
                shellLicenseLabel.TextAlign = ContentAlignment.MiddleCenter;
                shellLicenseLabel.SetBounds(638, 22, 290, 38);
                shellLicenseLabel.Region = MakeRoundRegion(290, 38, 12);
                header.Controls.Add(shellLicenseLabel);

                clockLabel = NewLabel(DateTime.Now.ToString("yyyy/MM/dd  HH:mm"), 9f, FontStyle.Regular, Color.FromArgb(228, 240, 255));
                clockLabel.BackColor = Color.Transparent;
                clockLabel.TextAlign = ContentAlignment.MiddleCenter;
                clockLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                clockLabel.SetBounds(Width - 330, 22, 160, 38);
                header.Controls.Add(clockLabel);
            }

            Button close = BuildWindowButton("×", Color.FromArgb(201, 54, 64), Color.White);
            close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            close.Location = new Point(Width - 50, 18);
            close.Click += delegate { Close(); };
            header.Controls.Add(close);

            Button min = BuildWindowButton("—", Color.White, Color.FromArgb(26, 77, 158));
            min.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            min.Location = new Point(Width - 96, 18);
            min.Click += delegate { WindowState = FormWindowState.Minimized; };
            header.Controls.Add(min);

            Button max = BuildWindowButton("□", Color.White, Color.FromArgb(26, 77, 158));
            max.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            max.Location = new Point(Width - 142, 18);
            max.Visible = shell;
            max.Click += delegate
            {
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                ResizeHostedWindow();
                ResizeDashboard();
            };
            header.Controls.Add(max);
            return header;
        }

        private Label NewLabel(string text, float size, FontStyle style, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", size, style);
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            return l;
        }

        private Button BuildNavButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(105, 177, 255);
            b.BackColor = Color.FromArgb(22, 101, 218);
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private Button BuildWindowButton(string text, Color back, Color fore)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            b.Size = new Size(38, 32);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private void Header_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { dragging = true; dragStart = Cursor.Position; }
        }
        private void Header_MouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging || WindowState == FormWindowState.Maximized) return;
            Point now = Cursor.Position;
            Location = new Point(Location.X + now.X - dragStart.X, Location.Y + now.Y - dragStart.Y);
            dragStart = now;
        }
        private void Header_MouseUp(object sender, MouseEventArgs e) { dragging = false; }

        private static Region MakeRoundRegion(int w, int h, int radius)
        {
            using (System.Drawing.Drawing2D.GraphicsPath p = new System.Drawing.Drawing2D.GraphicsPath())
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
    }
}
