using Newtonsoft.Json;
using SABPlus.Radial.Core.Models;
using System;
using System.IO;
using System.Text;

namespace SABPlus.Radial.Core.Services
{
    public static class JsonSerialization
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            NullValueHandling = NullValueHandling.Include
        };

        public static string Serialize<T>(T value, bool indented)
        {
            return JsonConvert.SerializeObject(
                value,
                indented ? Formatting.Indented : Formatting.None,
                SerializerSettings);
        }

        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("JSON-файл пуст.");
            }

            T value = JsonConvert.DeserializeObject<T>(json, SerializerSettings);
            if (value == null)
            {
                throw new InvalidDataException("Не удалось прочитать JSON.");
            }

            return value;
        }

        public static T DeepClone<T>(T value)
        {
            return Deserialize<T>(Serialize(value, false));
        }
    }

    public sealed class AtomicJsonFileStore<T>
    {
        private readonly string _filePath;
        private readonly Action<T> _validate;

        public string FilePath => _filePath;

        public string BackupPath => _filePath + ".bak";

        public AtomicJsonFileStore(string filePath, Action<T> validate)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Путь к JSON не задан.", nameof(filePath));
            }

            _filePath = filePath;
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
        }

        public bool Exists()
        {
            return File.Exists(_filePath);
        }

        public T Load()
        {
            string json = File.ReadAllText(_filePath, Encoding.UTF8);
            T value = JsonSerialization.Deserialize<T>(json);
            _validate(value);
            return value;
        }

        public T LoadBackup()
        {
            string json = File.ReadAllText(BackupPath, Encoding.UTF8);
            T value = JsonSerialization.Deserialize<T>(json);
            _validate(value);
            return value;
        }

        public void SaveAtomic(T value)
        {
            _validate(value);

            string directory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Не удалось определить папку настроек.");
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = _filePath + ".tmp." + Guid.NewGuid().ToString("N");

            try
            {
                string json = JsonSerialization.Serialize(value, true);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

                // Important block: validate exactly what was written before replacing user settings.
                string writtenJson = File.ReadAllText(temporaryPath, Encoding.UTF8);
                T writtenValue = JsonSerialization.Deserialize<T>(writtenJson);
                _validate(writtenValue);

                if (File.Exists(_filePath))
                {
                    File.Replace(temporaryPath, _filePath, BackupPath, true);
                }
                else
                {
                    File.Move(temporaryPath, _filePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    public sealed class WheelSettingsRepository
    {
        private readonly AtomicJsonFileStore<WheelSettings> _settingsStore;
        private readonly AtomicJsonFileStore<ProjectWheelStateCollection> _projectStateStore;

        public string SettingsFilePath => _settingsStore.FilePath;

        public WheelSettingsRepository(string settingsDirectory)
        {
            if (string.IsNullOrWhiteSpace(settingsDirectory))
            {
                throw new ArgumentException("Папка настроек не задана.", nameof(settingsDirectory));
            }

            _settingsStore = new AtomicJsonFileStore<WheelSettings>(
                Path.Combine(settingsDirectory, "wheel-settings.json"),
                ValidateSettings);

            _projectStateStore = new AtomicJsonFileStore<ProjectWheelStateCollection>(
                Path.Combine(settingsDirectory, "project-wheel-state.json"),
                ValidateProjectStates);
        }

        public WheelSettings LoadOrCreateDefault()
        {
            if (!_settingsStore.Exists())
            {
                WheelSettings defaults = WheelSettingsFactory.CreateDefault();
                _settingsStore.SaveAtomic(defaults);
                return defaults;
            }

            WheelSettings settings = _settingsStore.Load();
            WheelSettingsValidator.Normalize(settings);
            return settings;
        }

        public void Save(WheelSettings settings)
        {
            WheelSettingsValidator.Normalize(settings);
            _settingsStore.SaveAtomic(settings);
        }

        public WheelSettings LoadBackup()
        {
            WheelSettings settings = _settingsStore.LoadBackup();
            WheelSettingsValidator.Normalize(settings);
            return settings;
        }

        public ProjectWheelStateCollection LoadProjectStates()
        {
            if (!_projectStateStore.Exists())
            {
                return new ProjectWheelStateCollection();
            }

            return _projectStateStore.Load();
        }

        public void SaveProjectStates(ProjectWheelStateCollection states)
        {
            _projectStateStore.SaveAtomic(states);
        }

        private static void ValidateSettings(WheelSettings settings)
        {
            WheelSettingsValidator.Normalize(settings);
            WheelSettingsValidationResult result = WheelSettingsValidator.Validate(settings);
            if (!result.IsValid)
            {
                throw new InvalidDataException(string.Join(Environment.NewLine, result.Errors));
            }
        }

        private static void ValidateProjectStates(ProjectWheelStateCollection states)
        {
            if (states == null)
            {
                throw new InvalidDataException("Состояние проектов отсутствует.");
            }

            states.Projects = states.Projects ?? new System.Collections.Generic.List<ProjectWheelState>();
        }
    }
}
