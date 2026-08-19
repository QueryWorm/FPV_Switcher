using System;
using System.IO;
using Newtonsoft.Json;

namespace MikrotikSwitch
{
    public class Config
    {
        public string Address { get; set; } = "192.168.121.2:8728";
        public string User { get; set; } = "admin";
        public string Pass { get; set; } = "1";
        public string[] PortNames { get; set; } = new[] { "ether1", "ether2", "ether3", "ether4", "ether5", "ether6" };
        public int PollSeconds { get; set; } = 2;

        private static readonly string ConfigFile = Path.Combine(
            AppContext.BaseDirectory, "config.json");

        public static Config Load()
        {
            if (!File.Exists(ConfigFile))
            {
                var def = new Config();
                File.WriteAllText(ConfigFile, JsonConvert.SerializeObject(def, Formatting.Indented));
                throw new Exception($"Конфиг {ConfigFile} не найден – создан шаблон. Заполните address/user/pass и перезапустите.");
            }

            var json = File.ReadAllText(ConfigFile);
            var cfg = JsonConvert.DeserializeObject<Config>(json)
                      ?? throw new Exception($"Конфиг {ConfigFile} пуст или невалиден");

            cfg.PortNames ??= new[] { "ether1", "ether2", "ether3", "ether4", "ether5", "ether6" };
            if (cfg.PortNames.Length != 6)
                throw new Exception($"В {ConfigFile} должно быть ровно 6 имён в port_names, сейчас {cfg.PortNames.Length}");
            if (cfg.PollSeconds <= 0) cfg.PollSeconds = 2;
            return cfg;
        }
    }
}
