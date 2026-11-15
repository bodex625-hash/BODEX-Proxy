using System;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace BODEX.Proxy.Vpn
{
    /// <summary>
    /// Owns the tun2socks child process and its private configuration file.
    /// This class deliberately does not claim that Windows routes, DNS, or a kill switch
    /// are configured. Those must be applied transactionally by the privileged VPN service.
    /// </summary>
    public sealed class BodexTun2SocksSupervisor : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _enginePath;
        private readonly string _stateDirectory;
        private Process _process;
        private string _configPath;
        private bool _disposed;

        public event EventHandler<EngineExitedEventArgs> EngineExited;

        public BodexTun2SocksSupervisor(string enginePath, string stateDirectory)
        {
            if (String.IsNullOrWhiteSpace(enginePath)) throw new ArgumentException("Engine path is required.", nameof(enginePath));
            if (String.IsNullOrWhiteSpace(stateDirectory)) throw new ArgumentException("State directory is required.", nameof(stateDirectory));
            _enginePath = Path.GetFullPath(enginePath);
            _stateDirectory = Path.GetFullPath(stateDirectory);
        }

        public bool IsRunning
        {
            get
            {
                lock (_gate) return _process != null && !_process.HasExited;
            }
        }

        public void Start(string proxyHost, int proxyPort, string username, string password,
            string tunName, int mtu)
        {
            if (String.IsNullOrWhiteSpace(proxyHost)) throw new ArgumentException("SOCKS5 host is required.", nameof(proxyHost));
            if (proxyPort < 1 || proxyPort > 65535) throw new ArgumentOutOfRangeException(nameof(proxyPort));
            if (String.IsNullOrWhiteSpace(tunName)) throw new ArgumentException("Wintun adapter name is required.", nameof(tunName));
            if (mtu < 576 || mtu > 9000) throw new ArgumentOutOfRangeException(nameof(mtu));

            lock (_gate)
            {
                ThrowIfDisposed();
                if (_process != null && !_process.HasExited)
                    throw new InvalidOperationException("BODEX tunnel engine is already running.");
                if (!File.Exists(_enginePath))
                    throw new FileNotFoundException("BODEX tunnel engine was not found.", _enginePath);

                Directory.CreateDirectory(_stateDirectory);
                RestrictDirectoryToSystemAndAdministrators(_stateDirectory);
                _configPath = Path.Combine(_stateDirectory, "tun2socks.runtime.yaml");
                WritePrivateConfig(_configPath, BuildProxyUri(proxyHost, proxyPort, username, password), tunName, mtu);

                var start = new ProcessStartInfo
                {
                    FileName = _enginePath,
                    Arguments = "--config " + QuoteArgument(_configPath),
                    WorkingDirectory = Path.GetDirectoryName(_enginePath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var child = new Process { StartInfo = start, EnableRaisingEvents = true };
                child.Exited += OnEngineExited;
                try
                {
                    if (!child.Start()) throw new InvalidOperationException("Windows did not start the BODEX tunnel engine.");
                    _process = child;
                }
                catch
                {
                    child.Dispose();
                    SecureDeleteConfig();
                    throw;
                }
            }
        }

        public void Stop(TimeSpan timeout)
        {
            Process child;
            lock (_gate)
            {
                child = _process;
                if (child == null) { SecureDeleteConfig(); return; }
                if (child.HasExited) { _process = null; child.Dispose(); SecureDeleteConfig(); return; }
            }

            // tun2socks is a console process and handles CTRL+C/SIGTERM. A Windows service
            // cannot reliably deliver CTRL+C to a detached child, so stop it explicitly.
            if (!child.WaitForExit((int)Math.Max(0, timeout.TotalMilliseconds)))
            {
                child.Kill();
                if (!child.WaitForExit(5000))
                    throw new TimeoutException("BODEX tunnel engine did not terminate after the forced stop.");
            }

            lock (_gate)
            {
                if (Object.ReferenceEquals(_process, child)) _process = null;
                child.Dispose();
                SecureDeleteConfig();
            }
        }

        private void OnEngineExited(object sender, EventArgs e)
        {
            var child = sender as Process;
            int code = -1;
            try { if (child != null) code = child.ExitCode; } catch { }
            lock (_gate)
            {
                if (Object.ReferenceEquals(_process, child))
                {
                    _process = null;
                    SecureDeleteConfig();
                }
            }
            var handler = EngineExited;
            if (handler != null) handler(this, new EngineExitedEventArgs(code));
        }

        private static string BuildProxyUri(string host, int port, string user, string pass)
        {
            var credentials = String.IsNullOrEmpty(user) && String.IsNullOrEmpty(pass)
                ? String.Empty
                : Uri.EscapeDataString(user ?? String.Empty) + ":" + Uri.EscapeDataString(pass ?? String.Empty) + "@";
            return "socks5://" + credentials + host + ":" + port;
        }

        private static void WritePrivateConfig(string path, string proxy, string tun, int mtu)
        {
            // YAML double-quoted scalar; escape backslash and quote before writing.
            var safeProxy = proxy.Replace("\\", "\\\\").Replace("\"", "\\\"");
            var safeTun = tun.Replace("\\", "\\\\").Replace("\"", "\\\"");
            File.WriteAllText(path,
                "proxy: \"" + safeProxy + "\"\r\n" +
                "device: \"tun://" + safeTun + "\"\r\n" +
                "mtu: " + mtu + "\r\n" +
                "loglevel: info\r\n",
                new UTF8Encoding(false));

            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl, AccessControlType.Allow));
            File.SetAccessControl(path, security);
        }

        private static void RestrictDirectoryToSystemAndAdministrators(string directory)
        {
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(directory).SetAccessControl(security);
        }

        private void SecureDeleteConfig()
        {
            if (String.IsNullOrEmpty(_configPath)) return;
            try { if (File.Exists(_configPath)) File.Delete(_configPath); } catch { }
            _configPath = null;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BodexTun2SocksSupervisor));
        }

        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; }
            Stop(TimeSpan.FromSeconds(3));
            lock (_gate) _disposed = true;
        }
    }

    public sealed class EngineExitedEventArgs : EventArgs
    {
        public EngineExitedEventArgs(int exitCode) { ExitCode = exitCode; }
        public int ExitCode { get; private set; }
    }
}
