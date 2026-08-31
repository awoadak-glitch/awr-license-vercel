using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Reflection;

[assembly: AssemblyTitle("المستقبل")]
[assembly: AssemblyDescription("نظام المستقبل لإدارة الأعمال")]
[assembly: AssemblyCompany("المستقبل")]
[assembly: AssemblyProduct("المستقبل")]
[assembly: AssemblyCopyright("Copyright © المستقبل 2026")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace AlMustaqbal
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
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

    internal sealed class VerificationResult
    {
        public bool Valid;
        public string Code;
        public LicenseReply Reply;
        public string Raw;
        public static VerificationResult Ok(LicenseReply reply, string raw)
        {
            return new VerificationResult { Valid = true, Code = "VALID", Reply = reply, Raw = raw };
        }
        public static VerificationResult Fail(string code)
        {
            return new VerificationResult { Valid = false, Code = code ?? "SERVER_ERROR" };
        }
    }

    internal sealed class GradientPanel : Panel
    {
        public Color Color1 = Color.FromArgb(8, 58, 160);
        public Color Color2 = Color.FromArgb(38, 132, 255);
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0) return;
            using (LinearGradientBrush b = new LinearGradientBrush(ClientRectangle, Color1, Color2, 20f))
                e.Graphics.FillRectangle(b, ClientRectangle);
        }
    }

    internal sealed class RoundedPanel : Panel
    {
        public int Radius = 16;
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width < 4 || Height < 4) return;
            int r = Math.Min(Radius, Math.Min(Width, Height) / 2 - 1);
            if (r < 2) return;
            using (GraphicsPath p = new GraphicsPath())
            {
                int d = r * 2;
                p.AddArc(0, 0, d, d, 180, 90);
                p.AddArc(Width - d - 1, 0, d, d, 270, 90);
                p.AddArc(Width - d - 1, Height - d - 1, d, d, 0, 90);
                p.AddArc(0, Height - d - 1, d, d, 90, 90);
                p.CloseFigure();
                Region = new Region(p);
            }
        }
    }
}
