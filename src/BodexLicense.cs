using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Web.Script.Serialization;

namespace ProxiFyreUI.Infrastructure
{
    internal enum BodexLicenseState { Active, OfflineGrace, NeedsCode, Invalid }

    internal static class BodexLicense
    {
        private const string Api = "https://hb-browser-license.bodex625.workers.dev/api";
        private const long CheckIntervalMs = 24L * 60 * 60 * 1000;
        private const long OfflineGraceMs = 72L * 60 * 60 * 1000;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BODEX-PROXY-LICENSE-V1");
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static readonly string StorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BODEX Proxy", "license.dat");

        internal static Task<BodexLicenseState> CheckAsync()
        {
            return Task.Run(() => Check());
        }

        private static BodexLicenseState Check()
        {
            var saved = Load();
            if (saved == null || string.IsNullOrWhiteSpace(saved.Token))
                return BodexLicenseState.NeedsCode;

            var age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - saved.LastOk;
            if (age >= 0 && age < CheckIntervalMs)
                return BodexLicenseState.Active;

            try
            {
                var result = Send(new Dictionary<string, object>
                {
                    ["action"] = "validate",
                    ["token"] = saved.Token,
                    ["device_id"] = DeviceId()
                });
                if (GetBool(result, "ok"))
                {
                    saved.LastOk = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    Save(saved);
                    return BodexLicenseState.Active;
                }
                Delete();
                return BodexLicenseState.Invalid;
            }
            catch
            {
                return saved.LastOk > 0 && age >= 0 && age <= OfflineGraceMs
                    ? BodexLicenseState.OfflineGrace
                    : BodexLicenseState.Invalid;
            }
        }

        internal static Task<string> ActivateAsync(string code)
        {
            return Task.Run(() => Activate(code));
        }

        private static string Activate(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("أدخل كود الاشتراك.");

            var result = Send(new Dictionary<string, object>
            {
                ["action"] = "activate",
                ["code"] = code.Trim().ToUpperInvariant(),
                ["device_id"] = DeviceId()
            });

            if (!GetBool(result, "ok"))
                throw new InvalidOperationException(GetString(result, "message", "الكود غير صالح أو مرتبط بجهاز آخر."));

            var token = GetString(result, "token", "");
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("لم يصل رمز التفعيل من الخادم.");

            Save(new LicenseRecord
            {
                Token = token,
                LastOk = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
            return GetString(result, "expires_at", "اشتراك فعّال");
        }

        private static Dictionary<string, object> Send(Dictionary<string, object> values)
        {
            var request = (HttpWebRequest)WebRequest.Create(Api);
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";
            request.Timeout = 10000;
            request.ReadWriteTimeout = 10000;
            request.Proxy = WebRequest.DefaultWebProxy;
            var bytes = Encoding.UTF8.GetBytes(Json.Serialize(values));
            request.ContentLength = bytes.Length;
            using (var stream = request.GetRequestStream())
                stream.Write(bytes, 0, bytes.Length);

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    return Json.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    using (response)
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        var body = reader.ReadToEnd();
                        try
                        {
                            var data = Json.Deserialize<Dictionary<string, object>>(body);
                            throw new InvalidOperationException(GetString(data, "message", "تعذر التحقق من الاشتراك."));
                        }
                        catch (InvalidOperationException) { throw; }
                        catch { throw new InvalidOperationException("رفض الخادم طلب التفعيل."); }
                    }
                }
                throw new InvalidOperationException("تعذر الاتصال بخادم التفعيل. تحقق من الإنترنت.");
            }
        }

        private static string DeviceId()
        {
            string machineGuid = null;
            try
            {
                using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    machineGuid = key == null ? null : Convert.ToString(key.GetValue("MachineGuid"));
            }
            catch { }

            if (string.IsNullOrWhiteSpace(machineGuid))
            {
                var sid = WindowsIdentity.GetCurrent().User;
                machineGuid = Environment.MachineName + "|" + (sid == null ? "unknown" : sid.Value);
            }

            var raw = machineGuid + "|BODEX-PROXY|HB-LICENSE-V1";
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static LicenseRecord Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return null;
                var protectedBytes = File.ReadAllBytes(StorePath);
                var clear = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Json.Deserialize<LicenseRecord>(Encoding.UTF8.GetString(clear));
            }
            catch { return null; }
        }

        private static void Save(LicenseRecord record)
        {
            var directory = Path.GetDirectoryName(StorePath);
            Directory.CreateDirectory(directory);
            var clear = Encoding.UTF8.GetBytes(Json.Serialize(record));
            var protectedBytes = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(StorePath, protectedBytes);
        }

        private static void Delete()
        {
            try { if (File.Exists(StorePath)) File.Delete(StorePath); } catch { }
        }

        private static bool GetBool(Dictionary<string, object> data, string key)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) && value is bool && (bool)value;
        }

        private static string GetString(Dictionary<string, object> data, string key, string fallback)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value) : fallback;
        }

        private sealed class LicenseRecord
        {
            public string Token { get; set; }
            public long LastOk { get; set; }
        }
    }
}
