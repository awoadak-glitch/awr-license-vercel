using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace AlMustaqbal
{
    internal sealed partial class MainForm
    {
        private async void BeginVerify(string key, bool automatic)
        {
            if (verifying) return;
            key = (key ?? "").Trim();
            if (key.Length == 0) { SetLicenseStatus("أدخل كود الاشتراك أولاً", false); return; }
            verifying = true;
            if (verifyButton != null) verifyButton.Enabled = false;
            SetLicenseStatus("جاري التحقق الآمن من الاشتراك...", true);
            try
            {
                VerificationResult result = await Task.Run(delegate { return VerifyOnline(key); });
                if (!result.Valid)
                {
                    SetLicenseStatus(MapError(result.Code), false);
                    return;
                }
                activeKey = key;
                activeReply = result.Reply;
                activeRawReply = result.Raw;
                SaveKey(key);
                UpdateRemainingLabel(result.Reply);
                SetLicenseStatus("تم التحقق بنجاح", true);
                await Task.Delay(450);
                EnterApplicationShell();
            }
            catch (Exception)
            {
                SetLicenseStatus("تعذر الاتصال بخادم الترخيص. يلزم اتصال إنترنت.", false);
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

            byte[] payload = Encoding.UTF8.GetBytes(serializer.Serialize(body));
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(DecodeEndpoint());
            req.Method = "POST";
            req.ContentType = "application/json; charset=utf-8";
            req.Accept = "application/json";
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            req.UserAgent = "AlMustaqbal/" + AppVersion;
            req.ContentLength = payload.Length;
            using (Stream s = req.GetRequestStream()) s.Write(payload, 0, payload.Length);

            string raw;
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)req.GetResponse())
                using (StreamReader r = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) raw = r.ReadToEnd();
            }
            catch (WebException ex)
            {
                if (ex.Response == null) throw;
                using (StreamReader r = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) raw = r.ReadToEnd();
            }

            LicenseReply reply = serializer.Deserialize<LicenseReply>(raw);
            if (reply == null || !reply.success) return VerificationResult.Fail(reply == null ? "SERVER_ERROR" : reply.code);
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
                Protocol, reply.status, reply.license_id, reply.device_fingerprint, reply.nonce,
                reply.app_version, reply.client_hash, reply.issued_at, reply.token_expires_at,
                string.IsNullOrEmpty(reply.license_expires_at) ? "" : reply.license_expires_at,
                reply.remaining_seconds.HasValue ? (object)reply.remaining_seconds.Value : (object)(-1L)
            });
            if (!VerifySignature(canonical, reply.signature)) return VerificationResult.Fail("BAD_SIGNATURE");
            return VerificationResult.Ok(reply, raw);
        }

        private bool VerifySignature(string canonical, string signature)
        {
            try
            {
                byte[] sig = Base64UrlDecode(signature);
                if (sig.Length != 64) return false;
                ECParameters p = new ECParameters();
                p.Curve = ECCurve.NamedCurves.nistP256;
                p.Q = new ECPoint { X = Base64UrlDecode(PubX), Y = Base64UrlDecode(PubY) };
                using (ECDsaCng ecdsa = new ECDsaCng())
                {
                    ecdsa.ImportParameters(p);
                    return ecdsa.VerifyData(Encoding.UTF8.GetBytes(canonical), sig, HashAlgorithmName.SHA256);
                }
            }
            catch { return false; }
        }

        private void UpdateRemainingLabel(LicenseReply r)
        {
            if (remainingLabel != null && r != null) remainingLabel.Text = "المدة المتبقية: " + FormatRemaining(r.remaining_seconds);
        }

        private void UpdateShellLicenseText()
        {
            if (shellLicenseLabel == null || activeReply == null) return;
            long? remain = activeReply.remaining_seconds;
            if (!string.IsNullOrEmpty(activeReply.license_expires_at))
            {
                DateTime exp;
                if (DateTime.TryParse(activeReply.license_expires_at, out exp))
                    remain = Math.Max(0L, (long)(exp.ToUniversalTime() - DateTime.UtcNow).TotalSeconds);
            }
            shellLicenseLabel.Text = "اشتراك فعّال • المتبقي: " + FormatRemaining(remain);
        }

        private string FormatRemaining(long? seconds)
        {
            if (!seconds.HasValue) return "بدون انتهاء";
            long s = Math.Max(0L, seconds.Value);
            long days = s / 86400;
            long hours = (s % 86400) / 3600;
            long minutes = (s % 3600) / 60;
            if (days > 0) return days + " يوم و " + hours + " ساعة";
            if (hours > 0) return hours + " ساعة و " + minutes + " دقيقة";
            return minutes + " دقيقة";
        }

        private void SetLicenseStatus(string text, bool ok)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text;
            statusLabel.ForeColor = ok ? System.Drawing.Color.FromArgb(21, 126, 91) : System.Drawing.Color.FromArgb(200, 61, 61);
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
            uint serial = 0, maxLen = 0, flags = 0;
            StringBuilder volume = new StringBuilder(260), fs = new StringBuilder(260);
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
            using (FileStream f = File.OpenRead(path)) return Hex(sha.ComputeHash(f));
        }
        private static string Sha256Text(string text)
        {
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? "")));
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
            return Encoding.UTF8.GetString(Convert.FromBase64String("aHR0cHM6Ly9hd3ItbGljZW5zZS12ZXJjZWwudmVyY2VsLmFwcA==")) +
                   Encoding.UTF8.GetString(Convert.FromBase64String("L2FwaS9tdXN0YXFiYWwtdmVyaWZ5"));
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
                byte[] entropy = Encoding.UTF8.GetBytes("AlMustaqbal-License-v1");
                File.WriteAllBytes(StoragePath(), ProtectedData.Protect(Encoding.UTF8.GetBytes(key ?? ""), entropy, DataProtectionScope.CurrentUser));
            }
            catch { }
        }
        private static string LoadSavedKey()
        {
            try
            {
                if (!File.Exists(StoragePath())) return "";
                byte[] entropy = Encoding.UTF8.GetBytes("AlMustaqbal-License-v1");
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(StoragePath()), entropy, DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
        }
    }
}
