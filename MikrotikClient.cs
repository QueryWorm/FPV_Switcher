using System;
using System.Collections.Generic;
using System.Linq;
using tik4net;

namespace MikrotikSwitch
{
    public class PortInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public bool Running { get; set; }
        public bool Disabled { get; set; }
    }

    public class MikrotikClient : IDisposable
    {
        private ITikConnection? _connection;
        private readonly object _lock = new object();
        private readonly string _host;
        private readonly int _port;
        private readonly string _user;
        private readonly string _pass;
        private bool _disposed;

        public bool IsConnected
        {
            get { lock (_lock) { return _connection != null && _connection.IsOpened; } }
        }

        public MikrotikClient(string address, string user, string pass)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("Адрес не может быть пустым", nameof(address));

            var parts = address.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int port) || port < 1 || port > 65535)
                throw new ArgumentException($"Неверный формат адреса: {address}. Ожидается host:port", nameof(address));

            _host = parts[0];
            _port = port;
            _user = user ?? "";
            _pass = pass ?? "";

            OpenConnection();
        }

        private void OpenConnection()
        {
            lock (_lock)
            {
                _connection?.Dispose();
                _connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                _connection.Open(_host, _port, _user, _pass);
            }
        }

        public void EnsureConnected()
        {
            lock (_lock)
            {
                if (_connection == null || !_connection.IsOpened)
                    OpenConnection();
            }
        }

        public List<PortInfo> ListEthernetPorts()
        {
            lock (_lock)
            {
                EnsureConnectedLocked();

                var result = new List<PortInfo>();

                try
                {
                    var reply = _connection!.CallCommandSync("/interface/ethernet/print");

                    foreach (var sentence in reply)
                    {
                        if (sentence.Words.ContainsKey(".id"))
                        {
                            result.Add(new PortInfo
                            {
                                Id = sentence.Words[".id"],
                                Name = sentence.Words.ContainsKey("name") ? sentence.Words["name"] : "",
                                Running = sentence.Words.ContainsKey("running") && sentence.Words["running"] == "true",
                                Disabled = sentence.Words.ContainsKey("disabled") && sentence.Words["disabled"] == "true"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    TryReconnect();
                    throw new Exception($"Error listing ethernet ports: {ex.Message}", ex);
                }

                return result;
            }
        }

        public void SetPortEnabled(string id, bool enabled)
        {
            lock (_lock)
            {
                EnsureConnectedLocked();

                try
                {
                    string cmd = enabled ? "/interface/enable" : "/interface/disable";
                    _connection!.CallCommandSync(cmd, $"=.id={id}");
                }
                catch (Exception ex)
                {
                    TryReconnect();
                    throw new Exception($"Error setting port state: {ex.Message}", ex);
                }
            }
        }

        private void EnsureConnectedLocked()
        {
            if (_connection == null || !_connection.IsOpened)
                OpenConnection();
        }

        private void TryReconnect()
        {
            try
            {
                OpenConnection();
            }
            catch
            {
                // переподключение не удалось — следующий вызов снова попробует
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                if (_connection != null)
                {
                    try { _connection.Close(); } catch { }
                    _connection.Dispose();
                    _connection = null;
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
